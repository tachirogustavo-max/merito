using System.Windows;
using Entidades;

namespace Escritorio.Presentacion;

public sealed partial class VentanaCierre : VentanaBase
{
    public sealed record Renglon(string Titulo, string Detalle, bool Completa);

    public VentanaCierre() => InitializeComponent();

    public VentanaCierre(Window propietario, ResumenCierre resumen) : this()
    {
        Owner = propietario;
        Title = $"Revisión de cierre del período {resumen.Periodo.Nombre}";
        TituloPeriodo.Text = resumen.Periodo.Nombre;
        Resumen.Text = resumen.PuedeCerrar ? "Todas las materias están listas. Ya puedes cerrar el período." : "Resuelve los pendientes antes de cerrar el período.";
        SinMaterias.Visibility = resumen.Ofertas.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Materias.ItemsSource = resumen.Ofertas.Select(o => new Renglon($"{o.Materia}, sección {o.Seccion}",
            o.Pendientes.Count == 0 ? "Lista para el cierre." : string.Join("\n", o.Pendientes), o.Pendientes.Count == 0)).ToList();
    }
}
