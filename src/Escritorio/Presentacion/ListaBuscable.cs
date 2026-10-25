using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Escritorio.Presentacion;

/// <summary>
/// Lista para elegir escribiendo: filtra por cualquier parte del texto, sin importar tildes ni mayúsculas.
/// Flechas para moverse, Enter o Tab para elegir, Esc para dejar lo que había.
/// </summary>
public sealed class ListaBuscable : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable),
        typeof(ListaBuscable), new PropertyMetadata(null, (o, _) => ((ListaBuscable)o).Reiniciar()));
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(object),
        typeof(ListaBuscable), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (o, _) => ((ListaBuscable)o).MostrarElegido()));

    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    private string campoVisible = "";
    // Puede llegar después del valor elegido (por ejemplo en una plantilla); entonces se rehace el texto.
    public string DisplayMemberPath
    {
        get => campoVisible;
        set { campoVisible = value; lista.DisplayMemberPath = value; MostrarElegido(); }
    }
    /// <summary>Texto de ayuda cuando no hay nada elegido.</summary>
    public string? Ayuda { get => Sugerencias.GetTexto(caja); set => Sugerencias.SetTexto(caja, value ?? ""); }
    public event EventHandler? Elegido;

    private readonly TextBox caja = new();
    private readonly ListBox lista = new() { BorderThickness = new Thickness(0), Background = Brushes.Transparent, Focusable = false };
    private readonly TextBlock vacio = new() { Text = "Sin coincidencias", Margin = new Thickness(10, 9, 10, 9), Visibility = Visibility.Collapsed };
    private readonly Popup emergente;
    private ListCollectionView? vista;
    private bool escribiendo;

    public ListaBuscable()
    {
        Focusable = false;
        IsTabStop = false;
        caja.SetResourceReference(StyleProperty, "CampoLista");
        Sugerencias.SetTexto(caja, "Escribe para buscar…");
        lista.ItemContainerStyle = (Style)FindResource("OpcionBuscable");
        lista.MaxHeight = 300;
        ScrollViewer.SetHorizontalScrollBarVisibility(lista, ScrollBarVisibility.Disabled);
        vacio.SetResourceReference(TextBlock.ForegroundProperty, "Texto3");

        var contenido = new StackPanel();
        contenido.Children.Add(vacio);
        contenido.Children.Add(lista);
        var marco = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Padding = new Thickness(4), Child = contenido };
        marco.SetResourceReference(Border.BackgroundProperty, "Superficie");
        marco.SetResourceReference(Border.BorderBrushProperty, "Borde");
        marco.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Direction = 270, Opacity = 0.14,
            Color = (Color)FindResource("ColorSombra") };
        emergente = new Popup { Child = new Border { Padding = new Thickness(10, 0, 10, 14), Child = marco }, PlacementTarget = caja,
            Placement = PlacementMode.Bottom, VerticalOffset = 4, HorizontalOffset = -10, AllowsTransparency = true, StaysOpen = false, Focusable = false };
        emergente.Opened += (_, _) => marco.MinWidth = caja.ActualWidth;

        var flecha = new Path { Data = Geometry.Parse("M0,0 L5,5 L10,0"), StrokeThickness = 1.6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 1, 14, 0), IsHitTestVisible = false };
        flecha.SetResourceReference(Shape.StrokeProperty, "Texto2");
        var capas = new Grid();
        capas.Children.Add(caja);
        capas.Children.Add(flecha);
        capas.Children.Add(emergente);
        Content = capas;

        // Con Tab solo se selecciona el texto; la lista se abre al escribir, al hacer clic o con flecha abajo.
        caja.GotKeyboardFocus += (_, _) => caja.SelectAll();
        caja.PreviewMouseLeftButtonUp += (_, _) => { if (!emergente.IsOpen) Abrir(""); };
        caja.TextChanged += (_, _) => { if (!escribiendo) Abrir(caja.Text); };
        caja.PreviewKeyDown += Teclas;
        caja.LostKeyboardFocus += (_, _) =>
        {
            emergente.IsOpen = false;
            // Borrar el texto y salir deja la lista sin elección.
            if (caja.Text.Trim().Length == 0 && SelectedItem is not null) { SelectedItem = null; Elegido?.Invoke(this, EventArgs.Empty); }
            MostrarElegido();
        };
        // El nombre accesible declarado en el control lo lee la caja, que es la que recibe el foco.
        Loaded += (_, _) =>
        {
            if (System.Windows.Automation.AutomationProperties.GetName(this) is { Length: > 0 } nombre)
                System.Windows.Automation.AutomationProperties.SetName(caja, nombre);
        };
        lista.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (ItemsControl.ContainerFromElement(lista, (DependencyObject)e.OriginalSource) is ListBoxItem { Content: { } item }) Elegir(item);
        };
    }

    public TextBox Caja => caja;
    internal Popup Emergente => emergente;
    internal void Seleccionar(object item) => Elegir(item);

    private void Reiniciar()
    {
        vista = ItemsSource is null ? null : new ListCollectionView(ItemsSource.Cast<object>().ToList());
        // En listas cortas basta con elegir; en las largas se invita a escribir.
        if (Sugerencias.GetTexto(caja) is "Escribe para buscar…" or "Elige una opción")
            Sugerencias.SetTexto(caja, vista is { Count: <= 6 } ? "Elige una opción" : "Escribe para buscar…");
        lista.ItemsSource = vista;
        lista.DisplayMemberPath = DisplayMemberPath;
        MostrarElegido();
    }

    private void Abrir(string filtro)
    {
        if (vista is null) return;
        var buscado = Simplificar(filtro);
        vista.Filter = buscado.Length == 0 ? null : o => Simplificar(Texto(o)).Contains(buscado, StringComparison.Ordinal);
        vacio.Visibility = vista.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        lista.SelectedItem = buscado.Length == 0 && SelectedItem is not null && vista.Contains(SelectedItem) ? SelectedItem : vista.Count > 0 ? vista.GetItemAt(0) : null;
        if (lista.SelectedItem is { } marcado) lista.ScrollIntoView(marcado);
        if (caja.IsKeyboardFocusWithin) emergente.IsOpen = true;
    }

    private void Teclas(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down or Key.Up when vista is { Count: > 0 }:
                if (!emergente.IsOpen) { Abrir(""); e.Handled = true; return; }
                var indice = Math.Clamp(lista.SelectedIndex + (e.Key == Key.Down ? 1 : -1), 0, vista.Count - 1);
                lista.SelectedIndex = indice;
                lista.ScrollIntoView(lista.SelectedItem);
                e.Handled = true;
                break;
            case Key.Enter when emergente.IsOpen && lista.SelectedItem is { } elegido:
                Elegir(elegido);
                e.Handled = true;
                break;
            case Key.Tab when emergente.IsOpen && lista.SelectedItem is { } marcado && caja.Text != Texto(SelectedItem):
                Elegir(marcado);
                break;
            case Key.Escape when emergente.IsOpen:
                emergente.IsOpen = false;
                MostrarElegido();
                caja.SelectAll();
                e.Handled = true;
                break;
            case Key.F4:
                if (emergente.IsOpen) emergente.IsOpen = false; else Abrir("");
                e.Handled = true;
                break;
        }
    }

    private void Elegir(object item)
    {
        SelectedItem = item;
        emergente.IsOpen = false;
        MostrarElegido();
        caja.SelectAll();
        Elegido?.Invoke(this, EventArgs.Empty);
    }

    private void MostrarElegido()
    {
        escribiendo = true;
        caja.Text = Texto(SelectedItem);
        escribiendo = false;
    }

    private string Texto(object? item)
    {
        if (item is null) return "";
        if (DisplayMemberPath.Length == 0) return item.ToString() ?? "";
        return TypeDescriptor.GetProperties(item)[DisplayMemberPath]?.GetValue(item)?.ToString() ?? "";
    }

    private static string Simplificar(string texto)
    {
        var descompuesto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var limpio = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) limpio.Append(c);
        return limpio.ToString();
    }
}
