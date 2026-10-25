using System.Globalization;
using System.Windows;

namespace Escritorio.Presentacion;

/// <summary>
/// El área de trabajo de la pantalla. Las pruebas pueden simular una pantalla pequeña, por ejemplo
/// MERITO_AREA=1024x560, sin tocar la resolución del equipo.
/// </summary>
internal static class Pantalla
{
    public static Size? Simulada
    {
        get
        {
            var partes = (Environment.GetEnvironmentVariable("MERITO_AREA") ?? "").Split('x');
            return partes.Length == 2 && double.TryParse(partes[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ancho)
                && double.TryParse(partes[1], NumberStyles.None, CultureInfo.InvariantCulture, out var alto) ? new Size(ancho, alto) : null;
        }
    }

    public static Rect Area => Simulada is { } tamano ? new Rect(0, 0, tamano.Width, tamano.Height) : SystemParameters.WorkArea;
}