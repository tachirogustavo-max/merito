using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Escritorio.Servicios;

namespace Escritorio.Presentacion;

public sealed class OperacionCancelada() : Exception("Operación cancelada.");

public sealed class Orden(Action accion) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parametro) => true;
    public void Execute(object? parametro) => accion();
}

public sealed class Paginacion
{
    public const int Tamano = 100;
    public int Desde { get; set; }
    public int Total { get; private set; }
    public string Buscar { get; set; } = "";
    public event EventHandler? Actualizada;
    public string Consulta => $"desde={Desde}&cantidad={Tamano}&buscar={Uri.EscapeDataString(Buscar)}";

    public List<T> Recibir<T>(Entidades.Pagina<T> pagina)
    {
        Total = pagina.Total;
        Desde = pagina.Desde;
        Actualizada?.Invoke(this, EventArgs.Empty);
        return pagina.Elementos;
    }
}

/// <summary>
/// Base de las pantallas. Las vistas se escriben en XAML con esta clase como raíz; aquí quedan la ejecución
/// de acciones con su manejo de errores, la barra de carga, los atajos y el buscador de cada tabla.
/// </summary>
public class VistaBase : UserControl
{
    protected readonly ClienteApi Api;
    private ProgressBar? carga;
    private readonly DispatcherTimer demora = new() { Interval = TimeSpan.FromMilliseconds(220) };
    private bool notificada;
    private FranjaAviso? franja;

    private readonly List<TextBox> buscadores = [];
    protected UIElement? FocoInicial { get; set; }

    public bool Ocupada { get; private set; }
    public string? UltimoError { get; private set; }
    protected Window Propietario => Window.GetWindow(this);

    // Solo para el diseñador de Visual Studio, que necesita crear la clase raíz sin argumentos.
    public VistaBase() : this(null!) { }

    protected VistaBase(ClienteApi api)
    {
        Api = api;
        SetResourceReference(TemplateProperty, "PlantillaVista");
        demora.Tick += (_, _) => MostrarCarga();
        // Bloquea la interacción sin deshabilitar los controles, para que nada parpadee.
        // El buscador queda fuera del bloqueo: escribir mientras llega una página no debe perder letras.
        PreviewMouseDown += (_, e) => { if (Ocupada && !EnBuscador(e.OriginalSource)) e.Handled = true; };
        PreviewKeyDown += (_, e) => { if (Ocupada && !EnBuscador(e.OriginalSource)) e.Handled = true; };
        Unloaded += (_, _) => demora.Stop();
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        carga = GetTemplateChild("PART_Carga") as ProgressBar;
    }

    /// <summary>La franja de estado de la vista: la que declara el XAML o, si no hay, una nueva para colocar en código.</summary>
    protected FranjaAviso Aviso => franja ??= Buscar<FranjaAviso>(this) ?? new FranjaAviso();

    private static T? Buscar<T>(DependencyObject raiz) where T : DependencyObject
    {
        foreach (var hijo in LogicalTreeHelper.GetChildren(raiz).OfType<DependencyObject>())
        {
            if (hijo is T encontrado) return encontrado;
            if (Buscar<T>(hijo) is { } profundo) return profundo;
        }
        return null;
    }

    private bool EnBuscador(object? origen)
    {
        for (var elemento = origen as DependencyObject; elemento is not null;
             elemento = elemento is Visual ? VisualTreeHelper.GetParent(elemento) : LogicalTreeHelper.GetParent(elemento))
            if (elemento is TextBox caja && buscadores.Contains(caja)) return true;
        return false;
    }

    protected void Componer(UIElement cuerpo) => Content = cuerpo;

    protected void Notificar(string texto, Tono tono = Tono.Exito, string? titulo = null)
    {
        notificada = true;
        VentanaBase.Notificar(this, texto, tono, titulo);
    }

    protected void MarcarEstado(string texto, Tono tono = Tono.Aviso) => Aviso.Mostrar(texto, tono);
    protected void LimpiarEstado() => Aviso.Ocultar();

    private void MostrarCarga()
    {
        // Mientras un diálogo propio está abierto no se muestra el indicador.
        if (Propietario is { IsActive: false }) return;
        demora.Stop();
        if (carga is not null) carga.Visibility = Visibility.Visible;
    }

