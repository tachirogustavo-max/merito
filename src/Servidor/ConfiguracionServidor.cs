using System.Text.Json.Nodes;

namespace Servidor;

/// <summary>
/// Configuración del servidor. La deja el instalador en una carpeta que solo leen SYSTEM y administración;
/// cada valor puede sobrescribirse con su variable de entorno. La cuenta inicial por variables es solo para desarrollo:
/// en una instalación se crea desde el programa con el código de configuración.
/// </summary>
public sealed record ConfiguracionServidor(string? BaseDatos, string? BaseDatosMigracion, string? AdminCedula, string? AdminClave,
    string? Urls, bool ExigirHttps, bool Migrar, ConfiguracionCorreo? Correo = null, string? CarpetaCorreoPruebas = null, string? AvisoCorreo = null)
{
    public static string Ruta => Environment.GetEnvironmentVariable("MERITO_CONFIGURACION")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Merito", "privado", "servidor.json");

    /// <summary>La cuenta de correo va aparte, junto a servidor.json, para cambiarla sin tocar las conexiones a la base de datos.</summary>
    public static string RutaCorreo => Path.Combine(Path.GetDirectoryName(Ruta) ?? ".", "correo.json");

    public static ConfiguracionServidor Cargar()
    {
        var archivo = File.Exists(Ruta) ? JsonNode.Parse(File.ReadAllText(Ruta)) as JsonObject : null;
        string? Valor(string variable, string clave) => Environment.GetEnvironmentVariable(variable) ?? archivo?[clave]?.GetValue<string>();
        var (seccionCorreo, aviso) = SeccionCorreo(archivo);
        return new ConfiguracionServidor(
            Valor("ACADEMICO_BD", "BaseDatos"),
            Valor("ACADEMICO_BD_MIGRACION", "BaseDatosMigracion"),
            Environment.GetEnvironmentVariable("ACADEMICO_ADMIN_CEDULA"),
            Environment.GetEnvironmentVariable("ACADEMICO_ADMIN_CLAVE"),
            Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? archivo?["Urls"]?.GetValue<string>(),
            Environment.GetEnvironmentVariable("ACADEMICO_EXIGIR_HTTPS") == "1" || archivo?["ExigirHttps"]?.GetValue<bool>() == true,
            Environment.GetEnvironmentVariable("ACADEMICO_MIGRAR") == "1",
            LeerCorreo(seccionCorreo),
            Environment.GetEnvironmentVariable("ACADEMICO_CORREO_CARPETA"),
            aviso);
    }

    // correo.json manda; si no existe, la sección «Correo» de servidor.json de las primeras instalaciones.
    // Un correo.json mal escrito no impide arrancar: el servidor funciona sin correo y lo deja dicho en el registro.
    private static (JsonObject? Seccion, string? Aviso) SeccionCorreo(JsonObject? servidor)
    {
        if (!File.Exists(RutaCorreo)) return (servidor?["Correo"] as JsonObject, null);
        try { return (JsonNode.Parse(File.ReadAllText(RutaCorreo)) as JsonObject, null); }
        catch (System.Text.Json.JsonException error)
        { return (null, $"{RutaCorreo} está mal escrito cerca de la línea {(error.LineNumber ?? 0) + 1}: revisa comillas, comas y llaves. Mérito funciona sin correo hasta que se corrija."); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { return (null, $"No se pudo abrir {RutaCorreo} ({error.Message}). Mérito funciona sin correo hasta que se corrija."); }
    }

    // Las variables ACADEMICO_SMTP_* sobrescriben el archivo. Sin usuario y clave no hay envío por correo.
    private static ConfiguracionCorreo? LeerCorreo(JsonObject? seccion)
    {
        string? Valor(string variable, string clave) =>
            (Environment.GetEnvironmentVariable(variable) ?? seccion?[clave]?.ToString()) is { } texto && texto.Trim().Length > 0 ? texto.Trim() : null;
        var usuario = Valor("ACADEMICO_SMTP_USUARIO", "Usuario");
        var clave = Valor("ACADEMICO_SMTP_CLAVE", "Clave");
        if (usuario is null || clave is null) return null;
        var puerto = int.TryParse(Valor("ACADEMICO_SMTP_PUERTO", "Puerto"), out var p) ? p : 587;
        // La contraseña de aplicación de Google se muestra en grupos con espacios; se quitan.
        return new ConfiguracionCorreo(Valor("ACADEMICO_SMTP_SERVIDOR", "Servidor") ?? "smtp.gmail.com", puerto, usuario, clave.Replace(" ", ""),
            Valor("ACADEMICO_SMTP_REMITENTE", "Remitente") ?? usuario, Valor("ACADEMICO_SMTP_NOMBRE", "Nombre") ?? "Mérito");
    }
}
