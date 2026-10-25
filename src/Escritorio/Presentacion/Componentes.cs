using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace Escritorio.Presentacion;

// Piezas que las pantallas declaran en XAML. Cada una arma su contenido a partir de propiedades simples,
// así el diseñador de Visual Studio las muestra igual que en ejecución.

/// <summary>
/// Muestra el texto completo al pasar el mouse sobre una celda que lo corta con «…».
/// </summary>
public static class TextoRecortado
{
    public static readonly DependencyProperty AvisarProperty = DependencyProperty.RegisterAttached("Avisar", typeof(bool), typeof(TextoRecortado),
        new PropertyMetadata(false, (o, e) =>
        {
            if (o is not TextBlock texto || e.NewValue is not true) return;
            texto.SizeChanged += (_, _) => Revisar(texto);
            texto.Loaded += (_, _) => Revisar(texto);
            // Las tablas reciclan celdas al desplazarse: cambia el texto sin cambiar el tamaño.
            System.ComponentModel.DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock))
                .AddValueChanged(texto, (_, _) => Revisar(texto));
        }));
    public static bool GetAvisar(DependencyObject o) => (bool)o.GetValue(AvisarProperty);
    public static void SetAvisar(DependencyObject o, bool valor) => o.SetValue(AvisarProperty, valor);

    private static void Revisar(TextBlock texto)
    {
        texto.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var recortado = texto.Text.Length > 0 && texto.DesiredSize.Width > texto.ActualWidth + 0.5;
        texto.ToolTip = recortado ? texto.Text : null;
    }
}

/// <summary>Icono seguido de un texto, como en botones y pestañas.</summary>
public sealed class IconoTexto : StackPanel
{
    public static readonly DependencyProperty IconoProperty = DependencyProperty.Register(nameof(Icono), typeof(string), typeof(IconoTexto),
        new PropertyMetadata(null, (o, _) => ((IconoTexto)o).Armar()));
    public static readonly DependencyProperty TextoProperty = DependencyProperty.Register(nameof(Texto), typeof(string), typeof(IconoTexto),
        new PropertyMetadata("", (o, _) => ((IconoTexto)o).Armar()));
    public static readonly DependencyProperty TamanoProperty = DependencyProperty.Register(nameof(Tamano), typeof(double), typeof(IconoTexto),
        new PropertyMetadata(17.0, (o, _) => ((IconoTexto)o).Armar()));

    public string? Icono { get => (string?)GetValue(IconoProperty); set => SetValue(IconoProperty, value); }
    public string Texto { get => (string)GetValue(TextoProperty); set => SetValue(TextoProperty, value); }
    public double Tamano { get => (double)GetValue(TamanoProperty); set => SetValue(TamanoProperty, value); }

    public IconoTexto() => Orientation = Orientation.Horizontal;

    private void Armar()
    {
        Children.Clear();
        if (Icono is { Length: > 0 } clave && TryFindResource(clave) is Geometry figura)
            Children.Add(new Icono { Datos = figura, Width = Tamano, Height = Tamano, VerticalAlignment = VerticalAlignment.Center });
        if (Texto.Length > 0)
            Children.Add(new TextBlock { Text = Texto, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(Children.Count > 0 ? 8 : 0, 0, 0, 0) });
    }
}

/// <summary>Botón de una barra de acciones: texto, icono opcional, atajo en la ayuda y estilo principal o secundario.</summary>
public sealed class BotonAccion : Button
{
    public static readonly DependencyProperty TextoProperty = DependencyProperty.Register(nameof(Texto), typeof(string), typeof(BotonAccion),
        new PropertyMetadata("", (o, _) => ((BotonAccion)o).Armar()));
    public static readonly DependencyProperty IconoProperty = DependencyProperty.Register(nameof(Icono), typeof(string), typeof(BotonAccion),
        new PropertyMetadata(null, (o, _) => ((BotonAccion)o).Armar()));
    public static readonly DependencyProperty AtajoProperty = DependencyProperty.Register(nameof(Atajo), typeof(string), typeof(BotonAccion),
        new PropertyMetadata(null, (o, _) => ((BotonAccion)o).Armar()));
    public static readonly DependencyProperty SecundarioProperty = DependencyProperty.Register(nameof(Secundario), typeof(bool), typeof(BotonAccion),
        new PropertyMetadata(false, (o, _) => ((BotonAccion)o).Armar()));

