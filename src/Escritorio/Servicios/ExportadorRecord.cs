using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using Entidades;
using MigraDoc.DocumentObjectModel;
using Celda = MigraDoc.DocumentObjectModel.Tables.Cell;
using MigraDoc.Rendering;
using Documento = MigraDoc.DocumentObjectModel.Document;
using Parrafo = System.Windows.Documents.Paragraph;
using Medios = System.Windows.Media;

namespace Escritorio.Servicios;

public static class ExportadorRecord
{
    // Colores de la aplicación.
    static readonly Color Indigo = Color.FromRgb(0x43, 0x43, 0xD0);
    static readonly Color Tinta = Color.FromRgb(0x17, 0x1A, 0x2C);
    static readonly Color Gris = Color.FromRgb(0x6B, 0x71, 0x8C);
    static readonly Color Linea = Color.FromRgb(0xE2, 0xE5, 0xEE);
    static readonly Color Exito = Color.FromRgb(0x13, 0x79, 0x5B);
    static readonly Color Peligro = Color.FromRgb(0xBE, 0x31, 0x44);
    const string Firma = "Realizado en Mérito, sistema de registro académico";
    const double Ancho = 17.4; // cm útiles en A4 con márgenes de 1,8

    static byte[]? logotipo;
    internal static byte[] Logotipo()
    {
        if (logotipo != null) return logotipo;
        using var recurso = typeof(ExportadorRecord).Assembly.GetManifestResourceStream("logotipo.png")!;
        using var memoria = new MemoryStream();
        recurso.CopyTo(memoria);
        return logotipo = memoria.ToArray();
    }

