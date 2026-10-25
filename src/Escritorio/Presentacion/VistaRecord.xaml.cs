using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Entidades;
using Escritorio.Servicios;
using Microsoft.Win32;

namespace Escritorio.Presentacion;

public sealed partial class VistaRecord : VistaBase
{
    // Lo que muestran las plantillas del XAML, ya con el texto listo para leer.
    public sealed record GrupoPeriodo(string Titulo, bool Cerrado, List<TarjetaMateria> Materias);
    public sealed record TarjetaMateria(string Materia, string Detalle, string Acumulado, double Avance, string Definitiva,
        bool TieneDefinitiva, bool DefinitivaConDecimales, string? Condicion, string CondicionTexto, List<DetallePublicado> Evaluaciones, string Publicacion)
    {
        public bool TieneCondicion => Condicion is not null;
    }

    private RecordAcademico? record;

    public VistaRecord(ClienteApi api) : base(api)
    {
        InitializeComponent();
        Atajo(Key.F5, ModifierKeys.None, Cargar, "Récord actualizado.");
        RecargarSolo(Cargar);
        Atajo(Key.F12, ModifierKeys.None, () => Exportar(false));
        Atajo(Key.P, ModifierKeys.Control, Imprimir);
        Loaded += async (_, _) => await Ejecutar(Cargar);
    }

    private async void Pdf_Click(object sender, RoutedEventArgs e) => await Ejecutar(() => Exportar(false));
    private async void Csv_Click(object sender, RoutedEventArgs e) => await Ejecutar(() => Exportar(true));
    private async void Imprimir_Click(object sender, RoutedEventArgs e) => await Ejecutar(Imprimir);

    private async Task Cargar()
    {
        record = await Api.Obtener<RecordAcademico>("record");
        Promedio.Text = record.Promedio is { } valor ? $"{valor:0.##} /{record.EscalaMaxima:0.##}" : "Pendiente";
        IndicadorIndice.Rotulo = record.Indice == Indices.Simple ? "Promedio general" : "Índice académico";
        Promedio.ToolTip = (record.Indice == Indices.Simple ? "Promedio simple de las definitivas" : "Definitivas ponderadas por unidades de crédito") + " en períodos cerrados.";
        Promedio.Foreground = (Brush)FindResource(record.Promedio is null ? "Texto3" : "Indigo700");
        Publicadas.Text = record.Materias.Count.ToString(CultureInfo.CurrentCulture);
        UcAprobadas.Text = record.UcAprobadas.ToString(CultureInfo.CurrentCulture);
        Aprobadas.Text = record.Materias.Count(m => m.Condicion == Condiciones.Aprobado).ToString(CultureInfo.CurrentCulture);
        SinNotas.Visibility = record.Materias.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Periodos.ItemsSource = record.Materias.GroupBy(m => m.Periodo)
            .Select(g => new GrupoPeriodo($"Período {g.Key}", g.All(m => m.PeriodoCerrado), g.Select(Tarjeta).ToList())).ToList();
    }

    private static TarjetaMateria Tarjeta(MateriaRecord m) => new(
        m.Materia, (m.Codigo.Length > 0 ? $"{m.Codigo}, {m.Uc} UC. " : "") + $"{m.Carrera}, sección {m.Seccion}",
        $"Acumulado: {m.Acumulado:0.####} puntos, con el {m.PesoCalificado:0.##} % de la materia evaluado",
        (double)Math.Min(m.PesoCalificado, 100m),
        m.NotaFinal is { } nota ? Textos.Nota(nota) : "Pendiente",
        m.NotaFinal is not null, m.NotaFinal is { } n && n != decimal.Truncate(n),
        m.Condicion, Textos.Capitalizar(m.Condicion), m.Evaluaciones,
        $"Última publicación: {m.PublicadoEn.ToLocalTime():dd/MM/yyyy HH:mm}");

    private Task Exportar(bool csv)
    {
        if (record is null) throw new ArgumentException("Primero carga tu récord.");
        var guardar = new SaveFileDialog { FileName = "record_academico" + (csv ? ".csv" : ".pdf"), Filter = csv ? "Valores separados por comas|*.csv" : "Documento PDF|*.pdf" };
        if (guardar.ShowDialog(Propietario) != true) throw new OperacionCancelada();
        if (csv) ExportadorRecord.Csv(record, guardar.FileName);
        else ExportadorRecord.Pdf(record, guardar.FileName);
        Notificar($"Archivo guardado en {guardar.FileName}.", Tono.Exito);
        return Task.CompletedTask;
    }

    private Task Imprimir()
    {
        if (record is null) throw new ArgumentException("Primero carga tu récord.");
        var imprimir = new PrintDialog();
        if (imprimir.ShowDialog() != true) throw new OperacionCancelada();
        var documento = ExportadorRecord.Imprimible(record);
        documento.PageWidth = imprimir.PrintableAreaWidth;
        documento.PageHeight = imprimir.PrintableAreaHeight;
        imprimir.PrintDocument(((IDocumentPaginatorSource)documento).DocumentPaginator, "Récord académico");
        Notificar("Documento enviado a la impresora.", Tono.Exito);
        return Task.CompletedTask;
    }
}
