using System.Windows;

namespace Escritorio.Presentacion;

public static class Sugerencias
{
    public static readonly DependencyProperty TextoProperty = DependencyProperty.RegisterAttached(
        "Texto", typeof(string), typeof(Sugerencias), new PropertyMetadata(""));

    public static string GetTexto(DependencyObject elemento) => (string)elemento.GetValue(TextoProperty);
    public static void SetTexto(DependencyObject elemento, string valor) => elemento.SetValue(TextoProperty, valor);
}