    public static void Pdf(RecordAcademico record, string destino)
    {
        var ahora = DateTime.Now;
        var documento = new Documento();
        documento.Info.Title = $"Récord académico de {record.Estudiante.NombreCompleto}";
        documento.Info.Author = "Mérito";
        var normal = documento.Styles[StyleNames.Normal]!;
        normal.Font.Name = "Segoe UI"; normal.Font.Size = 9.5; normal.Font.Color = Tinta;
        normal.ParagraphFormat.SpaceAfter = 0;

        var seccion = documento.AddSection();
        var pagina = seccion.PageSetup;
        pagina.PageFormat = PageFormat.A4;
        pagina.TopMargin = "2.6cm"; pagina.BottomMargin = "2cm";
        pagina.LeftMargin = pagina.RightMargin = "1.8cm";
        pagina.HeaderDistance = "1.2cm"; pagina.FooterDistance = "1cm";
        pagina.DifferentFirstPageHeaderFooter = true;

        // Primera página: logotipo y datos del documento. Las siguientes: una línea discreta.
        var cabeza = seccion.Headers.FirstPage.AddTable();
        cabeza.AddColumn(Unit.FromCentimeter(8.7));
        cabeza.AddColumn(Unit.FromCentimeter(Ancho - 8.7));
        var filaCabeza = cabeza.AddRow();
        filaCabeza.VerticalAlignment = MigraDoc.DocumentObjectModel.Tables.VerticalAlignment.Center;
        var logo = filaCabeza.Cells[0].AddImage("base64:" + Convert.ToBase64String(Logotipo()));
        logo.Height = "0.95cm"; logo.LockAspectRatio = true;
        var meta = filaCabeza.Cells[1].AddParagraph("RÉCORD ACADÉMICO");
        meta.Format.Alignment = ParagraphAlignment.Right;
        meta.Format.Font.Size = 8; meta.Format.Font.Bold = true; meta.Format.Font.Color = Indigo;
        var emitido = filaCabeza.Cells[1].AddParagraph($"Emitido el {ahora.ToString("d 'de' MMMM 'de' yyyy", Espanol)}, {ahora:HH:mm}");
        emitido.Format.Alignment = ParagraphAlignment.Right;
        emitido.Format.Font.Size = 8; emitido.Format.Font.Color = Gris; emitido.Format.SpaceBefore = "1pt";
        var otras = seccion.Headers.Primary.AddParagraph();
        otras.Format.Font.Size = 8; otras.Format.Font.Color = Gris;
        otras.Format.TabStops.AddTabStop(Unit.FromCentimeter(Ancho), TabAlignment.Right);
        otras.AddText($"Récord académico de {record.Estudiante.NombreCompleto}");
        otras.AddTab();
        otras.AddText($"Cédula {Cedulas.Mostrar(record.Estudiante.Cedula)}");
        otras.Format.Borders.Bottom.Width = 0.5; otras.Format.Borders.Bottom.Color = Linea; otras.Format.Borders.DistanceFromBottom = "4pt";

        foreach (var pie in new[] { seccion.Footers.FirstPage, seccion.Footers.Primary })
        {
            var linea = pie.AddParagraph();
            linea.Format.Font.Size = 7.5; linea.Format.Font.Color = Gris;
            linea.Format.TabStops.AddTabStop(Unit.FromCentimeter(Ancho), TabAlignment.Right);
            linea.Format.Borders.Top.Width = 0.5; linea.Format.Borders.Top.Color = Linea; linea.Format.Borders.DistanceFromTop = "5pt";
            linea.AddText(Firma);
            linea.AddTab();
            linea.AddText("Página "); linea.AddPageField(); linea.AddText(" de "); linea.AddNumPagesField();
        }

        // Datos del estudiante y resumen.
        var datos = seccion.AddTable();
        datos.Borders.Visible = false; datos.LeftPadding = 0;
        foreach (var ancho in new[] { 8.2, 3.4, 2.9, 2.9 }) datos.AddColumn(Unit.FromCentimeter(ancho));
        var rotulos = datos.AddRow();
        string[] titulosDatos = ["ESTUDIANTE", "CÉDULA", record.Indice == Indices.Simple ? "PROMEDIO" : "ÍNDICE", "UC APROBADAS"];
        for (var i = 0; i < titulosDatos.Length; i++) Rotulo(rotulos.Cells[i], titulosDatos[i]);
        var valores = datos.AddRow();
        valores.TopPadding = "2pt";
        Valor(valores.Cells[0], record.Estudiante.NombreCompleto, 13);
        Valor(valores.Cells[1], Cedulas.Mostrar(record.Estudiante.Cedula), 11);
        Valor(valores.Cells[2], record.Promedio is { } promedio ? $"{promedio:0.##}" : "Pendiente", 11);
        Valor(valores.Cells[3], record.UcAprobadas.ToString(CultureInfo.CurrentCulture), 11);
        var nota = seccion.AddParagraph((record.Indice == Indices.Simple ? "El promedio es simple" : "El índice pondera cada definitiva por sus unidades de crédito") + " y solo incluye materias completas de períodos cerrados.");
        nota.Format.Font.Size = 7.5; nota.Format.Font.Color = Gris; nota.Format.SpaceBefore = "5pt";
        nota.Format.Borders.Bottom.Width = 0.75; nota.Format.Borders.Bottom.Color = Tinta; nota.Format.Borders.DistanceFromBottom = "10pt";

        if (record.Materias.Count == 0)
        {
            var vacio = seccion.AddParagraph("Todavía no hay notas publicadas.");
            vacio.Format.SpaceBefore = "18pt"; vacio.Format.Font.Color = Gris;
        }
        foreach (var periodo in record.Materias.GroupBy(m => m.Periodo))
        {
            var cerrado = periodo.All(m => m.PeriodoCerrado);
            var titulo = seccion.AddParagraph();
            titulo.Format.SpaceBefore = "20pt"; titulo.Format.SpaceAfter = "2pt"; titulo.Format.KeepWithNext = true;
            titulo.AddFormattedText($"Período {periodo.Key}", new Font { Size = 12, Bold = true, Color = Tinta });
            titulo.AddFormattedText(cerrado ? "   cerrado" : "   abierto", new Font { Size = 8.5, Color = Gris });
            foreach (var materia in periodo) Materia(seccion, materia);
        }

        var regla = seccion.AddParagraph("La definitiva se calcula con todas las evaluaciones y se redondea una sola vez, según las reglas de calificación de la institución. " +
            "Cada materia indica si quedó aprobada o aplazada.");
        regla.Format.Font.Size = 7.5; regla.Format.Font.Color = Gris; regla.Format.SpaceBefore = "22pt";

        var renderizador = new PdfDocumentRenderer { Document = documento };
        renderizador.RenderDocument();
        using var salida = new MemoryStream();
        renderizador.PdfDocument.Save(salida, false);
        GuardarAtomico(destino, salida.ToArray());
    }

