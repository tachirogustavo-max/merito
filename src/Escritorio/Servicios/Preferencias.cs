using System.IO;
using System.Text.Json;

namespace Escritorio.Servicios;

/// <summary>Preferencias de este equipo. Nunca guarda contraseñas ni sesiones.</summary>
public sealed record Preferencias(string? Servidor = null, string? Cedula = null, bool RecordarCedula = true)
{
    private static string Ruta => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Merito", "preferencias.json");

    /// <summary>Dirección que dejó el instalador para todos los usuarios del equipo.</summary>
    public static string? ServidorDelEquipo()
    {
        var ruta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Merito", "cliente.json");
        try { return File.Exists(ruta) ? JsonDocument.Parse(File.ReadAllText(ruta)).RootElement.GetProperty("Servidor").GetString() : null; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException) { return null; }
    }

    public static Preferencias Cargar()
    {
        try { return File.Exists(Ruta) ? JsonSerializer.Deserialize<Preferencias>(File.ReadAllText(Ruta)) ?? new() : new(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public void Guardar()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Ruta)!);
            var temporal = Ruta + ".tmp";
            File.WriteAllText(temporal, JsonSerializer.Serialize(this));
            File.Move(temporal, Ruta, true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
}
