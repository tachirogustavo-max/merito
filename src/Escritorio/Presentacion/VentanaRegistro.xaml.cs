using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Entidades;
using Escritorio.Servicios;
using Logica;

namespace Escritorio.Presentacion;

/// <summary>
/// Primera vez u olvido de contraseña: la cédula, un código que llega al correo de la matrícula y la contraseña nueva.
/// Los errores se muestran dentro de la ventana para no perder lo escrito.
/// </summary>
public sealed partial class VentanaRegistro : VentanaBase
{
    private readonly ClienteApi? api;
    private readonly DispatcherTimer espera = new() { Interval = TimeSpan.FromSeconds(1) };
    private int segundos;
    private bool ocupada;
    public string CedulaUsada => Cedula.Text.Trim();
    public bool Listo { get; private set; }

    public VentanaRegistro() => InitializeComponent();

    internal VentanaRegistro(Window propietario, ClienteApi cliente, string cedula) : this()
    {
        Owner = propietario;
        api = cliente;
        Cedula.Text = cedula;
        espera.Tick += (_, _) => Contar();
        Loaded += (_, _) => Cedula.Focus();
        Closed += (_, _) => espera.Stop();
    }

    private async void Enviar_Click(object sender, RoutedEventArgs e) => await Solicitar();
    private async void Reenviar_Click(object sender, RoutedEventArgs e) => await Solicitar();

    private async Task Solicitar()
    {
        if (ocupada || api is null) return;
        ocupada = true;
        Enviar.Content = "Enviando…";
        Avisar("", Tono.Neutro);
        try
        {
            var enviado = await api.Enviar<SolicitudEnviada>(HttpMethod.Post, "registro/codigo", new SolicitarCodigo(Cedula.Text));
            Paso.Text = "PASO 2 DE 2";
            Titulo.Text = enviado.Registrada ? "Elige tu contraseña nueva" : "Crea tu contraseña";
            Enviado.Text = $"Te enviamos un código de 6 cifras a {enviado.Correo}. Vence a las {enviado.VenceEn.ToLocalTime():HH:mm}; revisa también el correo no deseado.";
            PasoCedula.Visibility = Visibility.Collapsed;
            PasoCodigo.Visibility = Visibility.Visible;
            Codigo.Clear();
            Codigo.Focus();
            // Para pedir otro hay que esperar un minuto, igual que en el servidor.
            segundos = 60;
            Contar();
            espera.Start();
        }
        catch (Exception error) when (error is ErrorServicio or ReglaException or ArgumentException) { Avisar(error.Message, Tono.Peligro); }
        finally { ocupada = false; Enviar.Content = "Enviarme el código"; }
    }

    private void Contar()
    {
        Reenviar.IsEnabled = segundos <= 0;
        Reenviar.Content = segundos > 0 ? $"¿No te llegó? Podrás pedir otro en {segundos} s" : "¿No te llegó? Enviar otro código";
        if (segundos-- <= 0) espera.Stop();
    }

    private async void Guardar_Click(object sender, RoutedEventArgs e)
    {
        if (ocupada || api is null) return;
        ocupada = true;
        Guardar.Content = "Guardando…";
        Avisar("", Tono.Neutro);
        try
        {
            Seguridad.ConfirmarClave(Nueva.Password, Repetir.Password);
            await api.Enviar(HttpMethod.Post, "registro/confirmar", new ConfirmarCorreo(Cedula.Text, Codigo.Text, Nueva.Password));
            Nueva.Clear();
            Repetir.Clear();
            Listo = true;
            try { DialogResult = true; }
            catch (InvalidOperationException) { Close(); } // abierta sin ShowDialog, como en las pruebas
        }
        catch (Exception error) when (error is ErrorServicio or ReglaException or ArgumentException)
        {
            Avisar(error.Message, Tono.Peligro);
            Codigo.Focus();
            Codigo.SelectAll();
        }
        finally { ocupada = false; Guardar.Content = "Guardar contraseña"; }
    }

    private void Avisar(string texto, Tono tono)
    {
        if (string.IsNullOrWhiteSpace(texto)) { Alerta.Visibility = Visibility.Collapsed; return; }
        var (fondo, color) = Textos.Pinceles(tono);
        Alerta.Background = fondo;
        Alerta.BorderBrush = tono == Tono.Peligro ? (System.Windows.Media.Brush)FindResource("ErrorBorde") : fondo;
        Mensaje.Foreground = color;
        Mensaje.Text = texto;
        Alerta.Visibility = Visibility.Visible;
    }
}