    static void Materia(MigraDoc.DocumentObjectModel.Section seccion, MateriaRecord materia)
    {
        var tabla = seccion.AddTable();
        tabla.Borders.Visible = false;
        tabla.LeftPadding = tabla.RightPadding = 0;
        tabla.KeepTogether = materia.Evaluaciones.Count < 14;
        double[] anchos = [8.4, 3.0, 3.0, 3.0];
        foreach (var ancho in anchos) tabla.AddColumn(Unit.FromCentimeter(ancho));

        // Nombre de la materia y definitiva en la misma franja.
        var nombre = tabla.AddRow();
        nombre.TopPadding = "9pt"; nombre.BottomPadding = "1pt";
        nombre.Cells[0].MergeRight = 1;
        var texto = nombre.Cells[0].AddParagraph();
        texto.AddFormattedText(materia.Materia, new Font { Size = 10.5, Bold = true });
        var final = nombre.Cells[2].AddParagraph();
        nombre.Cells[2].MergeRight = 1;
        final.Format.Alignment = ParagraphAlignment.Right;
        if (materia.NotaFinal is { } nf)
        {
            final.AddFormattedText($"{Escritorio.Presentacion.Textos.Nota(nf)}", new Font { Size = 12, Bold = true, Color = Indigo });
            final.AddFormattedText($" /{materia.EscalaMaxima:0.##}  ", new Font { Size = 8, Color = Gris });
            final.AddFormattedText((materia.Condicion ?? "").ToUpperInvariant(), new Font { Size = 7.5, Bold = true, Color = materia.Condicion == Condiciones.Aprobado ? Exito : Peligro });
        }
        else final.AddFormattedText("Definitiva pendiente", new Font { Size = 8.5, Color = Gris });
        var detalle = tabla.AddRow();
        detalle.BottomPadding = "5pt";
        detalle.Cells[0].MergeRight = 3;
        var sub = detalle.Cells[0].AddParagraph($"{materia.Carrera}, sección {materia.Seccion}. Publicado el {materia.PublicadoEn.ToLocalTime():dd/MM/yyyy}");
        sub.Format.Font.Size = 8; sub.Format.Font.Color = Gris;

        var cabecera = tabla.AddRow();
        cabecera.HeadingFormat = false;
        cabecera.Borders.Bottom.Width = 0.5; cabecera.Borders.Bottom.Color = Gris;
        string[] titulos = ["Evaluación", "Ponderación", "Nota", "Puntos"];
        for (var i = 0; i < titulos.Length; i++)
        {
            var p = cabecera.Cells[i].AddParagraph(titulos[i]);
            p.Format.Font.Size = 7.5; p.Format.Font.Bold = true; p.Format.Font.Color = Gris;
            if (i > 0) p.Format.Alignment = ParagraphAlignment.Right;
        }
        foreach (var e in materia.Evaluaciones)
        {
            var fila = tabla.AddRow();
            fila.TopPadding = fila.BottomPadding = "3pt";
            fila.Borders.Bottom.Width = 0.4; fila.Borders.Bottom.Color = Linea;
            string[] celdas = [e.Evaluacion, $"{Numero(e.Peso)} %", Numero(e.Nota), Numero(e.AporteSobreVeinte)];
            for (var i = 0; i < celdas.Length; i++)
            {
                var p = fila.Cells[i].AddParagraph(celdas[i]);
                if (i > 0) p.Format.Alignment = ParagraphAlignment.Right;
                if (celdas[i] == "pendiente") p.Format.Font.Color = Gris;
            }
        }
        var total = tabla.AddRow();
        total.TopPadding = "4pt";
        total.Cells[0].AddParagraph($"Acumulado ({Numero(materia.PesoCalificado)} % evaluado)").Format.Font.Color = Gris;
        var acumulado = total.Cells[3].AddParagraph($"{Numero(materia.Acumulado)}");
        acumulado.Format.Alignment = ParagraphAlignment.Right; acumulado.Format.Font.Bold = true;
    }

    static void Rotulo(Celda celda, string texto)
    {
        var p = celda.AddParagraph(texto);
        p.Format.Font.Size = 7; p.Format.Font.Bold = true; p.Format.Font.Color = Gris;
    }

