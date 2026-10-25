using System.Text.Json;
using Logica;

namespace Servidor;

/// <summary>Cuenta que usa Mérito para enviar correos. Con Gmail: smtp.gmail.com, puerto 587 y una contraseña de aplicación.</summary>
public sealed record ConfiguracionCorreo(string Servidor, int Puerto, string Usuario, string Clave, string Remitente, string Nombre);

public static class Correos
{
    // Con carpeta de pruebas, los correos se escriben en archivos y no salen a internet; solo para desarrollo y pruebas.
    public static IEnviadorCorreo Crear(ConfiguracionCorreo? cuenta, string? carpetaPruebas, ILoggerFactory registros) =>
        new EnviadorCorreo(carpetaPruebas is { Length: > 0 } ? new BuzonDePruebas(carpetaPruebas)
        : cuenta is not null ? new EnviadorSmtp(cuenta, registros.CreateLogger<EnviadorSmtp>())
        : new SinCorreo());
}

public sealed class EnviadorSmtp(ConfiguracionCorreo cuenta, ILogger<EnviadorSmtp> registro) : ITransporteCorreo
{
    public bool Disponible => true;

    public async Task Enviar(string para, string asunto, string texto, string html, CancellationToken ct)
    {
        var mensaje = new MimeKit.MimeMessage();
        mensaje.From.Add(new MimeKit.MailboxAddress(cuenta.Nombre, cuenta.Remitente));
        mensaje.To.Add(MimeKit.MailboxAddress.Parse(para));
        mensaje.Subject = asunto;
        mensaje.Body = new MimeKit.BodyBuilder { TextBody = texto, HtmlBody = html }.ToMessageBody();
        using var cliente = new MailKit.Net.Smtp.SmtpClient { Timeout = 20_000 };
        try
        {
            await Conectar(cliente, ct);
            await cliente.AuthenticateAsync(cuenta.Usuario, cuenta.Clave, ct);
            await cliente.SendAsync(mensaje, ct);
            await cliente.DisconnectAsync(true, ct);
        }
        catch (Exception error) when (error is MailKit.Security.AuthenticationException or MailKit.ProtocolException or MailKit.CommandException
            or System.Net.Sockets.SocketException or IOException or TimeoutException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            // El detalle queda en el registro del servidor; a la persona se le dice qué hacer.
            var detalle = string.Join(" | ", Cadena(error).Select(e => $"{e.GetType().Name}: {e.Message}"));
            registro.LogWarning("No se pudo enviar un correo con {Usuario} por {Servidor}: {Detalle}", cuenta.Usuario, cuenta.Servidor, detalle);
            throw new ReglaException("No se pudo enviar el correo. Intenta de nuevo en unos minutos; si sigue fallando, avisa a administración.", TipoErrorRegla.ServicioExternoNoDisponible);
        }
    }

    // Primero el puerto configurado (587 con STARTTLS); si la red lo corta, el 465 con SSL directo, que suele quedar abierto.
    private async Task Conectar(MailKit.Net.Smtp.SmtpClient cliente, CancellationToken ct)
    {
        try { await cliente.ConnectAsync(cuenta.Servidor, cuenta.Puerto, MailKit.Security.SecureSocketOptions.Auto, ct); }
        catch (Exception error) when (cuenta.Puerto != 465 && error is System.Net.Sockets.SocketException or IOException or MailKit.ProtocolException
            or System.Security.Authentication.AuthenticationException or TimeoutException)
        {
            registro.LogInformation("El puerto {Puerto} no respondió ({Detalle}); se intenta el 465.", cuenta.Puerto, error.Message);
            await cliente.ConnectAsync(cuenta.Servidor, 465, MailKit.Security.SecureSocketOptions.SslOnConnect, ct);
        }
    }

    // Los mensajes genéricos no dicen nada: el motivo real suele estar en las excepciones internas.
    private static IEnumerable<Exception> Cadena(Exception error)
    {
        for (var actual = error; actual is not null; actual = actual.InnerException) yield return actual;
    }
}
public sealed class BuzonDePruebas(string carpeta) : ITransporteCorreo
{
    public bool Disponible => true;

    public async Task Enviar(string para, string asunto, string texto, string html, CancellationToken ct)
    {
        Directory.CreateDirectory(carpeta);
        var nombre = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json";
        await File.WriteAllTextAsync(Path.Combine(carpeta, nombre), JsonSerializer.Serialize(new { para, asunto, texto, html }), ct);
    }
}

public sealed class SinCorreo : ITransporteCorreo
{
    public bool Disponible => false;
    public Task Enviar(string para, string asunto, string texto, string html, CancellationToken ct) =>
        throw new ReglaException("Este servidor no tiene cuenta de correo configurada.");
}
