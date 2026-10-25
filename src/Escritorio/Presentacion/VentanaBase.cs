using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Escritorio.Presentacion;

/// <summary>
/// Ventana con barra de título propia. Conserva el marco nativo para que funcionen
/// Aero Snap, el menú del sistema, las animaciones y Snap Layouts de Windows 11.
/// </summary>
public class VentanaBase : Window
{
    public static readonly DependencyProperty MostrarTituloProperty = DependencyProperty.Register(
        nameof(MostrarTitulo), typeof(bool), typeof(VentanaBase), new PropertyMetadata(true));
    public static readonly DependencyProperty ContenidoBajoTituloProperty = DependencyProperty.Register(
        nameof(ContenidoBajoTitulo), typeof(bool), typeof(VentanaBase), new PropertyMetadata(false));
    // Botones de minimizar, maximizar y cerrar en claro, para pantallas con fondo oscuro como el acceso.
    public static readonly DependencyProperty TituloClaroProperty = DependencyProperty.Register(
        nameof(TituloClaro), typeof(bool), typeof(VentanaBase), new PropertyMetadata(false));
    public bool TituloClaro { get => (bool)GetValue(TituloClaroProperty); set => SetValue(TituloClaroProperty, value); }
    public static readonly DependencyProperty MaximizarResaltadoProperty = DependencyProperty.Register(
        nameof(MaximizarResaltado), typeof(bool), typeof(VentanaBase), new PropertyMetadata(false));
    public static readonly DependencyProperty MaximizarPresionadoProperty = DependencyProperty.Register(
        nameof(MaximizarPresionado), typeof(bool), typeof(VentanaBase), new PropertyMetadata(false));

    public bool MostrarTitulo { get => (bool)GetValue(MostrarTituloProperty); set => SetValue(MostrarTituloProperty, value); }
    public bool ContenidoBajoTitulo { get => (bool)GetValue(ContenidoBajoTituloProperty); set => SetValue(ContenidoBajoTituloProperty, value); }
    public bool MaximizarResaltado { get => (bool)GetValue(MaximizarResaltadoProperty); private set => SetValue(MaximizarResaltadoProperty, value); }
    public bool MaximizarPresionado { get => (bool)GetValue(MaximizarPresionadoProperty); private set => SetValue(MaximizarPresionadoProperty, value); }

    private Button? maximizar;
    private Panel? avisos;
    private readonly bool snapLayouts = Environment.OSVersion.Version.Build >= 22000;

    public VentanaBase()
    {
        SetResourceReference(StyleProperty, typeof(VentanaBase));
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Minimizar") is Button minimizar)
            minimizar.Click += (_, _) => SystemCommands.MinimizeWindow(this);
        maximizar = GetTemplateChild("PART_Maximizar") as Button;
        if (maximizar is not null) maximizar.Click += (_, _) => AlternarMaximizado();
        if (GetTemplateChild("PART_Cerrar") is Button cerrar)
            cerrar.Click += (_, _) => Close();
        avisos = GetTemplateChild("PART_Avisos") as Panel;
    }

    // Al cambiar de persona no deben quedar los avisos de la sesión anterior.
    public void LimpiarAvisos() => avisos?.Children.Clear();

    public void Notificar(string texto, Tono tono = Tono.Exito, string? titulo = null)
    {
        if (avisos is null || string.IsNullOrWhiteSpace(texto)) return;
        while (avisos.Children.Count >= 3) avisos.Children.RemoveAt(0);
        var aviso = new Notificacion(texto, tono, titulo);
        aviso.Cerrada += (_, _) => avisos.Children.Remove(aviso);
        avisos.Children.Add(aviso);
    }

    public static void Notificar(DependencyObject? origen, string texto, Tono tono = Tono.Exito, string? titulo = null)
    {
        if (origen is not null && GetWindow(origen) is VentanaBase ventana) ventana.Notificar(texto, tono, titulo);
    }