    /// <param name="exito">Notificación al terminar bien, si la acción no avisó por su cuenta.</param>
    protected async Task Ejecutar(Func<Task> accion, string? exito = null)
    {
        if (Ocupada) return;
        Ocupada = true;
        UltimoError = null;
        notificada = false;
        Cursor = Cursors.AppStarting;
        demora.Start();
        try
        {
            await accion();
            if (exito is not null && !notificada) Notificar(exito, Tono.Neutro);
        }
        catch (Exception error)
        {
            demora.Stop();
            if (carga is not null) carga.Visibility = Visibility.Collapsed;
            UltimoError = Errores.Presentar(Propietario, error);
        }
        finally
        {
            demora.Stop();
            if (carga is not null) carga.Visibility = Visibility.Collapsed;
            Cursor = null;
            Ocupada = false;
        }
    }

    protected static Rejilla Tabla(params (string Titulo, string Campo)[] columnas)
    {
        var tabla = new Rejilla();
        tabla.Definir(columnas);
        return tabla;
    }

    /// <summary>
    /// Conecta un panel de tabla con su búsqueda: en el servidor si hay paginación, si no sobre las filas cargadas.
    /// Coloca en el pie el paginador o el conteo de registros.
    /// </summary>
    protected void Registrar(PanelTabla panel, Paginacion? paginacion = null, Func<Task>? recargar = null, Action<string>? filtrar = null, UIElement? estado = null)
    {
        var tabla = panel.Tabla ?? throw new InvalidOperationException("El panel no tiene tabla.");
        var aplicar = paginacion is not null && recargar is not null ? BusquedaEnServidor(paginacion, recargar)
            : filtrar ?? (tabla is Rejilla rejilla ? rejilla.Filtrar : null);
        if (panel.Buscar is { Length: > 0 } && aplicar is not null)
        {
            panel.Buscador.TextChanged += (_, _) => aplicar(panel.Buscador.Text);
            buscadores.Add(panel.Buscador);
        }
        panel.Pie(paginacion is not null && recargar is not null ? Paginador(paginacion, recargar) : Conteo(tabla), estado);
    }

    protected PanelTabla PanelTabla(DataGrid tabla, UIElement acciones, UIElement? estado = null, string? buscar = null,
        Action<string>? filtrar = null, Paginacion? paginacion = null, Func<Task>? recargar = null)
    {
        var panel = new PanelTabla { Acciones = acciones, Tabla = tabla, Buscar = buscar };
        Registrar(panel, paginacion, recargar, filtrar, estado);
        return panel;
    }

    private UIElement Conteo(DataGrid tabla)
    {
        var cuenta = new TextBlock { Style = (Style)FindResource("TextoNota"), Margin = new Thickness(2, 10, 0, 0) };
        cuenta.SetBinding(TextBlock.TextProperty, new Binding("Items.Count") { Source = tabla, Converter = new ConvertidorCuenta() });
        return cuenta;
    }

    // Espera a que se deje de escribir para no consultar al servidor con cada tecla.
    private Action<string> BusquedaEnServidor(Paginacion paginacion, Func<Task> recargar)
    {
        var espera = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        espera.Tick += async (_, _) =>
        {
            espera.Stop();
            if (Ocupada) { espera.Start(); return; }
            paginacion.Desde = 0;
            await Ejecutar(recargar);
        };
        Unloaded += (_, _) => espera.Stop();
        return texto =>
        {
            paginacion.Buscar = texto.Trim();
            espera.Stop();
            espera.Start();
        };
    }

