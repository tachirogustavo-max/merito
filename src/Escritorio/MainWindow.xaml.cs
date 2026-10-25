using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Entidades;
using Escritorio.Presentacion;
using Escritorio.Servicios;
using Logica;

namespace Escritorio;

public partial class MainWindow : VentanaBase
{
    private readonly ClienteApi api;
    private bool ocupada;
    private Preferencias preferencias = Preferencias.Cargar();
    private string servidor;

    public MainWindow() : this(new ClienteApi(), propio: true) { }

    // Solo se desecha al cerrar el cliente que la ventana creó; uno recibido sigue siendo de quien lo pasó.
    internal MainWindow(ClienteApi cliente, bool propio = false)
    {
        api = cliente;
        if (propio) Closed += (_, _) => api.Dispose();
        InitializeComponent();
        TituloClaro = true;
        // En pantallas chicas (laptops, Canaima) abre ocupando el área de trabajo.
        var area = Pantalla.Area;
        if (area.Width < Width + 40 || area.Height < Height + 40)
        {
            Width = area.Width;
            Height = area.Height;
            if (Pantalla.Simulada is null) WindowState = WindowState.Maximized;
        }
        SizeChanged += (_, _) => Acomodar();
        servidor = Environment.GetEnvironmentVariable("ACADEMICO_URL_SERVIDOR") ?? preferencias.Servidor ?? Preferencias.ServidorDelEquipo() ?? "http://127.0.0.1:5187";
        RecordarCedula.IsChecked = preferencias.RecordarCedula;
        if (preferencias.RecordarCedula && !string.IsNullOrEmpty(preferencias.Cedula)) Cedula.Text = Cedulas.Mostrar(preferencias.Cedula);
        // Al cambiar el tamaño la curva se rehace; si ya terminó de dibujarse, queda completa.
        Acceso.SizeChanged += (_, _) =>
        {
            if (!IsLoaded) return;
            TrazarCurva();
            if (pasoTrazo is null) Dibujar(1);
        };
        Loaded += async (_, _) => { AnimarAcceso(); (Cedula.Text.Length > 0 ? (UIElement)Clave : Cedula).Focus(); await RevisarConfiguracion(); };
        // La dirección la deja el instalador; si el servidor cambia de IP, sistemas la corrige con Ctrl+Mayús+S.
        InputBindings.Add(new KeyBinding(new Orden(CambiarServidor), Key.S, ModifierKeys.Control | ModifierKeys.Shift));
        InputBindings.Add(new KeyBinding(new Orden(MostrarAtajos), Key.F1, ModifierKeys.None));
        for (var i = 0; i < 9; i++)
        {
            var indice = i;
            InputBindings.Add(new KeyBinding(new Orden(() => IrASeccion(indice)), Key.D1 + i, ModifierKeys.Control));
        }
        Clave.KeyUp += (_, _) => RevisarMayusculas();
        Clave.GotKeyboardFocus += (_, _) => RevisarMayusculas();
        Clave.LostKeyboardFocus += (_, _) => AvisoMayusculas.Visibility = Visibility.Collapsed;
        api.SesionVencida += (_, _) => Dispatcher.BeginInvoke(new Action(SesionTerminada));
        Closing += (_, e) =>
        {
            if (!ocupada) return;
            e.Cancel = true;
            Dialogo.Informar(this, "Operación en curso", "Espera a que termine la operación antes de cerrar.");
        };
    }

    // Animación de entrada del acceso.
    private EventHandler? pasoTrazo;
    private PathGeometry geometriaTrazo = new();
    private double unidadesTrazo;

