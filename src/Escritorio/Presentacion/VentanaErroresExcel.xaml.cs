using System.Windows;
using Entidades;

namespace Escritorio.Presentacion;

public sealed partial class VentanaErroresExcel : VentanaBase
{
    private readonly List<ErrorFila> errores = [];

    public VentanaErroresExcel() => InitializeComponent();

    public VentanaErroresExcel(Window propietario, VistaImportacion vista) : this(propietario, vista.Errores, vista.Filas.Count) { }

    public VentanaErroresExcel(Window propietario, List<ErrorFila> lista, int correctas) : this()
    {
        Owner = propietario;
        errores = lista;
        var filas = lista.Select(e => e.Fila).Distinct().Count();
        Titulo.Text = filas == 1 ? "Hay una fila por corregir" : $"Hay {filas} filas por corregir";
        Detalle.Text = "No se importó ninguna fila. Corrige estos datos en el Excel y vuelve a importarlo." +
            (correctas <= 0 ? "" : correctas == 1 ? " La otra fila está correcta." : $" Las otras {correctas} filas están correctas.");
        Errores.ItemsSource = lista;
    }
    private void Copiar_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(string.Join(Environment.NewLine, errores.Select(f => $"Fila {f.Fila}: {f.Problema}"))); Copiar.Content = "Copiada"; }
        catch (System.Runtime.InteropServices.ExternalException) { Copiar.Content = "No se pudo copiar"; }
    }

    private void Entendido_Click(object sender, RoutedEventArgs e) => Close();
}