    public string Texto { get => (string)GetValue(TextoProperty); set => SetValue(TextoProperty, value); }
    public string? Icono { get => (string?)GetValue(IconoProperty); set => SetValue(IconoProperty, value); }
    public string? Atajo { get => (string?)GetValue(AtajoProperty); set => SetValue(AtajoProperty, value); }
    public bool Secundario { get => (bool)GetValue(SecundarioProperty); set => SetValue(SecundarioProperty, value); }

    public BotonAccion()
    {
        Margin = new Thickness(0, 0, 10, 10);
        SetResourceReference(StyleProperty, typeof(Button));
    }

    private void Armar()
    {
        if (Secundario) SetResourceReference(StyleProperty, "BotonSecundario");
        else SetResourceReference(StyleProperty, typeof(Button));
        if (Icono is null) { Content = Texto; ClearValue(PaddingProperty); }
        else
        {
            Content = new IconoTexto { Icono = Icono, Texto = Texto };
            Padding = new Thickness(13, 0, 16, 0);
        }
        AutomationProperties.SetName(this, Texto);
        if (Atajo is { Length: > 0 } atajo)
        {
            ToolTip = $"{Texto} ({atajo})";
            AutomationProperties.SetAcceleratorKey(this, atajo);
        }
        else ClearValue(ToolTipProperty);
    }
}

/// <summary>Barra de acciones con buscador opcional, la tabla y un pie con el conteo o la paginación.</summary>
[ContentProperty(nameof(Tabla))]
public sealed class PanelTabla : DockPanel
{
    public static readonly DependencyProperty AccionesProperty = DependencyProperty.Register(nameof(Acciones), typeof(UIElement), typeof(PanelTabla),
        new PropertyMetadata(null, (o, _) => ((PanelTabla)o).Armar()));
    public static readonly DependencyProperty TablaProperty = DependencyProperty.Register(nameof(Tabla), typeof(DataGrid), typeof(PanelTabla),
        new PropertyMetadata(null, (o, _) => ((PanelTabla)o).Armar()));
    public static readonly DependencyProperty BuscarProperty = DependencyProperty.Register(nameof(Buscar), typeof(string), typeof(PanelTabla),
        new PropertyMetadata(null, (o, _) => ((PanelTabla)o).Armar()));

    public UIElement? Acciones { get => (UIElement?)GetValue(AccionesProperty); set => SetValue(AccionesProperty, value); }
    public DataGrid? Tabla { get => (DataGrid?)GetValue(TablaProperty); set => SetValue(TablaProperty, value); }
    /// <summary>Texto de ayuda del buscador; sin él no hay buscador.</summary>
    public string? Buscar { get => (string?)GetValue(BuscarProperty); set => SetValue(BuscarProperty, value); }

    public TextBox Buscador { get; } = new() { Width = 260, Margin = new Thickness(12, 0, 0, 10), VerticalAlignment = VerticalAlignment.Top, MinHeight = 38, ToolTip = "Buscar (Ctrl+B)" };
    private readonly DockPanel barra = new() { LastChildFill = true };
    private readonly DockPanel pie = new();
    private UIElement? resumen, estado;

    public PanelTabla()
    {
        Buscador.SetResourceReference(StyleProperty, "CampoIcono");
        Buscador.SetResourceReference(TagProperty, "IcoBuscar");
        DockPanel.SetDock(Buscador, Dock.Right);
        DockPanel.SetDock(barra, Dock.Top);
        DockPanel.SetDock(pie, Dock.Bottom);
    }

    /// <summary>Lo coloca la vista: conteo de registros o paginador, y una franja de estado opcional.</summary>
    internal void Pie(UIElement resumen, UIElement? estado = null)
    {
        this.resumen = resumen;
        this.estado = estado;
        Armar();
    }

    private void Armar()
    {
        barra.Children.Clear();
        if (Buscar is { Length: > 0 } ayuda)
        {
            Sugerencias.SetTexto(Buscador, ayuda);
            AutomationProperties.SetName(Buscador, ayuda);
            Buscador.ToolTip = $"{ayuda} (Ctrl+B)";
            barra.Children.Add(Buscador);
        }
        if (Acciones is not null) { Desprender(Acciones); barra.Children.Add(Acciones); }
        pie.Children.Clear();
        if (resumen is not null) { DockPanel.SetDock(resumen, Dock.Top); Desprender(resumen); pie.Children.Add(resumen); }
        if (estado is not null) { Desprender(estado); pie.Children.Add(estado); }
        Children.Clear();
        Children.Add(barra);
        Children.Add(pie);
        if (Tabla is not null) { Desprender(Tabla); Children.Add(Tabla); }
    }

