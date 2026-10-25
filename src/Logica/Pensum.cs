using System.Globalization;
using System.Text.RegularExpressions;
using Entidades;

namespace Logica;

public sealed record FilaCrudaPensum(int Numero, string Codigo, string Nombre, string Uc, string Semestre, bool TieneFormula);

/// <summary>
/// Reglas del pensum. Una materia la identifica su código: si el Excel de una carrera trae un código que ya existe,
/// es la misma materia (el ciclo básico de varias ingenierías) y solo se agrega a ese pensum.
/// </summary>
public static partial class Pensum
{
    public const int MaximoFilas = 400;
    public const int MaximoUc = 60;

    public static string Codigo(string? valor)
    {
        var codigo = (valor ?? "").Trim().ToUpperInvariant();
        if (!PatronCodigo().IsMatch(codigo))
            throw new ReglaException("El código de la materia lleva de 1 a 20 letras, números, puntos o guiones, por ejemplo MAT-21114.");
        return codigo;
    }

    public static int Uc(int uc) => uc is >= 0 and <= MaximoUc ? uc
        : throw new ReglaException($"Las unidades de crédito van de 0 a {MaximoUc}.");

    public static int Semestre(int semestre) => semestre is >= 0 and <= 20 ? semestre
        : throw new ReglaException("El semestre va de 1 a 20; 0 si es electiva o no tiene semestre.");

    public static NuevaMateria Normalizar(NuevaMateria materia) => new(Codigo(materia.Codigo),
        ReglasAcademicas.Texto(materia.Nombre, "Materia", 120), Uc(materia.Uc),
        materia.NotaAprobatoria is { } n ? decimal.Round(n, 2) == n ? n : throw new ReglaException("La nota aprobatoria lleva hasta dos decimales.") : null);

    private static int Entero(string texto, string campo, bool vacioEsCero)
    {
        var limpio = texto.Trim();
        if (limpio.Length == 0 && vacioEsCero) return 0;
        return int.TryParse(limpio, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n
            : throw new ReglaException($"{campo}: escribe un número entero.");
    }

    /// <summary>Valida el formato de cada fila del Excel por separado para informar todos los problemas de una vez.</summary>
    public static (List<(int Numero, FilaPensum Fila)> Filas, List<ErrorFila> Errores) Revisar(IReadOnlyList<FilaCrudaPensum> crudas)
    {
        if (crudas.Count is < 1 or > MaximoFilas) throw new ReglaException($"La hoja debe tener entre 1 y {MaximoFilas} materias.");
        var filas = new List<(int, FilaPensum)>();
        var errores = new List<ErrorFila>();
        var codigos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cruda in crudas)
        {
            try
            {
                if (cruda.TieneFormula) throw new ReglaException("No se admiten fórmulas en el pensum.");
                var fila = Normalizar(new FilaPensum(cruda.Codigo, cruda.Nombre, Entero(cruda.Uc, "UC", false), Entero(cruda.Semestre, "Semestre", true)));
                if (!codigos.Add(fila.Codigo)) throw new ReglaException($"El código {fila.Codigo} está repetido en el archivo.");
                filas.Add((cruda.Numero, fila));
            }
            catch (ReglaException error) { errores.Add(new ErrorFila(cruda.Numero, error.Message)); }
        }
        return (filas, errores);
    }

    public static FilaPensum Normalizar(FilaPensum fila) => new(Codigo(fila.Codigo), ReglasAcademicas.Texto(fila.Nombre, "Materia", 120),
        Uc(fila.Uc), Semestre(fila.Semestre));

    /// <summary>
    /// Compara el archivo con lo guardado. Un código existente debe traer el mismo nombre y las mismas UC:
    /// si no, el código es de otra materia, o la materia cambió y se corrige en el pensum, no al importar.
    /// </summary>
    public static (List<FilaPensum> Plan, List<ErrorFila> Errores) Planear(IReadOnlyList<(int Numero, FilaPensum Fila)> filas,
        IReadOnlyDictionary<string, Materia> porCodigo, IReadOnlyDictionary<long, int> enPensum)
    {
        var plan = new List<FilaPensum>();
        var errores = new List<ErrorFila>();
        foreach (var (numero, fila) in filas)
        {
            if (!porCodigo.TryGetValue(fila.Codigo, out var materia)) { plan.Add(fila with { Accion = "Materia nueva" }); continue; }
            if (!ReglasAcademicas.MismoTexto(materia.Nombre, fila.Nombre))
            { errores.Add(new ErrorFila(numero, $"El código {fila.Codigo} ya es de «{materia.Nombre}». Usa ese nombre o revisa el código.")); continue; }
            if (materia.Uc != fila.Uc)
            { errores.Add(new ErrorFila(numero, $"{fila.Codigo} tiene {materia.Uc} UC en Mérito y el archivo dice {fila.Uc}. Si cambió, corrígela en el pensum antes de importar.")); continue; }
            var accion = !enPensum.TryGetValue(materia.Id, out var semestre)
                ? materia.Carreras.Length > 0 ? "Se agrega (ya está en otras carreras)" : "Se agrega"
                : semestre == fila.Semestre ? "Sin cambios" : $"Pasa del semestre {semestre} al {fila.Semestre}";
            plan.Add(fila with { Nombre = materia.Nombre, Accion = accion });
        }
        return (plan, errores);
    }

    [GeneratedRegex(@"^[A-Z0-9][A-Z0-9.\-_]{0,19}$", RegexOptions.CultureInvariant)]
    private static partial Regex PatronCodigo();
}
