using System.IO;

namespace Escritorio.Servicios;

internal static class RegistroErrores
{
    public static string Registrar(Exception error)
    {
        var referencia = Guid.NewGuid().ToString("N")[..12];
        try
        {
            var directorio = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Merito", "diagnostico");
            Directory.CreateDirectory(directorio);
            File.AppendAllText(Path.Combine(directorio, $"errores-{DateTime.Today:yyyy-MM-dd}.log"),
                $"{DateTime.UtcNow:O} {referencia} {error.GetType().FullName}: {error.Message}{Environment.NewLine}{error}{Environment.NewLine}");
        }
        catch (Exception fallo) when (fallo is IOException or UnauthorizedAccessException) { }
        return referencia;
    }
}
