using System.Text.RegularExpressions;

namespace Logica;

/// <summary>
/// Formato con el que se arma el código de cada sección. Lo define administración según el que use la universidad.
/// Marcadores: {seccion} el identificador (obligatorio), {carrera} el código de la carrera,
/// {semestre} el número de semestre y {semestre:00} el mismo número con ceros a la izquierda.
/// Todo lo demás se copia tal cual. Ejemplo: «{semestre:00}S-{carrera}-{seccion}» da 02S-2603-D1.
/// </summary>
public static partial class FormatoSeccion
{
    public const string PorDefecto = "{seccion}";
    public const int LargoFormato = 60;
    public const int LargoCodigo = 60;
    public const int SemestreMaximo = 20;

    [GeneratedRegex(@"\{(?<nombre>[a-z]+)(?::(?<ceros>0{1,3}))?\}", RegexOptions.CultureInvariant)]
    private static partial Regex Marcador();

    public static string Validar(string? formato)
    {
        var texto = (formato ?? "").Trim();
        if (texto.Length == 0 || texto.Length > LargoFormato || texto.Any(char.IsControl))
            throw new ReglaException($"El formato debe tener entre 1 y {LargoFormato} caracteres.");
        foreach (Match marcador in Marcador().Matches(texto))
        {
            var nombre = marcador.Groups["nombre"].Value;
            if (nombre is not ("seccion" or "carrera" or "semestre"))
                throw new ReglaException($"«{marcador.Value}» no es un marcador válido. Usa {{seccion}}, {{carrera}}, {{semestre}} o {{semestre:00}}.");
            if (marcador.Groups["ceros"].Success && nombre != "semestre")
                throw new ReglaException("Solo {semestre} admite ceros a la izquierda, como {semestre:00}.");
        }
        // Una llave suelta casi siempre es un marcador mal escrito.
        if (Marcador().Replace(texto, "").IndexOfAny(['{', '}']) >= 0)
            throw new ReglaException("Hay una llave { o } que no forma un marcador válido.");
        if (!texto.Contains("{seccion}", StringComparison.Ordinal))
            throw new ReglaException("El formato debe incluir {seccion}, que distingue cada sección.");
        return texto;
    }

    public static bool UsaCarrera(string formato) => Marcador().Matches(formato).Any(m => m.Groups["nombre"].Value == "carrera");
    public static bool UsaSemestre(string formato) => Marcador().Matches(formato).Any(m => m.Groups["nombre"].Value == "semestre");

    /// <summary>Arma el código; falla con un mensaje claro si falta el dato que el formato pide.</summary>
    public static string Generar(string formato, string carrera, string? codigoCarrera, int semestre, string seccion)
    {
        if (UsaCarrera(formato) && string.IsNullOrWhiteSpace(codigoCarrera))
            throw new ReglaException($"La carrera {carrera} no tiene código y el formato de sección lo usa. Agrégalo en Estructura › Carreras.");
        if (UsaSemestre(formato) && semestre == 0)
            throw new ReglaException($"La sección {seccion} de {carrera} no tiene semestre y el formato de sección lo usa.");
        var codigo = Marcador().Replace(formato, m => m.Groups["nombre"].Value switch
        {
            "seccion" => seccion,
            "carrera" => codigoCarrera!,
            _ => m.Groups["ceros"].Success ? semestre.ToString(new string('0', m.Groups["ceros"].Value.Length)) : semestre.ToString()
        });
        if (codigo.Length > LargoCodigo) throw new ReglaException($"El código {codigo} pasa de {LargoCodigo} caracteres.");
        return codigo;
    }

    public static int Semestre(int semestre) => semestre is >= 0 and <= SemestreMaximo ? semestre
        : throw new ReglaException($"El semestre va de 1 a {SemestreMaximo}; deja 0 si la sección no tiene semestre.");

    // Letras, números y guiones: así el código de carrera se puede escribir dentro del código de sección.
    public static string? CodigoCarrera(string? codigo)
    {
        var texto = (codigo ?? "").Trim().ToUpperInvariant();
        if (texto.Length == 0) return null;
        if (texto.Length > 20 || !texto.All(c => char.IsLetterOrDigit(c) || c is '-' or '.' or '_'))
            throw new ReglaException("El código de la carrera admite hasta 20 letras, números, puntos o guiones.");
        return texto;
    }

    public static string Identificador(string? seccion) => ReglasAcademicas.Texto(seccion, "Sección", 20);
}
