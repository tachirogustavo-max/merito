using Escritorio.Servicios;

namespace Escritorio.Presentacion;

public sealed partial class VentanaPlanilla : VentanaBase
{
    private bool forzarCierre;

    internal VistaPlanilla Vista { get; }

    public VentanaPlanilla(ClienteApi api, long oferta, bool cargarAlAbrir = true)
    {
        InitializeComponent();
        Vista = new VistaPlanilla(api, oferta, cargarAlAbrir);
        Contenedor.Content = Vista;
        Closing += (_, e) => e.Cancel = !forzarCierre && !Vista.PuedeCerrar();
    }

    /// <summary>Cierra sin preguntar; se usa cuando la sesión ya no es válida.</summary>
    public void CerrarSinConfirmar()
    {
        forzarCierre = true;
        Close();
    }
}
