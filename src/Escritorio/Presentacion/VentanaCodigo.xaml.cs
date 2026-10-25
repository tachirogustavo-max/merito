using System.Windows;
using Entidades;

namespace Escritorio.Presentacion;

public sealed partial class VentanaCodigo : VentanaBase
{
    private readonly string codigo = "";

    public VentanaCodigo() => InitializeComponent();

    public VentanaCodigo(Window propietario, Persona persona, CodigoAcceso acceso) : this()
    {
        Owner = propietario;
        codigo = acceso.Codigo;
        Titular.Text = $"{persona.NombreCompleto}, cédula {Cedulas.Mostrar(persona.Cedula)}";
        Codigo.Text = acceso.Codigo;
        Vence.Text = $"Vence a las {acceso.VenceEn.ToLocalTime():HH:mm}. Entrégalo únicamente a su titular; se usa una sola vez.";
    }

    private void Copiar_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(codigo); RotuloCopiar.Texto = "Copiado"; }
        catch (System.Runtime.InteropServices.ExternalException) { RotuloCopiar.Texto = "No se pudo copiar"; }
    }

    private void Listo_Click(object sender, RoutedEventArgs e) => Close();
}
