using System.Windows;
using Entidades;
using Escritorio.Servicios;

namespace Escritorio.Presentacion;

public sealed partial class VentanaCargaSeccion : VentanaBase
{
    private bool guardada;
    internal VistaCargaSeccion Vista { get; }
    public ResultadoCarga? Resultado => Vista.Resultado;

    public VentanaCargaSeccion(ClienteApi api, Window propietario, List<Periodo> abiertos, List<Seccion> secciones, List<MateriaPensum> pensum, List<Persona> docentes)
    {
        InitializeComponent();
        Owner = propietario;
        Vista = new VistaCargaSeccion(api, abiertos, secciones, pensum, docentes, this);
        Contenedor.Content = Vista;
        Closing += (_, e) => e.Cancel = !guardada && !Vista.PuedeCerrar();
    }

    internal void Terminar()
    {
        guardada = true;
        try { DialogResult = true; }
        catch (InvalidOperationException) { Close(); } // abierta sin ShowDialog, como en las pruebas
    }
}