    static void Valor(Celda celda, string texto, double tamano)
    {
        var p = celda.AddParagraph(texto);
        p.Format.Font.Size = tamano; p.Format.Font.Bold = true;
    }

    public static FlowDocument Imprimible(RecordAcademico record)
    {
        var tinta = new Medios.SolidColorBrush(Medios.Color.FromRgb(0x17, 0x1A, 0x2C));
        var gris = new Medios.SolidColorBrush(Medios.Color.FromRgb(0x6B, 0x71, 0x8C));
        var linea = new Medios.SolidColorBrush(Medios.Color.FromRgb(0xE2, 0xE5, 0xEE));
        var indigo = new Medios.SolidColorBrush(Medios.Color.FromRgb(0x43, 0x43, 0xD0));
        var documento = new FlowDocument { FontFamily = new Medios.FontFamily("Segoe UI"), FontSize = 12, Foreground = tinta,
            PagePadding = new Thickness(56, 48, 56, 48), ColumnWidth = double.PositiveInfinity };

        var imagen = new BitmapImage();
        imagen.BeginInit(); imagen.CacheOption = BitmapCacheOption.OnLoad; imagen.StreamSource = new MemoryStream(Logotipo()); imagen.EndInit(); imagen.Freeze();
        var cabeza = new System.Windows.Controls.DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 22) };
        var logo = new System.Windows.Controls.Image { Source = imagen, Height = 34, HorizontalAlignment = HorizontalAlignment.Left };
        var ahora = DateTime.Now;
        var meta = new System.Windows.Controls.TextBlock { TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, FontSize = 10, Foreground = gris };
        meta.Inlines.Add(new Run("RÉCORD ACADÉMICO") { FontWeight = FontWeights.Bold, Foreground = indigo });
        meta.Inlines.Add(new LineBreak());
        meta.Inlines.Add(new Run($"Emitido el {ahora.ToString("d 'de' MMMM 'de' yyyy", Espanol)}, {ahora:HH:mm}"));
        System.Windows.Controls.DockPanel.SetDock(meta, System.Windows.Controls.Dock.Right);
        cabeza.Children.Add(meta);
        cabeza.Children.Add(logo);
        documento.Blocks.Add(new BlockUIContainer(cabeza));

        var estudiante = new Parrafo { Margin = new Thickness(0, 0, 0, 4) };
        estudiante.Inlines.Add(new Run(record.Estudiante.NombreCompleto) { FontSize = 18, FontWeight = FontWeights.Bold });
        estudiante.Inlines.Add(new Run($"   Cédula {Cedulas.Mostrar(record.Estudiante.Cedula)}") { Foreground = gris });
        documento.Blocks.Add(estudiante);
        documento.Blocks.Add(new Parrafo(new Run($"{(record.Indice == Indices.Simple ? "Promedio" : "Índice")} {(record.Promedio is { } p ? p.ToString("0.##") : "pendiente")}, {record.UcAprobadas} UC aprobadas, aprobadas {record.Materias.Count(m => m.Condicion == Condiciones.Aprobado)} de {record.Materias.Count}"))
            { Foreground = gris, FontSize = 11, BorderBrush = tinta, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, 12), Margin = new Thickness(0, 0, 0, 6) });

        foreach (var periodo in record.Materias.GroupBy(m => m.Periodo))
        {
            documento.Blocks.Add(new Parrafo(new Run($"Período {periodo.Key}") { FontSize = 14, FontWeight = FontWeights.Bold }) { Margin = new Thickness(0, 18, 0, 4), KeepWithNext = true });
            foreach (var materia in periodo)
            {
                var tabla = new Table { CellSpacing = 0, Margin = new Thickness(0, 6, 0, 8) };
                foreach (var ancho in new[] { 3.2, 1.1, 1.0, 1.0 }) tabla.Columns.Add(new TableColumn { Width = new GridLength(ancho, GridUnitType.Star) });
                var grupo = new TableRowGroup();
                var titulo = new TableRow();
                var nombre = new Parrafo(new Run(materia.Materia) { FontWeight = FontWeights.Bold });
                nombre.Inlines.Add(new LineBreak());
                nombre.Inlines.Add(new Run($"{materia.Carrera}, sección {materia.Seccion}") { FontSize = 10, Foreground = gris });
                titulo.Cells.Add(new TableCell(nombre) { ColumnSpan = 2, Padding = new Thickness(0, 0, 0, 6) });
                var final = new Parrafo { TextAlignment = TextAlignment.Right };
                if (materia.NotaFinal is { } nf)
                {
                    final.Inlines.Add(new Run(Escritorio.Presentacion.Textos.Nota(nf)) { FontSize = 15, FontWeight = FontWeights.Bold, Foreground = indigo });
                    final.Inlines.Add(new Run($" /{materia.EscalaMaxima:0.##}  {(materia.Condicion ?? "").ToUpperInvariant()}") { FontSize = 10, Foreground = gris });
                }
                else final.Inlines.Add(new Run("Definitiva pendiente") { FontSize = 10, Foreground = gris });
                titulo.Cells.Add(new TableCell(final) { ColumnSpan = 2 });
                grupo.Rows.Add(titulo);
                var cabecera = new TableRow();
                foreach (var (texto, i) in new[] { "Evaluación", "Ponderación", "Nota", "Puntos" }.Select((t, i) => (t, i)))
                    cabecera.Cells.Add(new TableCell(new Parrafo(new Run(texto)) { TextAlignment = i == 0 ? TextAlignment.Left : TextAlignment.Right })
                        { FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = gris, BorderBrush = gris, BorderThickness = new Thickness(0, 0, 0, 0.8), Padding = new Thickness(0, 0, 0, 3) });
                grupo.Rows.Add(cabecera);
                foreach (var e in materia.Evaluaciones)
                {
                    var fila = new TableRow();
                    string[] celdas = [e.Evaluacion, $"{Numero(e.Peso)} %", Numero(e.Nota), Numero(e.AporteSobreVeinte)];
                    for (var i = 0; i < celdas.Length; i++)
                        fila.Cells.Add(new TableCell(new Parrafo(new Run(celdas[i])) { TextAlignment = i == 0 ? TextAlignment.Left : TextAlignment.Right })
                            { BorderBrush = linea, BorderThickness = new Thickness(0, 0, 0, 0.6), Padding = new Thickness(0, 4, 0, 4) });
                    grupo.Rows.Add(fila);
                }
                tabla.RowGroups.Add(grupo);
                documento.Blocks.Add(tabla);
            }
        }
        documento.Blocks.Add(new Parrafo(new Run(Firma)) { FontSize = 9, Foreground = gris, Margin = new Thickness(0, 24, 0, 0) });
        return documento;
    }

    public static void Csv(RecordAcademico record, string destino)
    {
        var texto = new StringBuilder("periodo,materia,seccion,evaluacion,ponderacion,nota,puntos,definitiva,condicion\r\n");
        foreach (var materia in record.Materias)
        foreach (var evaluacion in materia.Evaluaciones)
        {
            string[] campos = [materia.Periodo, materia.Materia, materia.Seccion, evaluacion.Evaluacion,
                Invariante(evaluacion.Peso), Invariante(evaluacion.Nota), Invariante(evaluacion.AporteSobreVeinte),
                Invariante(materia.NotaFinal), materia.Condicion ?? ""];
            texto.AppendLine(string.Join(',', campos.Select(EscaparCsv)));
        }
        GuardarAtomico(destino, new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(texto.ToString())).ToArray());
    }

    private static string EscaparCsv(string valor)
    {
        // Excel interpreta algunos textos como fórmulas aunque estén entre comillas.
        if (valor.Length > 0 && "=+-@\t\r".Contains(valor[0])) valor = "'" + valor;
        return "\"" + valor.Replace("\"", "\"\"") + "\"";
    }

    static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");
    private static string Invariante(decimal? numero) => numero?.ToString("0.####", CultureInfo.InvariantCulture) ?? "";
    private static string Numero(decimal? numero) => numero?.ToString("0.####") ?? "pendiente";
    internal static void GuardarAtomico(string destino, byte[] contenido)
    {
        var temporal = destino + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temporal, contenido); File.Move(temporal, destino, true); }
        finally { if (File.Exists(temporal)) File.Delete(temporal); }
    }
}