    // La curva se calcula con el tamaño real de la ventana y termina junto a la tarjeta.
    private void TrazarCurva()
    {
        var esquina = TarjetaAcceso.TranslatePoint(new Point(0, 0), Acceso).X;
        var (x, alto) = (esquina > 0 ? esquina : Acceso.ActualWidth * 0.6, Acceso.ActualHeight);
        string P(double px, double py) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{px:0.#},{py:0.#}");
        Trazo.Data = Geometry.Parse($"M {P(-76, alto - 2)} C {P(x * 0.33, alto - 15)} {P(x * 0.6, alto * 0.93)} {P(x * 0.74, alto * 0.75)} " +
            $"S {P(x * 0.885, alto * 0.33)} {P(x - 26, alto * 0.21)}");
        geometriaTrazo = Trazo.Data.GetFlattenedPathGeometry();
        var largo = 0.0;
        foreach (var figura in geometriaTrazo.Figures)
        {
            var anterior = figura.StartPoint;
            foreach (var segmento in figura.Segments.OfType<PolyLineSegment>())
                foreach (var punto in segmento.Points) { largo += (punto - anterior).Length; anterior = punto; }
        }
        unidadesTrazo = largo / Trazo.StrokeThickness + 2;
        Trazo.StrokeDashArray = [unidadesTrazo, unidadesTrazo];
    }

    private void Dibujar(double avance)
    {
        Trazo.StrokeDashOffset = unidadesTrazo * (1 - avance);
        geometriaTrazo.GetPointAtFractionLength(Math.Clamp(avance, 0.0001, 1), out var punta, out _);
        Canvas.SetLeft(Punta, punta.X - Punta.Width / 2);
        Canvas.SetTop(Punta, punta.Y - Punta.Height / 2);
        // La punta se enciende cuando el trazo sale de la zona en sombra.
        Punta.Opacity = Math.Clamp((avance - 0.12) * 5, 0, 1);
    }

    private void AnimarAcceso()
    {
        if (pasoTrazo is not null) { CompositionTarget.Rendering -= pasoTrazo; pasoTrazo = null; }
        TrazarCurva();
        // Con las animaciones de Windows desactivadas, el trazo aparece ya dibujado.
        if (!SystemParameters.ClientAreaAnimation) { Dibujar(1); return; }
        Aparecer(Titular, 180, 16);
        Aparecer(TarjetaAcceso, 320, 12);
        Dibujar(0);
        var reloj = System.Diagnostics.Stopwatch.StartNew();
        pasoTrazo = (_, _) =>
        {
            var t = Math.Clamp((reloj.Elapsed.TotalMilliseconds - 420) / 1750, 0, 1);
            Dibujar(t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2);
            if (t >= 1 && pasoTrazo is not null) { CompositionTarget.Rendering -= pasoTrazo; pasoTrazo = null; }
        };
        CompositionTarget.Rendering += pasoTrazo;
    }
    private static void Aparecer(FrameworkElement elemento, int espera, double desplazamiento)
    {
        var suave = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duracion = TimeSpan.FromMilliseconds(700);
        var inicio = TimeSpan.FromMilliseconds(espera);
        elemento.Opacity = 0;
        elemento.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duracion) { BeginTime = inicio, EasingFunction = suave });
        if (elemento.RenderTransform is TranslateTransform mover)
            mover.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(desplazamiento, 0, duracion) { BeginTime = inicio, EasingFunction = suave });
    }

    /// <summary>
    /// Reparte el espacio según el tamaño: con poco ancho la barra lateral queda solo con iconos y los márgenes se achican;
    /// en el acceso, la tarjeta queda sola y centrada si no cabe junto al titular.
    /// </summary>
    private void Acomodar()
    {
        var compacta = ActualWidth < 1180;
        ColumnaLateral.Width = new GridLength(compacta ? 76 : 256);
        Lateral.Margin = compacta ? new Thickness(10, 20, 10, 12) : new Thickness(16, 20, 16, 16);
        TextoMarca.Visibility = DatosCuenta.Visibility = FlechaCuenta.Visibility = compacta ? Visibility.Collapsed : Visibility.Visible;
        foreach (var boton in Navegacion.Children.OfType<ToggleButton>())
            if (boton.Content is StackPanel { Children.Count: > 1 } contenido) contenido.Children[1].Visibility = compacta ? Visibility.Collapsed : Visibility.Visible;
        var margen = ActualWidth < 1000 ? 18 : compacta ? 24 : 36;
        Cabecera.Margin = new Thickness(margen, 0, margen, compacta ? 14 : 22);
        Contenido.Margin = new Thickness(margen, 0, margen, compacta ? 14 : 26);

        var angosta = ActualWidth < 980;
        var baja = ActualHeight < 660;
        Titular.Visibility = LienzoTrazo.Visibility = angosta ? Visibility.Collapsed : Visibility.Visible;
        Grid.SetColumn(TarjetaAcceso, angosta ? 0 : 1);
        Grid.SetColumnSpan(TarjetaAcceso, angosta ? 2 : 1);
        TarjetaAcceso.HorizontalAlignment = angosta ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        // Arriba quedan siempre libres los 40 px de los botones de la ventana.
        MarcoAcceso.Margin = baja ? new Thickness(angosta ? 24 : 48, 46, angosta ? 24 : 48, 14) : new Thickness(72, 56, 72, 40);
        ContenidoTarjeta.Margin = baja ? new Thickness(32, 22, 32, 20) : new Thickness(40, 38, 40, 34);
        // La tarjeta nunca pasa del alto disponible: lo que no quepa se desplaza dentro de ella.
        DesplazarAcceso.MaxHeight = Math.Max(240, ActualHeight - MarcoAcceso.Margin.Top - MarcoAcceso.Margin.Bottom - 8);
    }

    private void RevisarMayusculas() =>
        AvisoMayusculas.Visibility = Keyboard.IsKeyToggled(Key.CapsLock) ? Visibility.Visible : Visibility.Collapsed;

    private void MostrarMensaje(string texto, Tono tono)
    {
        if (string.IsNullOrWhiteSpace(texto)) { Alerta.Visibility = Visibility.Collapsed; return; }
        var (fondo, color) = Textos.Pinceles(tono);
        Alerta.Background = fondo;
        Alerta.BorderBrush = tono == Tono.Peligro ? (Brush)FindResource("ErrorBorde") : fondo;
        MensajeAcceso.Foreground = color;
        MensajeAcceso.Text = texto;
        IconoAlerta.Datos = (Geometry)FindResource(tono switch { Tono.Peligro => "IcoError", Tono.Exito => "IcoExito", _ => "IcoInfo" });
        TextElement.SetForeground(IconoAlerta, color);
        Alerta.Visibility = Visibility.Visible;
    }

    private async void IniciarSesion(object sender, RoutedEventArgs e)
    {
        if (ocupada) return;
        ocupada = true;
        // El botón no se deshabilita: cambia su texto y los clics repetidos se ignoran.
        Entrar.Content = "Conectando…";
        MostrarMensaje("", Tono.Neutro);
        try
        {
            servidor = Normalizar(servidor);
            await api.Iniciar(servidor, Cedula.Text, Clave.Password);
            Clave.Clear();
            var recordar = RecordarCedula.IsChecked == true;
            // La dirección no se guarda aquí: así un cambio de servidor hecho desde el instalador llega a todos los usuarios del equipo.
            preferencias = preferencias with { Cedula = recordar ? api.Persona!.Cedula : null, RecordarCedula = recordar };
            preferencias.Guardar();
            MostrarAplicacion();
        }
        catch (Exception error) when (error is ErrorServicio or ArgumentException or ReglaException)
        {
            MostrarMensaje(error.Message, Tono.Peligro);
        }
        finally { ocupada = false; Entrar.Content = "Iniciar sesión"; }
    }

    // Solo para sistemas, cuando el servidor cambia de dirección.
    private async void CambiarServidor()
    {
        if (ocupada || Acceso.Visibility != Visibility.Visible) return;
        var formulario = new Formulario(this, "Servidor de Mérito", "La dirección del equipo servidor, por ejemplo http://192.168.1.10:5187. La proporciona el área de sistemas.");
        var direccion = formulario.Texto("Dirección del servidor", servidor);
        if (formulario.ShowDialog() != true) return;
        ocupada = true;
        MostrarMensaje("Comprobando la conexión…", Tono.Neutro);
        try
        {
            var nueva = Normalizar(direccion.Text);
            await api.Probar(nueva);
            servidor = nueva;
            preferencias = preferencias with { Servidor = servidor.TrimEnd('/') };
            preferencias.Guardar();
            MostrarMensaje($"Conectado con el servidor de Mérito en {new Uri(servidor).Authority}.", Tono.Exito);
            await RevisarConfiguracion();
        }
        catch (Exception error) when (error is ErrorServicio or ArgumentException or ReglaException or UriFormatException) { MostrarMensaje(error.Message, Tono.Peligro); }
        finally { ocupada = false; }
    }

    /// <summary>Un servidor recién instalado no tiene administración: se ofrece crearla con el código.</summary>
    private async Task RevisarConfiguracion()
    {
        try
        {
            using var consulta = new ClienteApi();
            consulta.Configurar(Normalizar(servidor));
            var estado = await consulta.Obtener<EstadoConfiguracion>("configuracion");
            AvisoConfiguracion.Visibility = estado.Pendiente ? Visibility.Visible : Visibility.Collapsed;
            PorCorreo.Visibility = estado.CorreoDisponible ? Visibility.Visible : Visibility.Collapsed;
            ConCodigo.Content = estado.CorreoDisponible ? "Tengo un código de la universidad" : "Activar cuenta o recuperar acceso";
        }
        catch (Exception error) when (error is ErrorServicio or ArgumentException or UriFormatException) { AvisoConfiguracion.Visibility = Visibility.Collapsed; }
    }

    private async void ConfigurarMerito(object sender, RoutedEventArgs e)
    {
        if (ocupada) return;
        var formulario = new Formulario(this, "Configurar Mérito",
            "Crea la cuenta de administración. Necesitas el código que mostró la instalación del servidor; también está en C:\\ProgramData\\Merito\\privado\\codigo-configuracion.txt de ese equipo.", 520);
        var codigo = formulario.Texto("Código de configuración", "", "16 letras y números; los guiones son opcionales.");
        var cedula = formulario.Texto("Cédula", Cedula.Text);
        var nombres = formulario.Texto("Nombres");
        var apellidos = formulario.Texto("Apellidos");
        var nueva = formulario.Clave("Contraseña", "Mínimo 12 caracteres.");
        var repetir = formulario.Clave("Repetir contraseña");
        if (formulario.ShowDialog() != true) return;
        ocupada = true;
        try
        {
            Seguridad.ConfirmarClave(nueva.Password, repetir.Password);
            api.Configurar(Normalizar(servidor));
            await api.Enviar<long>(HttpMethod.Post, "configuracion", new ConfiguracionInicial(codigo.Text, cedula.Text, nombres.Text, apellidos.Text, nueva.Password));
            AvisoConfiguracion.Visibility = Visibility.Collapsed;
            Cedula.Text = cedula.Text.Trim();
            Clave.Clear();
            Clave.Focus();
            MostrarMensaje("Mérito quedó configurado. Inicia sesión con la cuenta de administración que acabas de crear.", Tono.Exito);
        }
        catch (Exception error) when (error is ErrorServicio or ArgumentException or ReglaException) { MostrarMensaje(error.Message, Tono.Peligro); }
        finally { ocupada = false; }
    }

    // Quien escribe solo la IP («192.168.1.10») recibe la dirección completa con el puerto habitual.
    private static string Normalizar(string direccion)
    {
        var texto = direccion.Trim();
        if (texto.Length == 0) return texto;
        if (!texto.Contains("://", StringComparison.Ordinal)) texto = "http://" + texto;
        return Uri.TryCreate(texto, UriKind.Absolute, out var uri) && uri.IsDefaultPort && !texto.Contains(":" + uri.Port, StringComparison.Ordinal) && uri.Scheme == "http"
            ? $"{uri.Scheme}://{uri.Host}:5187" : texto.TrimEnd('/');
    }

    internal void MostrarAplicacion()
    {
        var persona = api.Persona!;
        Acceso.Visibility = Visibility.Collapsed;
        Aplicacion.Visibility = Visibility.Visible;
        TituloClaro = false;
        NombrePersona.Text = persona.NombreCompleto;
        RolPersona.Text = Textos.Capitalizar(persona.Rol);
        Iniciales.Text = Textos.Iniciales(persona.NombreCompleto);
        MenuNombre.Text = persona.NombreCompleto;
        MenuDetalle.Text = $"{Textos.Capitalizar(persona.Rol)}, {Cedulas.Mostrar(persona.Cedula)}";
        Navegacion.Children.Clear();
        if (persona.Rol == Roles.Administrador)
        {
            AgregarNavegacion("IcoPanel", "Administración", () => Mostrar("Administración", "Cuentas, estructura académica, carga académica y control de períodos.", new VistaAdministracion(api)));
            AgregarNavegacion("IcoLibro", "Materias y matrícula", () => Mostrar("Materias y matrícula", "Abre la planilla de cada materia y confirma los listados de estudiantes.", new VistaOfertas(api)));
        }
        else if (persona.Rol == Roles.Docente)
            AgregarNavegacion("IcoLibro", "Mis materias", () => Mostrar("Mis materias", "Registra las notas, guarda borradores y publica avances cuando estén listos.", new VistaOfertas(api)));
        else
            AgregarNavegacion("IcoRecord", "Mi récord", () => Mostrar("Mi récord académico", "Las notas aparecen cuando el docente las publica.", new VistaRecord(api)));
        if (Navegacion.Children[0] is ToggleButton primero) Seleccionar(primero);
        Acomodar();
    }

    private void VerAtajos(object sender, RoutedEventArgs e) { MenuCuenta.IsOpen = false; MostrarAtajos(); }

    // El menú se abre junto a lo que se pulsó: el logo de arriba o la tarjeta de abajo.
    private void AbrirMenuCuenta(object sender, RoutedEventArgs e)
    {
        var desdeLogo = sender == BotonLogo;
        MenuCuenta.PlacementTarget = (UIElement)sender;
        MenuCuenta.Placement = desdeLogo ? PlacementMode.Bottom : PlacementMode.Top;
        MenuCuenta.IsOpen = !MenuCuenta.IsOpen;
        if (MenuCuenta.IsOpen) Dispatcher.BeginInvoke(() => OpcionClave.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }

    /// <summary>
    /// Cierra las planillas abiertas antes de salir. Cada una pregunta si tiene cambios sin guardar;
    /// si la persona decide quedarse en alguna, no se cierra la sesión.
    /// </summary>
    private bool CerrarVentanas()
    {
        foreach (var ventana in OwnedWindows.OfType<Window>().ToList())
        {
            ventana.Close();
            if (ventana.IsVisible) { ventana.Activate(); return false; }
        }
        return true;
    }

    private void MostrarAtajos()
    {
        if (OwnedWindows.Count == 0) new VentanaAtajos(this).ShowDialog();
    }

    private void IrASeccion(int indice)
    {
        if (Aplicacion.Visibility == Visibility.Visible && indice < Navegacion.Children.Count && Navegacion.Children[indice] is ToggleButton boton)
            Seleccionar(boton);
    }

    private void AgregarNavegacion(string icono, string texto, Action accion)
    {
        var contenido = new StackPanel { Orientation = Orientation.Horizontal };
        contenido.Children.Add(Icono.De(icono, 19));
        contenido.Children.Add(new TextBlock { Text = texto, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        var boton = new ToggleButton { Content = contenido, Style = (Style)FindResource("Navegacion"), Tag = accion, ToolTip = $"{texto} (Ctrl+{Navegacion.Children.Count + 1})" };
        System.Windows.Automation.AutomationProperties.SetName(boton, texto);
        boton.Click += (_, _) => Seleccionar(boton);
        Navegacion.Children.Add(boton);
    }

    private void Seleccionar(ToggleButton boton)
    {
        foreach (var otro in Navegacion.Children.OfType<ToggleButton>())
            otro.IsChecked = otro == boton;
        ((Action)boton.Tag)();
    }

    private void Mostrar(string titulo, string descripcion, UIElement vista)
    {
        TituloPagina.Text = titulo;
        DescripcionPagina.Text = descripcion;
        Contenido.Content = vista;
        var duracion = TimeSpan.FromMilliseconds(220);
        var suave = new CubicEase { EasingMode = EasingMode.EaseOut };
        Contenido.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duracion) { EasingFunction = suave });
        Entrada.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, duracion) { EasingFunction = suave });
    }

    private void Registrarse(object sender, RoutedEventArgs e)
    {
        if (ocupada) return;
        try
        {
            api.Configurar(Normalizar(servidor));
            var registro = new VentanaRegistro(this, api, Cedula.Text);
            if (registro.ShowDialog() != true) return;
            Cedula.Text = Cedulas.Mostrar(ReglasAcademicas.Cedula(registro.CedulaUsada));
            Clave.Clear();
            Clave.Focus();
            MostrarMensaje("Listo, tu contraseña quedó guardada. Ya puedes iniciar sesión.", Tono.Exito);
        }
        catch (Exception error) when (error is ArgumentException or UriFormatException or ReglaException) { MostrarMensaje(error.Message, Tono.Peligro); }
    }

    private async void Activar(object sender, RoutedEventArgs e)
    {
        if (ocupada) return;
        var formulario = new Formulario(this, "Establecer acceso", "Escribe el código que te dio administración o tu docente. Sirve una sola vez.");
        var cedula = formulario.Texto("Cédula", Cedula.Text);
        var codigo = formulario.Texto("Código temporal");
        var nueva = formulario.Clave("Contraseña nueva", "Mínimo 12 caracteres.");
        var repetir = formulario.Clave("Repetir contraseña");
        if (formulario.ShowDialog() != true) return;
        ocupada = true;
                try
        {
            Seguridad.ConfirmarClave(nueva.Password, repetir.Password);
            api.Configurar(servidor);
            await api.Enviar(HttpMethod.Post, "activar", new ActivarCuenta(cedula.Text, codigo.Text.Trim(), nueva.Password));
            MostrarMensaje("Cuenta lista. Ya puedes iniciar sesión.", Tono.Exito);
            Cedula.Text = cedula.Text;
            Clave.Focus();
        }
        catch (Exception error) when (error is ErrorServicio or ArgumentException or ReglaException) { MostrarMensaje(error.Message, Tono.Peligro); }
        finally { ocupada = false; }
    }

    private async void CambiarClave(object sender, RoutedEventArgs e)
    {
        MenuCuenta.IsOpen = false;
        // Cambiar la contraseña cierra todas las sesiones: primero se cierran las planillas abiertas.
        if (ocupada || !CerrarVentanas()) return;
        var formulario = new Formulario(this, "Cambiar contraseña", "Al cambiarla se cerrarán todas tus sesiones abiertas.");
        var actual = formulario.Clave("Contraseña actual");
        var nueva = formulario.Clave("Contraseña nueva", "Mínimo 12 caracteres.");
        var repetir = formulario.Clave("Repetir contraseña");
        if (formulario.ShowDialog() != true) return;
        ocupada = true;
        try
        {
            Seguridad.ConfirmarClave(nueva.Password, repetir.Password);
            await api.Enviar(HttpMethod.Post, "clave", new CambiarClave(actual.Password, nueva.Password));
            api.OlvidarSesion();
            MostrarAcceso();
            MostrarMensaje("Contraseña actualizada. Inicia sesión de nuevo.", Tono.Exito);
        }
        catch (Exception error) when (error is ErrorServicio or ArgumentException or ReglaException)
        { Dialogo.Informar(this, "No se cambió la contraseña", error.Message, Tono.Peligro); }
        finally { ocupada = false; }
    }

    private async void CerrarSesion(object sender, RoutedEventArgs e)
    {
        MenuCuenta.IsOpen = false;
        if (ocupada || !CerrarVentanas()) return;
        ocupada = true;
        try { await api.Salir(); }
        catch (ErrorServicio)
        {
            Dialogo.Informar(this, "Sesión cerrada en este equipo",
                "No se pudo confirmar la revocación en el servidor. La sesión vencerá automáticamente.", Tono.Aviso);
        }
        finally { ocupada = false; MostrarAcceso(); }
    }

    private void SesionTerminada()
    {
        if (Acceso.Visibility == Visibility.Visible) return;
        foreach (var planilla in OwnedWindows.OfType<VentanaPlanilla>().ToList()) planilla.CerrarSinConfirmar();
        MostrarAcceso();
        Dialogo.Informar(this, "Tu sesión terminó",
            "Por seguridad, vuelve a iniciar sesión. Si estabas editando notas, revisa la planilla al entrar: lo que no se guardó como borrador no llegó al servidor.", Tono.Aviso);
    }

    private void MostrarAcceso()
    {
        MenuCuenta.IsOpen = false;
        LimpiarAvisos();
        TituloClaro = true;
        AnimarAcceso();
        Contenido.Content = null;
        Navegacion.Children.Clear();
        TituloPagina.Text = DescripcionPagina.Text = "";
        // Quien no pidió recordar su cédula no la deja escrita para el siguiente.
        if (RecordarCedula.IsChecked != true) Cedula.Clear();
        Aplicacion.Visibility = Visibility.Collapsed;
        Acceso.Visibility = Visibility.Visible;
        Clave.Clear();
        MostrarMensaje("", Tono.Neutro);
        (Cedula.Text.Length > 0 ? (UIElement)Clave : Cedula).Focus();
    }
}
