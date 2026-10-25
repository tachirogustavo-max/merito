using System.Windows;

namespace Escritorio.Presentacion;

// Resumen de atajos de teclado.
public sealed partial class VentanaAtajos : VentanaBase
{
    public VentanaAtajos() => InitializeComponent();

    public VentanaAtajos(Window propietario) : this()
    {
        Owner = propietario;
        Height = Math.Min(640, SystemParameters.WorkArea.Height - 60);
        Loaded += (_, _) => Cerrar.Focus();
    }

    private void Cerrar_Click(object sender, RoutedEventArgs e) => Close();
}
