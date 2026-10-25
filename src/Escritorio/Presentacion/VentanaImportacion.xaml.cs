using System.Windows;
using Entidades;

namespace Escritorio.Presentacion;

public sealed partial class VentanaImportacion : VentanaBase
{
    public VentanaImportacion() => InitializeComponent();

    public VentanaImportacion(Window propietario, VistaImportacion vista, string detalle) : this()
    {
        Owner = propietario;
        Cantidad.Text = vista.Filas.Count == 1 ? "1 estudiante" : $"{vista.Filas.Count} estudiantes";
        Detalle.Text = detalle;
        Filas.ItemsSource = vista.Filas;
    }

    // Misma revisión para otros archivos, como el pensum de una carrera.
    public VentanaImportacion(Window propietario, string titulo, string cantidad, string detalle, System.Collections.IEnumerable filas, string columnas, string boton) : this()
    {
        Owner = propietario;
        Title = titulo;
        Cantidad.Text = cantidad;
        Detalle.Text = detalle;
        Filas.Columnas = columnas;
        Filas.ItemsSource = filas;
        Aceptar.Content = boton;
    }

    private void Confirmar_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
