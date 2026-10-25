using System.Net;

namespace Servidor;

/// <summary>Correos que envía Mérito, en texto y en HTML sencillo.</summary>
public static class Mensajes
{
    public static (string Asunto, string Texto, string Html) CodigoAcceso(string nombres, string codigo, bool registrada)
    {
        // No es una clave para el login: solo sirve en la ventana «¿Primera vez?» para elegir la contraseña.
        var (entrada, motivo) = registrada
            ? ("¿Se te olvidó la contraseña? Tranqui, pasa en las mejores familias. Aquí tienes tu código:", "elige una contraseña nueva")
            : ("¡Qué bueno tenerte en Mérito! Ya casi está lista tu cuenta; este es tu código:", "crea tu contraseña");
        var pasos = $"Escríbelo en la ventana «¿Primera vez u olvidaste tu contraseña?» y {motivo}. Vence en 15 minutos y sirve una sola vez.";
        const string cambio = "Si más adelante quieres cambiarla, entra a Mérito, toca tu nombre o el logo de arriba y elige «Cambiar contraseña».";
        const string ajeno = "¿No lo pediste? No pasa nada: ignora este correo y tu cuenta sigue igual.";
        var texto = $"""
            ¡Hola, {nombres}! 👋

            {entrada}

                {codigo}

            {pasos}

            {cambio}

            ¡Éxitos en este período!

            {ajeno}
            """;
        var html = Plantilla($"""
            <p style="margin:0 0 16px;font-size:18px;font-weight:600;color:#1E1C6E">¡Hola, {WebUtility.HtmlEncode(nombres)}! 👋</p>
            <p style="margin:0 0 20px">{WebUtility.HtmlEncode(entrada)}</p>
            <p style="margin:0 0 20px;padding:18px 0;text-align:center;background:#F1F1FB;border-radius:10px;font:600 34px/1 Consolas,'Courier New',monospace;letter-spacing:8px;color:#1E1C6E">{codigo}</p>
            <p style="margin:0 0 16px">{WebUtility.HtmlEncode(pasos)}</p>
            <p style="margin:0 0 16px">{WebUtility.HtmlEncode(cambio)}</p>
            <p style="margin:0 0 20px;font-weight:600">¡Éxitos en este período!</p>
            <p style="margin:0;color:#6B718C">{WebUtility.HtmlEncode(ajeno)}</p>
            """);
        return ($"¡Hola, {nombres}! Tu código de Mérito es {codigo}", texto, html);
    }

    public static (string Asunto, string Texto, string Html) Prueba(string quien)
    {
        var texto = $"¡Llegó! Este es un correo de prueba de Mérito, enviado por {quien}. El registro y la recuperación de contraseña por correo ya funcionan.";
        return ("¡Prueba de correo de Mérito!", texto, Plantilla($"<p style=\"margin:0\">{WebUtility.HtmlEncode(texto)}</p>"));
    }

    private static string Plantilla(string cuerpo) => $"""
        <!doctype html>
        <html lang="es"><body style="margin:0;padding:24px;background:#F4F5FA;font:15px/1.55 'Segoe UI',Arial,sans-serif;color:#171A2C">
        <div style="max-width:480px;margin:0 auto;background:#FFFFFF;border-radius:14px;overflow:hidden;border:1px solid #E2E5EE">
        <div style="height:5px;background:#4343D0"></div>
        <div style="padding:28px 32px">
        <p style="margin:0 0 22px;font-weight:600;font-size:18px;color:#4343D0">Mérito</p>
        {cuerpo}
        </div></div>
        <p style="max-width:480px;margin:14px auto 0;font-size:12px;color:#8A8EA6;text-align:center">Mensaje automático del registro académico. No respondas a este correo.</p>
        </body></html>
        """;
}
