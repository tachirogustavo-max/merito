using System.IO.Compression;
using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Entidades;
using Logica;

namespace Servidor;

// Lee la plantilla de Excel tal como está; la validación de cada fila la hace la capa de lógica.
public static class LectorMatricula
{
    private static readonly string[] Encabezados = ["cedula", "nombres", "apellidos", "carrera", "seccion"];

    // Correo y materias son opcionales y pueden ir en cualquier lugar: los archivos de antes, con cinco columnas, siguen sirviendo.
    public static List<FilaCruda> Leer(MemoryStream archivo, int maximo = ReglasAcademicas.MaximoInscritos) =>
        LectorExcel.Filas(archivo, Encabezados, "cédula, nombres, apellidos, carrera y sección (y correo y materias, si los hay)", maximo, ["correo", "materias"])
            .Select(f => new FilaCruda(f.Numero, f.Celdas[0], f.Celdas[1], f.Celdas[2], f.Celdas[3], f.Celdas[4], f.TieneFormula,
                f.Celdas[5], f.Celdas[6])).ToList();

    public static byte[] Plantilla() => LectorExcel.Plantilla("matricula", "PLANTILLA DE MATRÍCULA",
        "Una fila por estudiante y sección. Cédula con o sin V-, sin puntos. Carrera y sección tal como están en Mérito. Con el correo, el estudiante crea su acceso solo. " +
        "Materias vacío: todas las de la sección; si cursa solo algunas, sus códigos separados por coma. Quien arrastra una materia va en otra fila con esa sección y ese código.",
        [("cédula", 16), ("nombres", 24), ("apellidos", 24), ("carrera", 30), ("sección", 14), ("correo", 34), ("materias", 26)], textoEn: [1]);
}

public static class LectorNomina
{
    private static readonly string[] Encabezados = ["cedula", "nombres", "apellidos"];

    public static List<(int Numero, FilaNomina Fila, bool TieneFormula)> Leer(MemoryStream archivo) =>
        LectorExcel.Filas(archivo, Encabezados, "cédula, nombres y apellidos (y correo, si lo hay)", ServicioIdentidad.MaximoNomina, ["correo"])
            .Select(f => (f.Numero, new FilaNomina(f.Celdas[0], f.Celdas[1], f.Celdas[2], f.Celdas[3]), f.TieneFormula)).ToList();

    public static byte[] Plantilla() => LectorExcel.Plantilla("nomina", "NÓMINA DOCENTE",
        "Una fila por docente. Cédula con o sin V-, sin puntos. Con el correo, el docente crea su acceso solo desde «¿Primera vez?».",
        [("cédula", 16), ("nombres", 26), ("apellidos", 26), ("correo", 36)], textoEn: [1]);
}

public static class LectorPensum
{
    private static readonly string[] Encabezados = ["codigo", "materia", "uc", "semestre"];

    public static List<FilaCrudaPensum> Leer(MemoryStream archivo) =>
        LectorExcel.Filas(archivo, Encabezados, "código, materia, UC y semestre", Pensum.MaximoFilas)
            .Select(f => new FilaCrudaPensum(f.Numero, f.Celdas[0], f.Celdas[1], f.Celdas[2], f.Celdas[3], f.TieneFormula)).ToList();

    public static byte[] Plantilla() => LectorExcel.Plantilla("pensum", "PLANTILLA DE PENSUM",
        "Una fila por materia de la carrera. Código como en el pensum oficial; si la materia ya existe en otra carrera, usa el mismo código. Semestre vacío o 0 para electivas.",
        [("código", 16), ("materia", 44), ("uc", 8), ("semestre", 11)], textoEn: [1]);
}

internal static class LectorExcel
{
    // Celdas: primero las obligatorias y luego las opcionales, en el orden pedido; una opcional ausente llega vacía.
    public sealed record Fila(int Numero, string[] Celdas, bool TieneFormula);

    // Títulos que se aceptan para cada columna, ya sin tildes ni mayúsculas.
    private static readonly Dictionary<string, string[]> Equivalentes = new()
    {
        ["cedula"] = ["cedula", "cedula de identidad", "ci", "c.i."],
        ["nombres"] = ["nombres", "nombre"],
        ["apellidos"] = ["apellidos", "apellido"],
        ["carrera"] = ["carrera"],
        ["seccion"] = ["seccion", "codigo de seccion"],
        ["correo"] = ["correo", "correo electronico", "email", "e-mail"],
        ["materias"] = ["materias", "codigos de materias"],
        ["codigo"] = ["codigo", "codigo de materia"],
        ["materia"] = ["materia", "asignatura", "nombre de la materia"],
        ["uc"] = ["uc", "u.c.", "unidades de credito"],
        ["semestre"] = ["semestre"],
    };

    private static readonly Dictionary<string, string> Visibles = new()
    {
        ["cedula"] = "cédula", ["nombres"] = "nombres", ["apellidos"] = "apellidos", ["carrera"] = "carrera", ["seccion"] = "sección",
        ["correo"] = "correo", ["materias"] = "materias", ["codigo"] = "código", ["materia"] = "materia", ["uc"] = "UC", ["semestre"] = "semestre",
    };

