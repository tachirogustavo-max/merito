using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace Escritorio.Presentacion;

public sealed class Notificacion : Grid
{
    private readonly DispatcherTimer reloj;
    private readonly TranslateTransform desplazamiento = new(28, 0);
    private bool cerrando;
    public event EventHandler? Cerrada;

    public Notificacion(string texto, Tono tono, string? titulo)
    {
        Width = 360;
        Margin = new Thickness(0, 0, 0, 10);
        RenderTransform = desplazamiento;
        Opacity = 0;
        var (fondo, color) = Textos.Pinceles(tono);
        Brush Recurso(string clave) => (Brush)Application.Current.FindResource(clave);

        // La sombra va en un elemento aparte para que el texto no pierda nitidez.
        Children.Add(new Border
        {
            CornerRadius = new CornerRadius(12), Background = Recurso("Superficie"), Margin = new Thickness(2),
            Effect = new DropShadowEffect { BlurRadius = 22, ShadowDepth = 6, Direction = 270, Opacity = 0.16, Color = (Color)Application.Current.FindResource("ColorSombra") }
        });

        var simbolo = Icono.De(tono switch { Tono.Exito => "IcoCheck", Tono.Peligro => "IcoAlerta", Tono.Aviso => "IcoAlerta", _ => "IcoInfo" }, 17);
        TextElement.SetForeground(simbolo, color);
        var circulo = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Background = fondo, Child = simbolo, VerticalAlignment = VerticalAlignment.Top };

        var textos = new StackPanel { Margin = new Thickness(12, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        if (titulo is not null)
            textos.Children.Add(new TextBlock { Text = titulo, FontWeight = FontWeights.SemiBold, Foreground = Recurso("Texto"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 2) });
        textos.Children.Add(new TextBlock { Text = texto, Foreground = Recurso(titulo is null ? "Texto" : "Texto2"), TextWrapping = TextWrapping.Wrap, LineHeight = 20, FontSize = 13.5 });

        var cerrar = new Button { Style = (Style)Application.Current.FindResource("BotonIcono"), VerticalAlignment = VerticalAlignment.Top, ToolTip = "Cerrar", Content = Icono.De("IcoCerrar", 14) };
        System.Windows.Automation.AutomationProperties.SetName(cerrar, "Cerrar notificación");
        cerrar.Click += (_, _) => Cerrar();

        var fila = new DockPanel();
        DockPanel.SetDock(circulo, Dock.Left);
        DockPanel.SetDock(cerrar, Dock.Right);
        fila.Children.Add(circulo);
        fila.Children.Add(cerrar);
        fila.Children.Add(textos);
        var franja = new Border { Width = 3, CornerRadius = new CornerRadius(1.5), Background = color, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(4, 16, 0, 16) };
        Children.Add(new Border
        {
            CornerRadius = new CornerRadius(12), Background = Recurso("Superficie"), BorderBrush = Recurso("Borde"), BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 12, 8, 12), Margin = new Thickness(2), Child = fila
        });
        Children.Add(franja);

        reloj = new DispatcherTimer { Interval = TimeSpan.FromSeconds(tono is Tono.Peligro or Tono.Aviso ? 7 : 4.5) };
        reloj.Tick += (_, _) => Cerrar();
        MouseEnter += (_, _) => reloj.Stop();
        MouseLeave += (_, _) => { if (!cerrando) reloj.Start(); };
        Loaded += (_, _) =>
        {
            Animar(1, 0, 240);
            reloj.Start();
        };
        Unloaded += (_, _) => reloj.Stop();
    }

    private void Animar(double opacidad, double x, int milisegundos, EventHandler? fin = null)
    {
        var duracion = TimeSpan.FromMilliseconds(milisegundos);
        var suave = new CubicEase { EasingMode = EasingMode.EaseOut };
        var opaco = new DoubleAnimation(opacidad, duracion) { EasingFunction = suave };
        if (fin is not null) opaco.Completed += fin;
        BeginAnimation(OpacityProperty, opaco);
        desplazamiento.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(x, duracion) { EasingFunction = suave });
    }

    public void Cerrar()
    {
        if (cerrando) return;
        cerrando = true;
        reloj.Stop();
        IsHitTestVisible = false;
        Animar(0, 28, 180, (_, _) => Cerrada?.Invoke(this, EventArgs.Empty));
    }
}