    private UIElement Paginador(Paginacion paginacion, Func<Task> recargar)
    {
        var rango = new TextBlock { Style = (Style)FindResource("TextoNota"), VerticalAlignment = VerticalAlignment.Center };
        Button Paso(string texto, string icono, bool siguiente)
        {
            var contenido = new StackPanel { Orientation = Orientation.Horizontal };
            var simbolo = Icono.De(icono, 15);
            var rotulo = new TextBlock { Text = texto, VerticalAlignment = VerticalAlignment.Center };
            if (siguiente) { contenido.Children.Add(rotulo); simbolo.Margin = new Thickness(6, 0, 0, 0); contenido.Children.Add(simbolo); }
            else { simbolo.Margin = new Thickness(0, 0, 6, 0); contenido.Children.Add(simbolo); contenido.Children.Add(rotulo); }
            var boton = new Button { Content = contenido, MinHeight = 32, Padding = new Thickness(10, 0, 12, 0), Margin = new Thickness(8, 0, 0, 0) };
            boton.SetResourceReference(StyleProperty, "BotonSecundario");
            System.Windows.Automation.AutomationProperties.SetName(boton, texto == "Anterior" ? "Página anterior" : "Página siguiente");
            boton.Click += async (_, _) =>
            {
                paginacion.Desde = Math.Max(0, paginacion.Desde + (siguiente ? Paginacion.Tamano : -Paginacion.Tamano));
                await Ejecutar(recargar);
            };
            return boton;
        }
        var anterior = Paso("Anterior", "IcoChevronIzquierda", false);
        var siguiente = Paso("Siguiente", "IcoChevronDerecha", true);
        void Mostrar()
        {
            rango.Text = paginacion.Total == 0 ? "Sin registros"
                : $"{paginacion.Desde + 1:N0} a {Math.Min(paginacion.Desde + Paginacion.Tamano, paginacion.Total):N0} de {paginacion.Total:N0} registros";
            // Los botones que no aplican se ocultan en lugar de verse deshabilitados.
            anterior.Visibility = paginacion.Desde > 0 ? Visibility.Visible : Visibility.Collapsed;
            siguiente.Visibility = paginacion.Desde + Paginacion.Tamano < paginacion.Total ? Visibility.Visible : Visibility.Collapsed;
        }
        paginacion.Actualizada += (_, _) => Mostrar();
        Mostrar();
        var botones = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        botones.Children.Add(anterior);
        botones.Children.Add(siguiente);
        var fila = new DockPanel { Margin = new Thickness(2, 8, 0, 0), MinHeight = 32 };
        DockPanel.SetDock(botones, Dock.Right);
        fila.Children.Add(botones);
        fila.Children.Add(rango);
        return fila;
    }

    /// <summary>
    /// Recarga sola al volver a la ventana, para ver lo que cambiaron otros (una nota publicada, una matrícula confirmada).
    /// Sin avisos: si el servidor no responde, se vuelve a intentar la próxima vez. sePuede evita perder lo que se está editando.
    /// </summary>
    protected void RecargarSolo(Func<Task> recargar, Func<bool>? sePuede = null)
    {
        Window? ventana = null;
        var ultima = DateTime.UtcNow;
        async void Recargar(object? origen, EventArgs e)
        {
            if (Ocupada || !IsVisible || sePuede?.Invoke() == false || DateTime.UtcNow - ultima < TimeSpan.FromSeconds(5)) return;
            ultima = DateTime.UtcNow;
            Ocupada = true;
            try { await recargar(); }
            catch (Exception error) when (error is ErrorServicio or Logica.ReglaException or ArgumentException or InvalidOperationException or TaskCanceledException) { }
            finally { Ocupada = false; }
        }
        Loaded += (_, _) => { ventana = Window.GetWindow(this); if (ventana is not null) ventana.Activated += Recargar; };
        Unloaded += (_, _) => { if (ventana is not null) ventana.Activated -= Recargar; };
    }

    /// <summary>Atajo de teclado activo mientras el foco está dentro de la vista.</summary>
    protected void Atajo(Key tecla, ModifierKeys modificadores, Func<Task> accion, string? exito = null) =>
        InputBindings.Add(new KeyBinding(new Orden(() => _ = Ejecutar(accion, exito)), tecla, modificadores));

    /// <summary>Ctrl+B: lleva el foco al buscador visible y selecciona lo escrito.</summary>
    protected void HabilitarBusqueda() => InputBindings.Add(new KeyBinding(new Orden(() =>
    {
        if (buscadores.FirstOrDefault(b => b.IsVisible) is { } buscador) { buscador.Focus(); buscador.SelectAll(); }
    }), Key.B, ModifierKeys.Control));

    protected void EnfocarInicio()
    {
        if (FocoInicial is Rejilla rejilla) rejilla.Enfocar();
        else FocoInicial?.Focus();
    }

    protected static void Confirmar(string texto, Window propietario, string titulo = "Confirmar operación", string aceptar = "Continuar", bool peligro = false)
    {
        if (!Dialogo.Preguntar(propietario, titulo, texto, aceptar, peligro)) throw new OperacionCancelada();
    }
}
