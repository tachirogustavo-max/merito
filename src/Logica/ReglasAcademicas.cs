using System.Globalization;
using System.Text.RegularExpressions;
using Entidades;

namespace Logica;

public static partial class ReglasAcademicas
{
    // Escala, nota aprobatoria, redondeo e índice son configurables: ver Calificacion.
    public const int MaximoEvaluaciones = 30;
    public const int MaximoInscritos = 2000;

    public static string Texto(string? valor, string campo, int maximo = 120)
    {
        var limpio = valor?.Trim() ?? "";
        if (limpio.Length == 0 || limpio.Length > maximo || limpio.Any(char.IsControl))
            throw new ReglaException($"{campo}: escribe entre 1 y {maximo} caracteres válidos.");
        return limpio;
    }

    public static string Cedula(string? valor)
    {
        var cedula = (valor ?? "").Trim().ToUpperInvariant().Replace(" ", "").Replace(".", "");
        // Solo cédulas venezolanas: se guardan los números, la «V-» se agrega al mostrarlas.
        if (!PatronCedula().IsMatch(cedula))
            throw new ReglaException("Escribe la cédula con 5 a 10 números; puede llevar V- delante.");
        return cedula.TrimStart('V').TrimStart('-');
    }

    /// <summary>Correo en minúsculas y sin espacios; vacío es «sin correo».</summary>
    public static string? Correo(string? valor)
    {
        var correo = (valor ?? "").Trim().ToLowerInvariant();
        if (correo.Length == 0) return null;
        if (correo.Length > 254 || !PatronCorreo().IsMatch(correo)) throw new ReglaException($"«{correo}» no parece un correo válido.");
        return correo;
    }

    public static void ValidarPeso(decimal peso)
    {
        if (peso <= 0 || peso > 100 || decimal.Round(peso, 2) != peso)
            throw new ReglaException("La ponderación debe estar entre 0,01 % y 100 %, con hasta dos decimales.");
    }

    public static void ValidarPlan(decimal pesoActual, decimal pesoNuevo)
    {
        if (pesoActual + pesoNuevo > 100) throw new ReglaException("Con esta evaluación la ponderación pasa de 100 %.");
    }

    /// <summary>Una lista que llega de afuera: no nula, con un tamaño razonable y sin elementos vacíos.</summary>
    public static List<T> Lista<T>(List<T>? lista, int minimo, int maximo, string mensaje) where T : class =>
        lista is { Count: var n } && n >= minimo && n <= maximo && lista.All(e => e is not null) ? lista : throw new ReglaException(mensaje);

    public static bool MismoTexto(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    public static decimal LeerDecimal(string valor) => decimal.TryParse(valor.Replace(',', '.'),
        NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var numero)
        ? numero : throw new ReglaException("Escribe un número válido.");

    // Usuario de hasta 64 caracteres sin puntos al borde ni seguidos; dominio con partes de letras, números y guiones
    // (sin guion al borde) y terminación de solo letras. Deja fuera comas, espacios, «..» y dominios sin punto.
    [GeneratedRegex(@"^(?!\.)(?!.*\.\.)[a-z0-9.!#$%&'*+/=?^_`{|}~-]{1,64}(?<!\.)@(?=.{4,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex PatronCorreo();

    [GeneratedRegex(@"^(?:V-?)?[0-9]{5,10}$", RegexOptions.CultureInvariant)]
    private static partial Regex PatronCedula();
}