    private static void Desprender(UIElement elemento)
    {
        if (VisualTreeHelper.GetParent(elemento) is Panel padre) padre.Children.Remove(elemento);
    }
}

/// <summary>Tarjeta con un rótulo, su icono y el dato destacado que lleva dentro.</summary>
[ContentProperty(nameof(Valor))]
public sealed class Indicador : Border
{
    public static readonly DependencyProperty IconoProperty = DependencyProperty.Register(nameof(Icono), typeof(string), typeof(Indicador),
        new PropertyMetadata(null, (o, _) => ((Indicador)o).Armar()));
    public static readonly DependencyProperty RotuloProperty = DependencyProperty.Register(nameof(Rotulo), typeof(string), typeof(Indicador),
        new PropertyMetadata("", (o, _) => ((Indicador)o).Armar()));
    public static readonly DependencyProperty ValorProperty = DependencyProperty.Register(nameof(Valor), typeof(UIElement), typeof(Indicador),
        new PropertyMetadata(null, (o, _) => ((Indicador)o).Armar()));
    public static readonly DependencyProperty ExtraProperty = DependencyProperty.Register(nameof(Extra), typeof(UIElement), typeof(Indicador),
        new PropertyMetadata(null, (o, _) => ((Indicador)o).Armar()));

    public string? Icono { get => (string?)GetValue(IconoProperty); set => SetValue(IconoProperty, value); }
    public string Rotulo { get => (string)GetValue(RotuloProperty); set => SetValue(RotuloProperty, value); }
    public UIElement? Valor { get => (UIElement?)GetValue(ValorProperty); set => SetValue(ValorProperty, value); }
    public UIElement? Extra { get => (UIElement?)GetValue(ExtraProperty); set => SetValue(ExtraProperty, value); }

    public Indicador()
    {
        Margin = new Thickness(6, 0, 6, 0);
        Padding = new Thickness(16, 14, 16, 14);
        SetResourceReference(StyleProperty, "Tarjeta");
    }

    private void Armar()
    {
        var cabeza = new StackPanel { Orientation = Orientation.Horizontal };
        if (Icono is { Length: > 0 } clave && TryFindResource(clave) is Geometry figura)
        {
            var simbolo = new Icono { Datos = figura, Width = 16, Height = 16 };
            simbolo.SetResourceReference(TextElement.ForegroundProperty, "Indigo600");
            cabeza.Children.Add(simbolo);
        }
        var rotulo = new TextBlock { Text = Rotulo, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        rotulo.SetResourceReference(StyleProperty, "Rotulo");
        cabeza.Children.Add(rotulo);
        var contenido = new StackPanel();
        contenido.Children.Add(cabeza);
        if (Valor is not null) { if (LogicalTreeHelper.GetParent(Valor) is Panel p) p.Children.Remove(Valor); contenido.Children.Add(Valor); }
        if (Extra is not null) { if (LogicalTreeHelper.GetParent(Extra) is Panel p) p.Children.Remove(Extra); contenido.Children.Add(Extra); }
        Child = contenido;
    }
}

/// <summary>Franja para estados que deben seguir visibles, como cambios sin guardar.</summary>
public sealed class FranjaAviso : Border
{
    private readonly TextBlock texto = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, LineHeight = 20 };
    private readonly Icono icono = new() { Width = 17, Height = 17, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };

    public FranjaAviso()
    {
        CornerRadius = new CornerRadius(9);
        Margin = new Thickness(0, 14, 0, 0);
        MinHeight = 40;
        SnapsToDevicePixels = true;
        Visibility = Visibility.Collapsed;
        var fila = new DockPanel { Margin = new Thickness(14, 9, 14, 9) };
        DockPanel.SetDock(icono, Dock.Left);
        fila.Children.Add(icono);
        fila.Children.Add(texto);
        Child = fila;
    }

    public void Mostrar(string mensaje, Tono tono)
    {
        var (fondo, color) = Textos.Pinceles(tono);
        Background = fondo;
        texto.Foreground = color;
        TextElement.SetForeground(icono, color);
        icono.Datos = (Geometry)FindResource(tono switch { Tono.Exito => "IcoExito", Tono.Peligro => "IcoError", Tono.Aviso => "IcoAlerta", _ => "IcoInfo" });
        texto.Text = mensaje;
        Visibility = Visibility.Visible;
    }

    public void Ocultar()
    {
        Visibility = Visibility.Collapsed;
        texto.Text = "";
    }
}
