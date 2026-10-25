using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Escritorio.Presentacion;

public sealed class Icono : Viewbox
{
    public static readonly DependencyProperty DatosProperty = DependencyProperty.Register(nameof(Datos), typeof(Geometry),
        typeof(Icono), new PropertyMetadata(null, (d, e) => ((Icono)d).ruta.Data = (Geometry?)e.NewValue));
    public static readonly DependencyProperty GrosorProperty = DependencyProperty.Register(nameof(Grosor), typeof(double),
        typeof(Icono), new PropertyMetadata(1.8, (d, e) => ((Icono)d).ruta.StrokeThickness = (double)e.NewValue));

    public Geometry? Datos { get => (Geometry?)GetValue(DatosProperty); set => SetValue(DatosProperty, value); }
    public double Grosor { get => (double)GetValue(GrosorProperty); set => SetValue(GrosorProperty, value); }

    private readonly Path ruta = new()
    {
        Width = 24, Height = 24, Stretch = Stretch.None, StrokeThickness = 1.8,
        StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round
    };

    public Icono()
    {
        Width = 18;
        Height = 18;
        IsHitTestVisible = false;
        ruta.SetBinding(Shape.StrokeProperty, new Binding { Path = new PropertyPath(TextElement.ForegroundProperty), RelativeSource = RelativeSource.Self });
        Child = ruta;
    }

    public static Icono De(string recurso, double tamano = 18) => new()
    {
        Datos = (Geometry)Application.Current.FindResource(recurso), Width = tamano, Height = tamano
    };
}
