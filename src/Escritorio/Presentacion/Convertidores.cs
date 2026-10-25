using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Escritorio.Presentacion;

public enum Tono { Neutro, Exito, Aviso, Peligro, Marca }

public static class Textos
{
    public static string Capitalizar(string? texto) =>
        string.IsNullOrEmpty(texto) ? "" : char.ToUpper(texto[0], CultureInfo.CurrentCulture) + texto[1..];

    /// <summary>Una nota entera se muestra sin decimales; la que conserva 0,50 se muestra con dos.</summary>
    public static string Nota(decimal valor) => valor == decimal.Truncate(valor)
        ? valor.ToString("0", CultureInfo.CurrentCulture) : valor.ToString("0.00", CultureInfo.CurrentCulture);

    public static string Iniciales(string nombre)
    {
        var partes = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return partes.Length switch
        {
            0 => "?",
            1 => partes[0][..1].ToUpper(CultureInfo.CurrentCulture),
            _ => (partes[0][..1] + partes[^1][..1]).ToUpper(CultureInfo.CurrentCulture)
        };
    }

    public static Tono Clasificar(object? valor, string? campo) => valor switch
    {
        bool activo when campo == "Activo" => activo ? Tono.Exito : Tono.Neutro,
        bool cerrado when campo == "Cerrado" => cerrado ? Tono.Neutro : Tono.Exito,
        "publicado" or "confirmada" or "aprobado" or "activa" => Tono.Exito,
        "aplazado" => Tono.Peligro,
        "pendiente" or "cambios sin publicar" or "sin registrar" => Tono.Aviso,
        "cerrado" or "sin publicar" or "inactiva" => Tono.Neutro,
        _ => Tono.Marca
    };

    public static string Etiqueta(object? valor, string? campo) => valor switch
    {
        bool activo when campo == "Activo" => activo ? "Activa" : "Inactiva",
        bool cerrado when campo == "Cerrado" => cerrado ? "Cerrado" : "Abierto",
        bool otro => otro ? "Sí" : "No",
        string texto => Capitalizar(texto),
        _ => valor?.ToString() ?? ""
    };

    public static (Brush Fondo, Brush Texto) Pinceles(Tono tono)
    {
        var app = Application.Current;
        Brush Recurso(string clave) => (Brush)app.FindResource(clave);
        return tono switch
        {
            Tono.Exito => (Recurso("ExitoSuave"), Recurso("Exito")),
            Tono.Aviso => (Recurso("AvisoSuave"), Recurso("Aviso")),
            Tono.Peligro => (Recurso("ErrorSuave"), Recurso("Error")),
            Tono.Marca => (Recurso("Indigo50"), Recurso("Indigo700")),
            _ => (Recurso("NeutroSuave"), Recurso("Texto2"))
        };
    }
}

public sealed class ConvertidorInsignia : IValueConverter
{
    public string Modo { get; set; } = "etiqueta";
    public string? Campo { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (Modo == "etiqueta") return Textos.Etiqueta(value, Campo);
        var (fondo, texto) = Textos.Pinceles(Textos.Clasificar(value, Campo));
        return Modo == "fondo" ? fondo : texto;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class ConvertidorCapital : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Textos.Capitalizar(value?.ToString());
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class ConvertidorCedula : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Entidades.Cedulas.Mostrar(value as string);
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class ConvertidorCuenta : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is 1 ? "1 registro" : $"{value} registros";
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class ConvertidorFecha : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateTime fecha ? (fecha.Kind == DateTimeKind.Local ? fecha : fecha.ToLocalTime()).ToString("dd/MM/yyyy HH:mm", culture) : "";
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
