using System.ComponentModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using Entidades;
using Escritorio.Servicios;

namespace Escritorio.Presentacion;

// Carga académica de una sección completa: cada materia de la carrera con su docente, en una sola pantalla.
public sealed partial class VistaCargaSeccion : VistaBase
{
    public sealed class Fila(MateriaPensum materia, string? asignadaA, int semestreSeccion) : INotifyPropertyChanged
    {
        private Persona? docente;
        public MateriaPensum Materia { get; } = materia;
        public string? AsignadaA { get; } = asignadaA;
        public bool Asignada => AsignadaA is not null;
        public bool Libre => AsignadaA is null;
        // Solo se marca lo que se sale de lo esperado: otro semestre o una electiva.
        public string Nota => Materia.Semestre == 0 ? "electiva" : Materia.Semestre != semestreSeccion && semestreSeccion > 0 ? $"semestre {Materia.Semestre}" : "";
        public bool TieneNota => Nota.Length > 0;
        public Persona? Docente
        {
            get => docente;
            set { docente = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Docente))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public sealed record OpcionSeccion(Seccion Seccion)
    {
        public string Nombre => $"{Seccion.Codigo} ({Seccion.Carrera})";
    }

    private readonly List<MateriaPensum> pensum = [];
    private readonly List<Seccion> secciones = [];
    private static readonly Referencia Todos = new(0, "Todos");
    private readonly VentanaCargaSeccion? ventana;
    private List<Fila> filas = [];
    private object? periodoMostrado, seccionMostrada;
    public List<Persona> Docentes { get; } = [];
    public ResultadoCarga? Resultado { get; private set; }

    public VistaCargaSeccion() => InitializeComponent();

    internal VistaCargaSeccion(ClienteApi api, List<Periodo> abiertos, List<Seccion> secciones, List<MateriaPensum> pensum, List<Persona> docentes, VentanaCargaSeccion ventana) : base(api)
    {
        InitializeComponent();
        this.pensum = pensum;
        this.ventana = ventana;
        Docentes = docentes;
        ListaPeriodo.ItemsSource = abiertos;
        ListaPeriodo.SelectedItem = abiertos.FirstOrDefault();
        this.secciones = secciones;
        // Carrera y semestre solo acotan la lista de secciones; la elegida sigue siendo la que manda.
        ListaCarrera.ItemsSource = secciones.GroupBy(s => s.CarreraId).Select(g => new Referencia(g.Key, g.First().Carrera)).OrderBy(c => c.Nombre).ToList();
        ListaCarrera.Elegido += (_, _) => { ListaSemestre.SelectedItem = null; FiltrarSecciones(); };
        ListaSemestre.Elegido += (_, _) => FiltrarSecciones();
        FiltrarSecciones();
        ListaPeriodo.Elegido += async (_, _) => await Cambiar(ListaPeriodo);
        ListaSeccion.Elegido += async (_, _) => await Cambiar(ListaSeccion);
        TodoElPensum.Click += async (_, _) => await Ejecutar(Mostrar);
        Atajo(Key.G, ModifierKeys.Control, Guardar);
        InputBindings.Add(new KeyBinding(new Orden(() => ventana.Close()), Key.Escape, ModifierKeys.None));
        Loaded += (_, _) => ListaSeccion.Caja.Focus();
    }

    private void FiltrarSecciones()
    {
        var carrera = (ListaCarrera.SelectedItem as Referencia)?.Id;
        var deCarrera = secciones.Where(s => carrera is null || s.CarreraId == carrera).ToList();
        var semestres = deCarrera.Where(s => s.Semestre > 0).Select(s => s.Semestre).Distinct().Order()
            .Select(n => new Referencia(n, $"Semestre {n}")).Prepend(Todos).ToList();
        if (!semestres.Contains(ListaSemestre.SelectedItem)) ListaSemestre.SelectedItem = null;
        ListaSemestre.ItemsSource = semestres;
        var semestre = (ListaSemestre.SelectedItem as Referencia)?.Id ?? 0;
        ListaSeccion.ItemsSource = deCarrera.Where(s => semestre == 0 || s.Semestre == semestre)
            .OrderBy(s => s.Carrera).ThenBy(s => s.Semestre).ThenBy(s => s.Codigo).Select(s => new OpcionSeccion(s)).ToList();
    }

    private async void Guardar_Click(object sender, RoutedEventArgs e) => await Ejecutar(Guardar);
    private void Cancelar_Click(object sender, RoutedEventArgs e) => ventana?.Close();