    /// <summary>
    /// Busca la fila de títulos en las primeras 10 filas y cada columna por su título, en cualquier orden.
    /// Faltan obligatorias o hay una repetida: se rechaza el archivo diciendo cuál. Columnas que no se piden: se ignoran.
    /// </summary>
    public static List<Fila> Filas(MemoryStream archivo, string[] encabezados, string descripcion, int maximo, string[]? opcionales = null)
    {
        try
        {
            archivo.Position = 0;
            using (var zip = new ZipArchive(archivo, ZipArchiveMode.Read, true))
                if (zip.Entries.Count > 300 || zip.Entries.Sum(e => e.Length) > 30 * 1024 * 1024)
                    throw new ReglaException("El archivo expandido supera el límite permitido.");
            archivo.Position = 0;
            using var libro = new XLWorkbook(archivo);
            if (libro.Worksheets.Count != 1) throw new ReglaException("El archivo debe tener una sola hoja. Borra las demás hojas o copia los datos a la plantilla.");
            var hoja = libro.Worksheet(1);
            var buscadas = encabezados.Concat(opcionales ?? []).ToArray();
            var ultimaColumna = Math.Min(hoja.LastColumnUsed()?.ColumnNumber() ?? 0, 60);

            // Qué columna corresponde a cada título en una fila; null si un título se repite.
            (Dictionary<string, int> Columnas, string? Repetida) Leer(int fila)
            {
                var columnas = new Dictionary<string, int>();
                for (var c = 1; c <= ultimaColumna; c++)
                {
                    var texto = Normalizar(hoja.Cell(fila, c).GetString());
                    var nombre = buscadas.FirstOrDefault(b => Equivalentes.GetValueOrDefault(b, [b]).Contains(texto));
                    if (nombre is null) continue;
                    if (!columnas.TryAdd(nombre, c)) return (columnas, nombre);
                }
                return (columnas, null);
            }
            var candidatas = Enumerable.Range(1, 10).Select(f => (Fila: f, Lectura: Leer(f))).ToList();
            var (titulos, (mapa, repetida)) = candidatas.OrderByDescending(c => c.Lectura.Columnas.Keys.Count(encabezados.Contains)).First();
            var faltan = encabezados.Where(e => !mapa.ContainsKey(e)).ToList();
            if (faltan.Count == encabezados.Length)
                throw new ReglaException($"No se encontró la fila de títulos. Las columnas deben llamarse: {descripcion}, en una de las primeras 10 filas.");
            if (faltan.Count > 0)
                throw new ReglaException($"Faltan columnas: {string.Join(", ", faltan.Select(f => Visibles.GetValueOrDefault(f, f)))}. Revisa los títulos de la fila {titulos}.");
            if (repetida is not null) throw new ReglaException($"La columna «{Visibles.GetValueOrDefault(repetida, repetida)}» está dos veces en la fila de títulos.");

            var ultimaFila = Math.Min(hoja.LastRowUsed()?.RowNumber() ?? 0, titulos + maximo + 1);
            var filas = new List<Fila>();
            for (var numero = titulos + 1; numero <= ultimaFila; numero++)
            {
                var celdas = buscadas.Select(b => mapa.TryGetValue(b, out var c) ? hoja.Cell(numero, c) : null).ToArray();
                if (celdas.All(c => c is null || c.IsEmpty())) continue;
                filas.Add(new Fila(numero, celdas.Select(c => c?.GetFormattedString() ?? "").ToArray(), celdas.Any(c => c is { HasFormula: true })));
            }
            return filas;
        }
        catch (ReglaException) { throw; }
        catch (Exception error) when (error is InvalidDataException or ArgumentException or FormatException or IOException or InvalidOperationException)
        { throw new ReglaException("No se pudo leer el archivo. Usa una plantilla .xlsx válida y sin contraseña."); }
    }

    public static byte[] Plantilla(string nombreHoja, string titulo, string indicacion, (string Titulo, double Ancho)[] columnas, int[] textoEn)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet(nombreHoja);
        hoja.ShowGridLines = false;
        hoja.Style.Font.FontName = "Segoe UI";
        hoja.Style.Font.FontSize = 10;
        var indigo = XLColor.FromHtml("#4343D0");
        var gris = XLColor.FromHtml("#6B718C");
        var linea = XLColor.FromHtml("#E2E5EE");
        var ultima = columnas.Length;

        hoja.Row(1).Height = 44;
        using (var logo = typeof(LectorExcel).Assembly.GetManifestResourceStream("logotipo.png")!)
            hoja.AddPicture(logo).MoveTo(hoja.Cell(1, 1), 2, 8).WithSize(139, 42);
        hoja.Cell(1, ultima).Value = titulo;
        hoja.Cell(1, ultima).Style.Font.SetBold().Font.SetFontColor(indigo).Font.SetFontSize(9)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right).Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        hoja.Cell(2, 1).Value = indicacion;
        hoja.Cell(2, 1).Style.Font.SetFontColor(gris).Font.SetFontSize(9).Alignment.SetWrapText(true);
        hoja.Range(2, 1, 2, ultima).Merge();
        hoja.Row(2).Height = indicacion.Length > 110 ? 28 : 15;
        hoja.Row(3).Height = 8;

        const int fila = 4;
        for (var i = 0; i < columnas.Length; i++) hoja.Cell(fila, i + 1).Value = columnas[i].Titulo;
        var cabecera = hoja.Range(fila, 1, fila, ultima);
        cabecera.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(indigo)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        hoja.Row(fila).Height = 22;
        var cuerpo = hoja.Range(fila + 1, 1, fila + 200, ultima);
        cuerpo.Style.Border.SetBottomBorder(XLBorderStyleValues.Thin).Border.SetBottomBorderColor(linea);
        foreach (var columna in textoEn) hoja.Column(columna).Style.NumberFormat.Format = "@";
        for (var i = 0; i < columnas.Length; i++) hoja.Column(i + 1).Width = columnas[i].Ancho;
        hoja.SheetView.FreezeRows(fila);
        using var salida = new MemoryStream();
        libro.SaveAs(salida);
        return salida.ToArray();
    }

    private static string Normalizar(string texto) => new(texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
}
