using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Escritorio.Presentacion;

// Una nota por cargar: de un estudiante en una evaluación, o de una evaluación para un estudiante.
public sealed record CampoNota(long Clave, string Titulo, string Detalle, string Valor);

/// <summary>
/// Carga de notas en una ventana aparte: la planilla solo muestra. Cada nota se valida al escribirla,
/// Enter pasa a la siguiente y nada se guarda hasta pulsar «Guardar borrador».
/// </summary>
public sealed class VentanaNotas : VentanaBase
{
    private readonly List<(CampoNota Campo, TextBox Caja, TextBlock Error)> filas = [];
    private readonly Func<string, string?> validar;
    private readonly TextBlock resumen = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock aviso = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12), Visibility = Visibility.Collapsed };

    /// <summary>Solo las notas que cambiaron, por clave del campo. Vacío quiere decir pendiente.</summary>
    public Dictionary<long, string> Cambios { get; } = [];

    public VentanaNotas(Window propietario, string titulo, string descripcion, IReadOnlyList<CampoNota> campos, Func<string, string?> validar)
    {
        this.validar = validar;
        if (propietario is { IsLoaded: true, IsVisible: true }) { Owner = propietario; WindowStartupLocation = WindowStartupLocation.CenterOwner; }
        else WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Title = titulo;
        Width = 560;
        MaxHeight = SystemParameters.WorkArea.Height - 60;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        MostrarTitulo = false;

        var cuerpo = new DockPanel { Margin = new Thickness(30, 0, 30, 26) };
        var cabecera = new StackPanel();
        cabecera.Children.Add(new TextBlock { Text = titulo, Style = (Style)FindResource("TituloDialogo"), FontSize = 21 });
        cabecera.Children.Add(new TextBlock { Text = descripcion, Style = (Style)FindResource("TextoSecundario"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 18) });
        aviso.SetResourceReference(TextBlock.ForegroundProperty, "Error");
        cabecera.Children.Add(aviso);
        DockPanel.SetDock(cabecera, Dock.Top);
        cuerpo.Children.Add(cabecera);

        var guardar = new Button { Content = "Guardar borrador", MinWidth = 150 };
        guardar.Click += (_, _) => Aceptar();
        var cancelar = new Button { Content = "Cancelar", IsCancel = true, MinWidth = 104, Style = (Style)FindResource("BotonSecundario"), Margin = new Thickness(0, 0, 10, 0) };
        var botones = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        botones.Children.Add(cancelar);
        botones.Children.Add(guardar);
        var pie = new DockPanel();
        DockPanel.SetDock(botones, Dock.Right);
        pie.Children.Add(botones);
        resumen.SetResourceReference(TextBlock.ForegroundProperty, "Texto2");
        pie.Children.Add(resumen);
        var marcoPie = new Border { BorderBrush = (Brush)FindResource("Borde"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 18, 0, 0), Margin = new Thickness(0, 14, 0, 0), Child = pie };
        DockPanel.SetDock(marcoPie, Dock.Bottom);
        cuerpo.Children.Add(marcoPie);

        var lista = new StackPanel();
        foreach (var campo in campos) lista.Children.Add(Fila(campo));
        // La ventana nunca pasa de la pantalla: con muchos estudiantes, la lista se desplaza.
        cuerpo.Children.Add(new ScrollViewer { Content = lista, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false,
            MaxHeight = Math.Max(220, SystemParameters.WorkArea.Height - 360) });
        Content = cuerpo;
        Actualizar();
        Loaded += (_, _) => { var primera = filas.FirstOrDefault(f => f.Caja.Text.Length == 0).Caja ?? filas.FirstOrDefault().Caja; primera?.Focus(); primera?.SelectAll(); };
    }

    internal IReadOnlyList<TextBox> Cajas => filas.Select(f => f.Caja).ToList();

    private UIElement Fila(CampoNota campo)
    {
        var renglon = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        renglon.ColumnDefinitions.Add(new ColumnDefinition());
        renglon.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        var nombres = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
        nombres.Children.Add(new TextBlock { Text = campo.Titulo, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        nombres.Children.Add(new TextBlock { Text = campo.Detalle, Style = (Style)FindResource("TextoNota") });
        renglon.Children.Add(nombres);
        var caja = new TextBox { Text = campo.Valor, MaxLength = 6, HorizontalContentAlignment = HorizontalAlignment.Center, FontSize = 15 };
        AutomationProperties.SetName(caja, $"Nota de {campo.Titulo}");
        Sugerencias.SetTexto(caja, "Pendiente");
        Grid.SetColumn(caja, 1);
        renglon.Children.Add(caja);
        var error = new TextBlock { Style = (Style)FindResource("TextoNota"), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 4, 0, 0) };
        error.SetResourceReference(TextBlock.ForegroundProperty, "Error");
        Grid.SetRow(error, 1);
        Grid.SetColumnSpan(error, 2);
        renglon.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        renglon.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        renglon.Children.Add(error);
        filas.Add((campo, caja, error));
        caja.TextChanged += (_, _) => { Revisar(caja, error); Actualizar(); };
        caja.GotKeyboardFocus += (_, _) => caja.SelectAll();
        // Enter pasa a la nota siguiente.
        caja.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            caja.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        };
        return renglon;
    }

    private bool Revisar(TextBox caja, TextBlock error)
    {
        var problema = validar(caja.Text.Trim());
        error.Text = problema ?? "";
        error.Visibility = problema is null ? Visibility.Collapsed : Visibility.Visible;
        caja.SetResourceReference(BorderBrushProperty, problema is null ? "Borde" : "Error");
        return problema is null;
    }

    private void Actualizar()
    {
        var conNota = filas.Count(f => f.Caja.Text.Trim().Length > 0);
        resumen.Text = filas.Count == 1 ? (conNota == 1 ? "Con nota" : "Pendiente") : $"{conNota} de {filas.Count} con nota";
    }

    /// <summary>Guarda en Cambios lo que cambió, si todas las notas son válidas.</summary>
    internal bool Aceptar()
    {
        var malas = filas.Where(f => !Revisar(f.Caja, f.Error)).ToList();
        if (malas.Count > 0)
        {
            aviso.Text = malas.Count == 1 ? "Hay una nota que revisar." : $"Hay {malas.Count} notas que revisar.";
            aviso.Visibility = Visibility.Visible;
            malas[0].Caja.Focus();
            return false;
        }
        Cambios.Clear();
        foreach (var (campo, caja, _) in filas)
            if (caja.Text.Trim() != campo.Valor.Trim()) Cambios[campo.Clave] = caja.Text.Trim();
        try { DialogResult = true; }
        catch (InvalidOperationException) { Close(); } // abierta sin ShowDialog, como en las pruebas
        return true;
    }
}
