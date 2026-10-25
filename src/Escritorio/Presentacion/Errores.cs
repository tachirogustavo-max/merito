using System.IO;
using System.Net;
using System.Windows;
using Escritorio.Servicios;
using Logica;

namespace Escritorio.Presentacion;

/// <summary>
/// Decide cómo comunicar cada fallo: los datos por corregir se avisan con una notificación
/// y los problemas de conexión, conflictos o fallos inesperados con un modal.
/// </summary>
public static class Errores
{
    public static string? Presentar(Window? ventana, Exception error)
    {
        switch (error)
        {
            case OperacionCancelada:
                return null;
            case ErrorServicio { Estado: HttpStatusCode.Unauthorized } sesion:
                // La ventana principal ya devuelve a la pantalla de acceso.
                return sesion.Message;
            case ErrorServicio { Estado: HttpStatusCode.Conflict } conflicto:
                Dialogo.Informar(ventana, "No se aplicó el cambio", conflicto.Message +
                    "\n\nLo que ves en pantalla no se ha perdido. Recarga para comparar con la versión vigente antes de reintentar.", Tono.Aviso, conflicto.Referencia);
                return conflicto.Message;
            case ErrorServicio { Estado: HttpStatusCode.ServiceUnavailable or HttpStatusCode.RequestTimeout or HttpStatusCode.InternalServerError
                or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout } grave:
                var sinConexion = grave.Referencia is null;
                Dialogo.Informar(ventana, sinConexion ? "Sin conexión con el servidor" : "No se pudo completar la operación", grave.Message, Tono.Peligro, grave.Referencia);
                return grave.Message;
            case ErrorServicio { Estado: HttpStatusCode.Forbidden } permiso:
                VentanaBase.Notificar(ventana, permiso.Message, Tono.Peligro, "Acción no permitida");
                return permiso.Message;
            case ErrorServicio { Estado: HttpStatusCode.TooManyRequests } limite:
                VentanaBase.Notificar(ventana, limite.Message, Tono.Aviso, "Demasiados intentos");
                return limite.Message;
            case ErrorServicio rechazo:
                Avisar(ventana, rechazo.Message);
                return rechazo.Message;
            case ArgumentException or ReglaException:
                Avisar(ventana, error.Message);
                return error.Message;
            case IOException or UnauthorizedAccessException:
                Dialogo.Informar(ventana, "No se pudo usar el archivo",
                    error.Message + "\n\nComprueba que no esté abierto en otro programa y que tengas permiso sobre la carpeta.", Tono.Peligro);
                return error.Message;
            default:
                var referencia = RegistroErrores.Registrar(error);
                Dialogo.Informar(ventana, "Algo salió mal",
                    "La operación no se completó. Los datos en pantalla se conservan; si vuelve a ocurrir, comunica la referencia a administración.",
                    Tono.Peligro, referencia);
                return referencia;
        }
    }

    // Los mensajes largos, como los errores de un Excel, necesitan espacio para leerse.
    private static void Avisar(Window? ventana, string mensaje)
    {
        if (mensaje.Count(c => c == '\n') >= 2) Dialogo.Informar(ventana, "Hay datos por corregir", mensaje, Tono.Aviso);
        else VentanaBase.Notificar(ventana, mensaje, Tono.Aviso, "Revisa los datos");
    }
}
