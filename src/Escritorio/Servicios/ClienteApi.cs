using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Entidades;

namespace Escritorio.Servicios;

public sealed class ErrorServicio(string mensaje, HttpStatusCode estado, string? referencia = null) : Exception(mensaje)
{
    public HttpStatusCode Estado { get; } = estado;
    public string? Referencia { get; } = referencia;
}

public sealed class ClienteApi : IDisposable
{
    private HttpClient cliente = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public Persona? Persona { get; private set; }
    public event EventHandler? SesionVencida;
    public string Direccion => cliente.BaseAddress?.ToString() ?? "";

    public void Configurar(string direccion)
    {
        if (!Uri.TryCreate(direccion.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https")) throw new ArgumentException("Escribe una dirección de servidor válida.");
        if (uri.Scheme == "http" && !Logica.RedLocal.EsDireccionLocal(uri))
            throw new ArgumentException("Fuera de la red local la dirección debe usar HTTPS.");
        if (uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.AbsolutePath != "/")
            throw new ArgumentException("La dirección debe contener solo el protocolo, servidor y puerto.");
        if (cliente.BaseAddress == uri) return;
        cliente.Dispose();
        cliente = new HttpClient { BaseAddress = uri, Timeout = TimeSpan.FromSeconds(30) };
    }

    private sealed record Salud(string Servicio, int Version);

    public async Task<int> Probar(string direccion)
    {
        Configurar(direccion);
        var salud = await Enviar<Salud>(HttpMethod.Get, "salud");
        if (salud.Servicio != "merito") throw new ErrorServicio("En esa dirección responde otro programa, no un servidor de Mérito.", HttpStatusCode.BadGateway);
        return salud.Version;
    }

    public async Task Iniciar(string direccion, string cedula, string clave)
    {
        Configurar(direccion);
        var sesion = await Enviar<Sesion>(HttpMethod.Post, "acceso", new Acceso(cedula, clave));
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sesion.Token);
        Persona = sesion.Persona;
    }

    public async Task Salir()
    {
        try { await Enviar(HttpMethod.Post, "salir", new { }); }
        finally { OlvidarSesion(); }
    }

    public void OlvidarSesion()
    {
        Persona = null;
        cliente.DefaultRequestHeaders.Authorization = null;
    }

    public Task<T> Obtener<T>(string ruta, CancellationToken ct = default) => Enviar<T>(HttpMethod.Get, ruta, null, ct);

    public async Task<T> Enviar<T>(HttpMethod metodo, string ruta, object? datos = null, CancellationToken ct = default)
    {
        using var peticion = new HttpRequestMessage(metodo, ruta);
        if (datos is not null) peticion.Content = JsonContent.Create(datos);
        using var respuesta = await Ejecutar(peticion, ct);
        return await respuesta.Content.ReadFromJsonAsync<T>(Json, ct)
            ?? throw new ErrorServicio("El servidor devolvió una respuesta vacía.", respuesta.StatusCode);
    }

    public async Task Enviar(HttpMethod metodo, string ruta, object datos, CancellationToken ct = default)
    {
        using var peticion = new HttpRequestMessage(metodo, ruta) { Content = JsonContent.Create(datos) };
        using var respuesta = await Ejecutar(peticion, ct);
    }

    public Task<VistaImportacion> LeerExcel(long oferta, byte[] archivo) => Subir<VistaImportacion>($"ofertas/{oferta}/leer-excel", archivo);

    // El servidor lee el Excel y devuelve lo que entendió, sin guardar nada.
    public async Task<T> Subir<T>(string ruta, byte[] archivo)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Post, ruta) { Content = new ByteArrayContent(archivo) };
        peticion.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var respuesta = await Ejecutar(peticion, CancellationToken.None);
        return await respuesta.Content.ReadFromJsonAsync<T>(Json)
            ?? throw new ErrorServicio("No se pudo leer el archivo.", respuesta.StatusCode);
    }

    public async Task<byte[]> Descargar(string ruta)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Get, ruta);
        using var respuesta = await Ejecutar(peticion, CancellationToken.None);
        return await respuesta.Content.ReadAsByteArrayAsync();
    }

    private async Task<HttpResponseMessage> Ejecutar(HttpRequestMessage peticion, CancellationToken ct)
    {
        HttpResponseMessage respuesta;
        try { respuesta = await cliente.SendAsync(peticion, ct); }
        catch (HttpRequestException) { throw new ErrorServicio("No hay conexión con el servidor. Comprueba la red; los cambios sin guardar se conservan en pantalla.", HttpStatusCode.ServiceUnavailable); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        { throw new ErrorServicio("Se agotó el tiempo de espera. Recarga para verificar si la operación llegó a guardarse.", HttpStatusCode.RequestTimeout); }
        if (respuesta.IsSuccessStatusCode) return respuesta;
        try
        {
            var error = await respuesta.Content.ReadFromJsonAsync<ErrorApi>(Json, ct);
            if (respuesta.StatusCode == HttpStatusCode.Unauthorized) AvisarVencimiento(peticion);
            throw new ErrorServicio(error?.Mensaje ?? "El servidor rechazó la operación.", respuesta.StatusCode, error?.Referencia);
        }
        catch (JsonException)
        {
            if (respuesta.StatusCode == HttpStatusCode.Unauthorized) AvisarVencimiento(peticion);
            throw new ErrorServicio(respuesta.StatusCode == HttpStatusCode.TooManyRequests
                ? "Demasiados intentos. Espera un minuto antes de volver a intentarlo."
                : "El servidor rechazó la solicitud. Comprueba los datos y tu sesión.", respuesta.StatusCode);
        }
        finally { respuesta.Dispose(); }
    }

    private void AvisarVencimiento(HttpRequestMessage peticion)
    {
        var ruta = peticion.RequestUri?.OriginalString ?? "";
        if (Persona is null || ruta is "acceso" or "activar" or "salir") return;
        OlvidarSesion();
        SesionVencida?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => cliente.Dispose();
}
