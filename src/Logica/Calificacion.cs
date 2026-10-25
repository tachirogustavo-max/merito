using System.Globalization;
using Entidades;

namespace Logica;

/// <summary>
/// Cálculo de notas con las reglas que configura la institución. Nada de la escala, la nota aprobatoria,
/// el redondeo o el índice está fijo en el código: lo que Mérito trae de fábrica vive en la migración 0006.
/// </summary>
public static class Calificacion
{
    public const decimal TopeEscala = 100m;
    public static readonly string[] Claves = ["escala_minima", "escala_maxima", "nota_aprobatoria", "redondeo", "indice", "indice_aplazadas"];

    public static ReglasCalificacion Leer(IReadOnlyDictionary<string, string> valores)
    {
        decimal Numero(string clave) => decimal.Parse(valores[clave], NumberStyles.Number, CultureInfo.InvariantCulture);
        return new ReglasCalificacion(Numero("escala_minima"), Numero("escala_maxima"), Numero("nota_aprobatoria"),
            valores["redondeo"], valores["indice"], valores["indice_aplazadas"] == "si");
    }

    public static Dictionary<string, string> Escribir(ReglasCalificacion r) => new()
    {
        ["escala_minima"] = r.EscalaMinima.ToString("0.##", CultureInfo.InvariantCulture),
        ["escala_maxima"] = r.EscalaMaxima.ToString("0.##", CultureInfo.InvariantCulture),
        ["nota_aprobatoria"] = r.NotaAprobatoria.ToString("0.##", CultureInfo.InvariantCulture),
        ["redondeo"] = r.Redondeo,
        ["indice"] = r.Indice,
        ["indice_aplazadas"] = r.IndiceConAplazadas ? "si" : "no"
    };

    public static ReglasCalificacion Validar(ReglasCalificacion? r)
    {
        if (r is null) throw new ReglaException("Faltan las reglas de calificación.");
        if (r.EscalaMinima < 0 || r.EscalaMaxima > TopeEscala || r.EscalaMinima >= r.EscalaMaxima
            || decimal.Truncate(r.EscalaMinima) != r.EscalaMinima || decimal.Truncate(r.EscalaMaxima) != r.EscalaMaxima)
            throw new ReglaException($"La escala va de un número entero a otro mayor, entre 0 y {TopeEscala:0}; por ejemplo de 1 a 20.");
        ValidarAprobatoria(r.NotaAprobatoria, r);
        if (r.Redondeo is not (Redondeos.Media or Redondeos.Entero or Redondeos.Ninguno)) throw new ReglaException("Elige cómo se redondea la definitiva.");
        if (r.Indice is not (Indices.Ponderado or Indices.Simple)) throw new ReglaException("Elige cómo se calcula el índice.");
        return r;
    }

    public static decimal ValidarAprobatoria(decimal nota, ReglasCalificacion r)
    {
        if (nota <= r.EscalaMinima || nota > r.EscalaMaxima || decimal.Round(nota, 2) != nota)
            throw new ReglaException($"La nota aprobatoria debe ser mayor que {r.EscalaMinima:0.##} y no pasar de {r.EscalaMaxima:0.##}, con hasta dos decimales.");
        return nota;
    }

    public static void ValidarNota(decimal? nota, ReglasCalificacion r)
    {
        if (nota is { } n && (n < r.EscalaMinima || n > r.EscalaMaxima || decimal.Round(n, 2) != n))
            throw new ReglaException($"Las notas deben estar entre {r.EscalaMinima:0.##} y {r.EscalaMaxima:0.##}, con un máximo de dos decimales.");
    }

    public static decimal Aporte(decimal nota, decimal peso) => nota * peso / 100m;
    public static decimal PorcentajeObtenido(decimal nota, decimal peso, decimal escalaMaxima) => nota * peso / escalaMaxima;

    /// <summary>
    /// «media»: menos de 0,50 se descarta, 0,50 exacto se conserva y más de 0,50 sube al entero siguiente.
    /// «entero»: desde 0,50 sube. «ninguno»: se deja con dos decimales.
    /// </summary>
    public static decimal Redondear(decimal valor, string modo)
    {
        if (modo == Redondeos.Ninguno) return decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
        if (modo == Redondeos.Entero) return decimal.Round(valor, 0, MidpointRounding.AwayFromZero);
        var entero = decimal.Truncate(valor);
        var fraccion = valor - entero;
        return fraccion switch { > 0.50m => entero + 1, 0.50m => entero + 0.50m, _ => entero };
    }

    public static string Condicion(decimal notaFinal, decimal aprobatoria) => notaFinal >= aprobatoria ? Condiciones.Aprobado : Condiciones.Aplazado;

    /// <summary>Acumulado y definitiva de una materia; la definitiva solo existe con el plan completo calificado.</summary>
    public static (decimal Acumulado, decimal? Final) Calcular(IReadOnlyList<(decimal Peso, decimal? Nota)> evaluaciones, string redondeo)
    {
        var acumulado = evaluaciones.Where(e => e.Nota.HasValue).Sum(e => Aporte(e.Nota!.Value, e.Peso));
        var completa = evaluaciones.Count > 0 && evaluaciones.Sum(e => e.Peso) == 100m && evaluaciones.All(e => e.Nota.HasValue);
        return (acumulado, completa ? Redondear(acumulado, redondeo) : null);
    }

    /// <summary>Índice con las definitivas dadas. Ponderado: suma de definitiva por UC entre la suma de UC; las de 0 UC no pesan.</summary>
    public static decimal? Indice(IEnumerable<(decimal Final, int Uc, bool Aprobada)> materias, ReglasCalificacion r)
    {
        var cuentan = materias.Where(m => r.IndiceConAplazadas || m.Aprobada).ToList();
        if (r.Indice == Indices.Simple) return cuentan.Count == 0 ? null : decimal.Round(cuentan.Average(m => m.Final), 2, MidpointRounding.AwayFromZero);
        var uc = cuentan.Sum(m => m.Uc);
        return uc == 0 ? null : decimal.Round(cuentan.Sum(m => m.Final * m.Uc) / uc, 2, MidpointRounding.AwayFromZero);
    }
}