    private void AlternarMaximizado()
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        AjustarAPantalla();
        if (PresentationSource.FromVisual(this) is not HwndSource fuente) return;
        // Evita los destellos negros al redimensionar antes de que WPF pinte.
        if (fuente.CompositionTarget is not null && TryFindResource("ColorFondo") is Color fondo)
            fuente.CompositionTarget.BackgroundColor = fondo;
        Nativo.AplicarMarco(fuente.Handle);
        fuente.AddHook(Procesar);
    }

    // En pantallas pequeñas (una Canaima de 1024 × 600) ninguna ventana puede superar el área de trabajo:
    // se achica, y lo que no quepa se desplaza dentro de ella.
    private void AjustarAPantalla()
    {
        var area = Pantalla.Area;
        if (MinWidth > area.Width - 24) MinWidth = area.Width - 24;
        if (MinHeight > area.Height - 24) MinHeight = area.Height - 24;
        if (SizeToContent is SizeToContent.Height or SizeToContent.WidthAndHeight && MaxHeight > area.Height - 24) MaxHeight = area.Height - 24;
        if (SizeToContent is SizeToContent.Width or SizeToContent.WidthAndHeight && MaxWidth > area.Width - 24) MaxWidth = area.Width - 24;
        var ajustada = false;
        if (SizeToContent is SizeToContent.Manual or SizeToContent.Height && !double.IsNaN(Width) && Width > area.Width - 24) { Width = area.Width - 24; ajustada = true; }
        if (SizeToContent == SizeToContent.Manual && !double.IsNaN(Height) && Height > area.Height - 24) { Height = area.Height - 24; ajustada = true; }
        if (!ajustada && Pantalla.Simulada is null) return;
        // WPF ya calculó la posición con el tamaño original; se vuelve a centrar.
        var referencia = WindowStartupLocation == WindowStartupLocation.CenterOwner && Owner is { WindowState: WindowState.Normal }
            ? new Rect(Owner.Left, Owner.Top, Owner.ActualWidth, Owner.ActualHeight) : area;
        Left = Math.Max(area.Left, referencia.Left + (referencia.Width - Width) / 2);
        Top = Math.Max(area.Top, referencia.Top + (referencia.Height - Height) / 2);
        if (Left + Width > area.Right) Left = Math.Max(area.Left, area.Right - Width);
        if (Top + Height > area.Bottom) Top = Math.Max(area.Top, area.Bottom - Height);
    }

    private IntPtr Procesar(IntPtr hwnd, int mensaje, IntPtr wParam, IntPtr lParam, ref bool manejado)
    {
        switch (mensaje)
        {
            case Nativo.WM_GETMINMAXINFO:
                Nativo.AjustarMaximizado(hwnd, lParam);
                break;
            case Nativo.WM_NCHITTEST when snapLayouts:
                var encima = SobreMaximizar(lParam);
                if (MaximizarResaltado != encima) MaximizarResaltado = encima;
                if (encima) { manejado = true; return Nativo.HTMAXBUTTON; }
                break;
            case Nativo.WM_NCMOUSELEAVE:
                MaximizarResaltado = false;
                MaximizarPresionado = false;
                break;
            case Nativo.WM_NCLBUTTONDOWN when snapLayouts && wParam.ToInt32() == Nativo.HTMAXBUTTON:
                MaximizarPresionado = true;
                manejado = true;
                break;
            case Nativo.WM_NCLBUTTONUP when snapLayouts && wParam.ToInt32() == Nativo.HTMAXBUTTON:
                if (MaximizarPresionado) Dispatcher.BeginInvoke(new Action(AlternarMaximizado));
                MaximizarPresionado = false;
                manejado = true;
                break;
        }
        return IntPtr.Zero;
    }

    private bool SobreMaximizar(IntPtr lParam)
    {
        if (maximizar is null || !maximizar.IsVisible || PresentationSource.FromVisual(maximizar) is null) return false;
        var valor = lParam.ToInt64();
        var pantalla = new Point((short)(valor & 0xFFFF), (short)((valor >> 16) & 0xFFFF));
        var punto = maximizar.PointFromScreen(pantalla);
        return punto.X >= 0 && punto.Y >= 0 && punto.X < maximizar.ActualWidth && punto.Y < maximizar.ActualHeight;
    }

    private static class Nativo
    {
        public const int WM_GETMINMAXINFO = 0x0024;
        public const int WM_NCHITTEST = 0x0084;
        public const int WM_NCLBUTTONDOWN = 0x00A1;
        public const int WM_NCLBUTTONUP = 0x00A2;
        public const int WM_NCMOUSELEAVE = 0x02A2;
        public const int HTMAXBUTTON = 9;

        [StructLayout(LayoutKind.Sequential)] private struct Punto { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] private struct MinMax { public Punto Reservado, TamanoMaximo, PosicionMaxima, RastreoMinimo, RastreoMaximo; }
        [StructLayout(LayoutKind.Sequential)] private struct Rectangulo { public int Izquierda, Arriba, Derecha, Abajo; }
        [StructLayout(LayoutKind.Sequential)] private struct InfoMonitor { public int Tamano; public Rectangulo Monitor, Trabajo; public int Opciones; }

        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int opciones);
        [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref InfoMonitor info);
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int atributo, ref int valor, int tamano);

        // Sin esto la ventana maximizada sobresale del monitor y tapa la barra de tareas.
        public static void AjustarMaximizado(IntPtr hwnd, IntPtr lParam)
        {
            var monitor = MonitorFromWindow(hwnd, 2);
            var info = new InfoMonitor { Tamano = Marshal.SizeOf<InfoMonitor>() };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return;
            var datos = Marshal.PtrToStructure<MinMax>(lParam);
            var trabajo = info.Trabajo;
            datos.PosicionMaxima.X = trabajo.Izquierda - info.Monitor.Izquierda;
            datos.PosicionMaxima.Y = trabajo.Arriba - info.Monitor.Arriba;
            datos.TamanoMaximo.X = trabajo.Derecha - trabajo.Izquierda;
            datos.TamanoMaximo.Y = trabajo.Abajo - trabajo.Arriba;
            // Con la barra de tareas oculta se deja un píxel para que pueda aparecer.
            if (trabajo.Abajo == info.Monitor.Abajo && trabajo.Arriba == info.Monitor.Arriba) datos.TamanoMaximo.Y -= 1;
            Marshal.StructureToPtr(datos, lParam, true);
        }

        public static void AplicarMarco(IntPtr hwnd)
        {
            try
            {
                var esquinas = 2;
                DwmSetWindowAttribute(hwnd, 33, ref esquinas, sizeof(int));
                var borde = 0x00E3D8D5;
                DwmSetWindowAttribute(hwnd, 34, ref borde, sizeof(int));
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
    }
}