    public bool PuedeCerrar() => !filas.Any(f => f.Docente is not null) || Dialogo.Preguntar(Window.GetWindow(this), "Docentes sin guardar",
        "Elegiste docentes que todavía no se guardaron. Si cierras ahora se pierden.", "Cerrar sin guardar", true);

    // Al cambiar de sección o período con docentes elegidos se pregunta antes de descartarlos.
    private async Task Cambiar(ListaBuscable origen)
    {
        if (ListaPeriodo.SelectedItem == periodoMostrado && ListaSeccion.SelectedItem == seccionMostrada) return;
        if (filas.Any(f => f.Docente is not null) && !Dialogo.Preguntar(Window.GetWindow(this), "Docentes sin guardar",
                "Si cambias de sección se pierden los docentes que elegiste y no guardaste.", "Cambiar de todos modos", true))
        {
            origen.SelectedItem = origen == ListaPeriodo ? periodoMostrado : seccionMostrada;
            return;
        }
        await Ejecutar(Mostrar);
    }

    private async Task Mostrar()
    {
        periodoMostrado = ListaPeriodo.SelectedItem;
        seccionMostrada = ListaSeccion.SelectedItem;
        if (ListaPeriodo.SelectedItem is not Periodo p || ListaSeccion.SelectedItem is not OpcionSeccion s) { MostrarFilas([], "Elige una sección para ver sus materias."); return; }
        var asignadas = (await Api.Obtener<List<MateriaAsignada>>($"periodos/{p.Id}/secciones/{s.Seccion.Id}/carga")).ToDictionary(a => a.MateriaId, a => a.Docente);
        var semestre = s.Seccion.Semestre;
        var dePensum = pensum.Where(m => m.CarreraId == s.Seccion.CarreraId).ToList();
        // Una sección de 2.º semestre ve las de 2.º y las electivas; el resto del pensum, a pedido.
        TodoElPensum.Visibility = semestre > 0 && dePensum.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        var visibles = dePensum.Where(m => semestre == 0 || TodoElPensum.IsChecked == true || m.Semestre == semestre || m.Semestre == 0 || asignadas.ContainsKey(m.MateriaId));
        var nuevas = visibles.OrderBy(m => asignadas.ContainsKey(m.MateriaId)).ThenBy(m => m.Semestre == 0).ThenBy(m => m.Semestre).ThenBy(m => m.Codigo)
            .Select(m => new Fila(m, asignadas.GetValueOrDefault(m.MateriaId), semestre)).ToList();
        MostrarFilas(nuevas, dePensum.Count == 0 ? $"El pensum de {s.Seccion.Carrera} todavía no tiene materias. Agrégalas en Estructura › Pensum."
            : $"El pensum de {s.Seccion.Carrera} no tiene materias del semestre {semestre}. Marca «Ver todo el pensum».");
    }

    private void MostrarFilas(List<Fila> nuevas, string vacio)
    {
        filas = nuevas;
        foreach (var fila in filas) fila.PropertyChanged += (_, _) => Contar();
        Filas.ItemsSource = filas;
        Vacio.Text = vacio;
        Vacio.Visibility = filas.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Contar();
    }

    private void Contar()
    {
        if (filas.Count == 0) { Resumen.Text = ""; return; }
        var elegidas = filas.Count(f => f.Docente is not null);
        var yaAsignadas = filas.Count(f => f.Asignada);
        Resumen.Text = $"{filas.Count} {(filas.Count == 1 ? "materia" : "materias")}, {yaAsignadas} ya {(yaAsignadas == 1 ? "asignada" : "asignadas")}, {elegidas} por guardar";
    }

    private async Task Guardar()
    {
        if (ListaPeriodo.SelectedItem is not Periodo p || ListaSeccion.SelectedItem is not OpcionSeccion s) throw new ArgumentException("Elige el período y la sección.");
        var nuevas = filas.Where(f => f.Docente is not null).Select(f => new MateriaDocente(f.Materia.MateriaId, f.Docente!.Id)).ToList();
        if (nuevas.Count == 0) throw new ArgumentException("Elige el docente de al menos una materia.");
        Resultado = await Api.Enviar<ResultadoCarga>(HttpMethod.Post, "ofertas/lote", new CargaEnBloque(p.Id, s.Seccion.Id, nuevas));
        ventana?.Terminar();
    }
}
