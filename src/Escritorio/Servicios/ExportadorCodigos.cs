using System.IO;
using Entidades;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Documento = MigraDoc.DocumentObjectModel.Document;

namespace Escritorio.Servicios;

/// <summary>
/// Hoja de códigos de acceso para entregar en clase: una tira por estudiante, para recortar.
/// El docente verifica la identidad (cédula o comprobante de inscripción) antes de entregar cada tira.
/// </summary>
public static class ExportadorCodigos
{
    static readonly Color Indigo = Color.FromRgb(0x43, 0x43, 0xD0);
    static readonly Color Tinta = Color.FromRgb(0x17, 0x1A, 0x2C);
    static readonly Color Gris = Color.FromRgb(0x6B, 0x71, 0x8C);
    static readonly Color Corte = Color.FromRgb(0xB8, 0xBD, 0xCC);
    const double Ancho = 18.6; // cm útiles en A4 con márgenes de 1,2

    public static void Pdf(CodigosDeSeccion hoja, string destino)
    {
        var documento = new Documento();
        documento.Info.Title = $"Códigos de acceso, {hoja.Asignacion}";
        documento.Info.Author = "Mérito";
        var normal = documento.Styles[StyleNames.Normal]!;
        normal.Font.Name = "Segoe UI"; normal.Font.Size = 9; normal.Font.Color = Tinta;
        normal.ParagraphFormat.SpaceAfter = 0;

        var seccion = documento.AddSection();
        var pagina = seccion.PageSetup;
        pagina.PageFormat = PageFormat.A4;
        pagina.TopMargin = "2.2cm"; pagina.BottomMargin = "1.4cm";
        pagina.LeftMargin = pagina.RightMargin = "1.2cm";
        pagina.HeaderDistance = "0.9cm"; pagina.FooterDistance = "0.7cm";

        var cabeza = seccion.Headers.Primary.AddTable();
        cabeza.AddColumn(Unit.FromCentimeter(6));
        cabeza.AddColumn(Unit.FromCentimeter(Ancho - 6));
        var fila = cabeza.AddRow();
        fila.VerticalAlignment = VerticalAlignment.Center;
        var logo = fila.Cells[0].AddImage("base64:" + Convert.ToBase64String(ExportadorRecord.Logotipo()));
        logo.Height = "0.8cm"; logo.LockAspectRatio = true;
        var titulo = fila.Cells[1].AddParagraph("CÓDIGOS DE ACCESO");
        titulo.Format.Alignment = ParagraphAlignment.Right;
        titulo.Format.Font.Size = 8; titulo.Format.Font.Bold = true; titulo.Format.Font.Color = Indigo;
        var asignacion = fila.Cells[1].AddParagraph(hoja.Asignacion);
        asignacion.Format.Alignment = ParagraphAlignment.Right;
        asignacion.Format.Font.Size = 8; asignacion.Format.Font.Color = Gris;

        var pie = seccion.Footers.Primary.AddParagraph();
        pie.Format.Font.Size = 7; pie.Format.Font.Color = Gris;
        pie.Format.TabStops.AddTabStop(Unit.FromCentimeter(Ancho), TabAlignment.Right);
        pie.AddText("Entrega cada tira solo a su titular, después de verificar su cédula o su comprobante de inscripción.");
        pie.AddTab();
        pie.AddText("Página "); pie.AddPageField(); pie.AddText(" de "); pie.AddNumPagesField();

        // Dos tiras por renglón; el borde punteado marca dónde recortar.
        var tabla = seccion.AddTable();
        tabla.AddColumn(Unit.FromCentimeter(Ancho / 2));
        tabla.AddColumn(Unit.FromCentimeter(Ancho / 2));
        tabla.Borders.Style = BorderStyle.DashSmallGap;
        tabla.Borders.Color = Corte;
        tabla.Borders.Width = 0.6;
        tabla.LeftPadding = tabla.RightPadding = "0.45cm";
        tabla.TopPadding = "0.35cm"; tabla.BottomPadding = "0.35cm";
        for (var i = 0; i < hoja.Codigos.Count; i += 2)
        {
            var renglon = tabla.AddRow();
            Tira(renglon.Cells[0], hoja.Codigos[i]);
            if (i + 1 < hoja.Codigos.Count) Tira(renglon.Cells[1], hoja.Codigos[i + 1]);
        }

        var renderizador = new PdfDocumentRenderer { Document = documento };
        renderizador.RenderDocument();
        using var salida = new MemoryStream();
        renderizador.PdfDocument.Save(salida, false);
        ExportadorRecord.GuardarAtomico(destino, salida.ToArray());
    }

    static void Tira(Cell celda, CodigoEstudiante codigo)
    {
        var marca = celda.AddParagraph();
        marca.AddFormattedText("Mérito", new Font { Bold = true, Color = Indigo, Size = 8.5 });
        marca.AddFormattedText("   código de acceso personal", new Font { Color = Gris, Size = 7.5 });
        var nombre = celda.AddParagraph(codigo.NombreCompleto);
        nombre.Format.Font.Size = 10.5; nombre.Format.Font.Bold = true; nombre.Format.SpaceBefore = "5pt";
        var cedula = celda.AddParagraph($"Cédula {Cedulas.Mostrar(codigo.Cedula)}");
        cedula.Format.Font.Color = Gris; cedula.Format.Font.Size = 8.5;
        var clave = celda.AddParagraph(codigo.Codigo);
        clave.Format.Font.Name = "Consolas"; clave.Format.Font.Size = 15; clave.Format.Font.Bold = true;
        clave.Format.SpaceBefore = "6pt"; clave.Format.SpaceAfter = "1pt";
        var vence = celda.AddParagraph($"Vence el {codigo.VenceEn.ToLocalTime():dd/MM/yyyy} a las {codigo.VenceEn.ToLocalTime():HH:mm}. Sirve una sola vez.");
        vence.Format.Font.Size = 7.5; vence.Format.Font.Color = Gris;
        var pasos = celda.AddParagraph("En Mérito pulsa «Activar cuenta o recuperar acceso», escribe tu cédula y este código, y elige tu contraseña de 12 caracteres o más.");
        pasos.Format.Font.Size = 7.5; pasos.Format.SpaceBefore = "4pt";
    }
}
