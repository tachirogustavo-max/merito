using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Escritorio.Presentacion;

public sealed class Formulario : VentanaBase
{
    private readonly StackPanel campos = new();

    public Formulario(Window propietario, string titulo, string descripcion, double ancho = 480)
    {
        if (propietario is { IsLoaded: true, IsVisible: true }) { Owner = propietario; WindowStartupLocation = WindowStartupLocation.CenterOwner; }
        else WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Title = titulo;
        Width = ancho;
        MaxHeight = SystemParameters.WorkArea.Height - 60;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        MostrarTitulo = false;

        var cuerpo = new StackPanel { Margin = new Thickness(30, 0, 30, 26) };
        cuerpo.Children.Add(new TextBlock { Text = titulo, Style = (Style)FindResource("TituloDialogo"), FontSize = 21 });
        cuerpo.Children.Add(new TextBlock { Text = descripcion, Style = (Style)FindResource("TextoSecundario"), Margin = new Thickness(0, 8, 0, 24) });
        cuerpo.Children.Add(campos);

        var aceptar = new Button { Content = "Continuar", IsDefault = true, MinWidth = 110 };
        aceptar.Click += (_, _) => DialogResult = true;
        var cancelar = new Button { Content = "Cancelar", IsCancel = true, MinWidth = 104, Style = (Style)FindResource("BotonSecundario"), Margin = new Thickness(0, 0, 10, 0) };
        var botones = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        botones.Children.Add(cancelar);
        botones.Children.Add(aceptar);
        var pie = new Border { BorderBrush = (System.Windows.Media.Brush)FindResource("Borde"), BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 20, 0, 0), Margin = new Thickness(0, 10, 0, 0), Child = botones };
        cuerpo.Children.Add(pie);

        Content = new ScrollViewer { Content = cuerpo, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false };
        Loaded += (_, _) =>
        {
            var primero = campos.Children.OfType<Control>().FirstOrDefault();
            if (primero is ListaBuscable lista) lista.Caja.Focus(); else primero?.Focus();
        };
    }

    public TextBox Texto(string etiqueta, string inicial = "", string? ayuda = null)
    {
        var campo = new TextBox { Text = inicial, MaxLength = 128 };
        Agregar(etiqueta, campo, ayuda);
        return campo;
    }

    public PasswordBox Clave(string etiqueta, string? ayuda = null)
    {
        var campo = new PasswordBox { MaxLength = 128 };
        Agregar(etiqueta, campo, ayuda);
        return campo;
    }

    public ListaBuscable Seleccion<T>(string etiqueta, IEnumerable<T> opciones, string campoVisible, T? inicial = default, string? ayuda = null)
    {
        var campo = new ListaBuscable { DisplayMemberPath = campoVisible };
        campo.ItemsSource = opciones.ToList();
        if (inicial is not null) campo.SelectedItem = inicial;
        Agregar(etiqueta, campo, ayuda);
        return campo;
    }

    /// <summary>Dos campos cortos en un mismo renglón, como «desde» y «hasta».</summary>
    public (TextBox Primero, TextBox Segundo) Par(string etiqueta1, string inicial1, string etiqueta2, string inicial2)
    {
        var renglon = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        renglon.ColumnDefinitions.Add(new ColumnDefinition());
        renglon.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        renglon.ColumnDefinitions.Add(new ColumnDefinition());
        TextBox Campo(string etiqueta, string inicial, int columna)
        {
            var caja = new TextBox { Text = inicial, MaxLength = 128 };
            AutomationProperties.SetName(caja, etiqueta);
            var panel = new StackPanel();
            panel.Children.Add(new Label { Content = etiqueta, Target = caja });
            panel.Children.Add(caja);
            Grid.SetColumn(panel, columna);
            renglon.Children.Add(panel);
            return caja;
        }
        var primero = Campo(etiqueta1, inicial1, 0);
        var segundo = Campo(etiqueta2, inicial2, 2);
        campos.Children.Add(renglon);
        return (primero, segundo);
    }

    /// <summary>Texto destacado que la pantalla puede ir cambiando, por ejemplo la vista previa de un código.</summary>
    public TextBlock Vista(string texto = "")
    {
        var vista = new TextBlock { Text = texto, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 0, 0, 18) };
        vista.SetResourceReference(TextBlock.ForegroundProperty, "Indigo700");
        campos.Children.Add(vista);
        return vista;
    }

    private void Agregar(string etiqueta, Control control, string? ayuda)
    {
        var destino = control is ListaBuscable buscable ? (Control)buscable.Caja : control;
        campos.Children.Add(new Label { Content = etiqueta, Target = destino });
        AutomationProperties.SetName(destino, etiqueta);
        control.Margin = new Thickness(0, 0, 0, ayuda is null ? 18 : 6);
        campos.Children.Add(control);
        if (ayuda is not null)
            campos.Children.Add(new TextBlock { Text = ayuda, Style = (Style)FindResource("TextoNota"), Margin = new Thickness(2, 0, 0, 18) });
    }

    public static T Elegido<T>(ListaBuscable campo) => campo.SelectedItem is T seleccionado ? seleccionado
        : throw new ArgumentException("Selecciona todos los campos requeridos.");
}
