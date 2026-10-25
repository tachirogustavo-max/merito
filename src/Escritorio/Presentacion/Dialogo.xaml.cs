using System.Windows;
using System.Windows.Documents;

namespace Escritorio.Presentacion;

public sealed partial class Dialogo : VentanaBase
{
    private readonly string referencia = "";

    public Dialogo() => InitializeComponent();

    internal Dialogo(Window? propietario, string titulo, string texto, Tono tono, string aceptar, string? cancelar, string? referencia = null) : this()
    {
        Title = titulo;
        ShowInTaskbar = propietario is null;
        if (propietario is { IsLoaded: true, IsVisible: true }) { Owner = propietario; WindowStartupLocation = WindowStartupLocation.CenterOwner; }
        else WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var confirmacion = cancelar is not null;
        var (fondo, color) = Textos.Pinceles(tono);
        Circulo.Background = fondo;
        TextElement.SetForeground(Simbolo, color);
        Simbolo.Datos = (System.Windows.Media.Geometry)FindResource(tono switch
        {
            Tono.Exito => "IcoCheck", Tono.Aviso => "IcoAlerta", Tono.Peligro => confirmacion ? "IcoAlerta" : "IcoError", _ => "IcoInfo"
        });
        Titulo.Text = titulo;
        Mensaje.Text = texto;
        if (referencia is not null)
        {
            this.referencia = referencia;
            Referencia.Text = $"Referencia: {referencia}";
            BloqueReferencia.Visibility = Visibility.Visible;
        }
        Aceptar.Content = aceptar;
        Aceptar.IsCancel = !confirmacion;
        if (confirmacion && tono == Tono.Peligro) Aceptar.SetResourceReference(StyleProperty, "BotonPeligro");
        if (confirmacion) Cancelar.Content = cancelar;
        else { Cancelar.Visibility = Visibility.Collapsed; Cancelar.IsCancel = false; }
        // Ante una acción destructiva el foco inicial queda en «Cancelar».
        Loaded += (_, _) => (tono == Tono.Peligro && confirmacion ? Cancelar : Aceptar).Focus();
    }

    private void Aceptar_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void CopiarReferencia_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(referencia); CopiarReferencia.Content = "Copiada"; }
        catch (System.Runtime.InteropServices.ExternalException) { CopiarReferencia.Content = "No se pudo copiar"; }
    }

    public static bool Preguntar(Window? propietario, string titulo, string texto, string aceptar = "Continuar", bool peligro = false) =>
        new Dialogo(propietario, titulo, texto, peligro ? Tono.Peligro : Tono.Marca, aceptar, "Cancelar").ShowDialog() == true;

    public static void Informar(Window? propietario, string titulo, string texto, Tono tono = Tono.Marca, string? referencia = null) =>
        new Dialogo(propietario, titulo, texto, tono, "Entendido", null, referencia).ShowDialog();
}
