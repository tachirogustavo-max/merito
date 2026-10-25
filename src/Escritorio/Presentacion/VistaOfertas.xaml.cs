using System.Windows;
using System.Windows.Input;
using Entidades;
using Escritorio.Servicios;

namespace Escritorio.Presentacion;

public sealed partial class VistaOfertas : VistaBase
{
    private readonly Paginacion pagina = new();

    public VistaOfertas(ClienteApi api) : base(api)
    {
        InitializeComponent();
        if (api.Persona?.Rol == Roles.Docente)
        {
            Ofertas.Columnas = "Período=Periodo; Materia=Materia; Sección=Seccion; Inscritos=Matriculados; Por revisar=Pendientes; Estado=Estado";
            PanelOfertas.Buscar = "Buscar materia o sección";
        }
        Registrar(PanelOfertas, pagina, Cargar);
        Ofertas.Activada += async (_, _) => await Ejecutar(Abrir);
        Atajo(Key.A, ModifierKeys.Control, Abrir);
        Atajo(Key.F5, ModifierKeys.None, Cargar, "Información actualizada.");
        RecargarSolo(Cargar);
        HabilitarBusqueda();
        FocoInicial = Ofertas;
        Loaded += async (_, _) => { await Ejecutar(Cargar); EnfocarInicio(); };
    }

    private async void Abrir_Click(object sender, RoutedEventArgs e) => await Ejecutar(Abrir);

    private async Task Cargar() => Ofertas.ItemsSource = pagina.Recibir(await Api.Obtener<Pagina<Oferta>>($"ofertas?{pagina.Consulta}"));

    private async Task Abrir()
    {
        if (Ofertas.SelectedItem is not Oferta oferta) throw new ArgumentException("Selecciona una materia y sección.");
        var ventana = new VentanaPlanilla(Api, oferta.Id) { Owner = Propietario };
        ventana.ShowDialog();
        await Cargar();
    }
}
