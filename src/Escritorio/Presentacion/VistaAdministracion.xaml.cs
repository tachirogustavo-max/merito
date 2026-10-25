using System.Net.Http;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Entidades;
using Escritorio.Servicios;

namespace Escritorio.Presentacion;

public sealed partial class VistaAdministracion : VistaBase
{
    private const string Actualizado = "Información actualizada.";
    private List<Persona> listaDocentes = [];
    private readonly Paginacion paginaPersonas = new();
    private readonly Paginacion paginaDocentes = new();
    private readonly Paginacion paginaEstudiantes = new();
    private readonly Paginacion paginaOfertas = new();
    private readonly Paginacion paginaAuditoria = new();
    private List<Periodo> listaPeriodos = [];
    private List<Carrera> listaCarreras = [];
    private List<Materia> listaMaterias = [];
    private List<MateriaPensum> listaPensum = [];
    private List<Seccion> listaSecciones = [];

    public VistaAdministracion(ClienteApi api) : base(api)
    {
        InitializeComponent();
        Registrar(PanelCuentas, paginaPersonas, CargarPersonas);
        Registrar(PanelDocentes, paginaDocentes, CargarNomina);
        Registrar(PanelEstudiantes, paginaEstudiantes, CargarEstudiantes);
        FiltroPeriodo.Elegido += async (_, _) => { paginaEstudiantes.Desde = 0; await Ejecutar(CargarEstudiantes); };
        FiltroSeccion.Elegido += async (_, _) => { paginaEstudiantes.Desde = 0; await Ejecutar(CargarEstudiantes); };
        Registrar(PanelCarreras);
        Registrar(PanelMaterias);
        Registrar(PanelSecciones);
        Registrar(PanelCarga, paginaOfertas, CargarOfertas);
        Registrar(PanelPeriodos);
        Registrar(PanelActividad, paginaAuditoria, CargarAuditoria);
        Carreras.Activada += async (_, _) => await Ejecutar(CorregirCarrera);
        Materias.Activada += async (_, _) => await Ejecutar(CorregirMateria);
        Secciones.Activada += async (_, _) => await Ejecutar(CorregirSeccion);
        CarreraPensum.Elegido += (_, _) => MostrarPensum();

        // Ctrl+U crea en la pestaña visible.
        Atajo(Key.U, ModifierKeys.Control, () => (AutomationProperties.GetName((DependencyObject)Pestanas.SelectedItem), Estructura.SelectedIndex) switch
        {
            ("Estructura", 0) => EditarCarrera(null),
            ("Estructura", 1) => CrearMateria(),
            ("Estructura", _) => EditarSeccion(null),
            ("Docentes", _) => CrearPersona(Roles.Docente),
            ("Carga académica", _) => AsignarEnBloque(),
            ("Estudiantes", _) => CrearPersona(Roles.Estudiante),
            ("Períodos", _) => CrearReferencia("periodos", "período"),
            ("Cuentas", _) => CrearPersona(Roles.Administrador),
            _ => Task.CompletedTask
        });
        Atajo(Key.F5, ModifierKeys.None, Cargar, Actualizado);
        RecargarSolo(Cargar);
        HabilitarBusqueda();
        FocoInicial = Carreras;
        Loaded += async (_, _) => { await Ejecutar(Cargar); EnfocarInicio(); };
    }

    // Botones del XAML
    private async void CrearCuenta_Click(object sender, RoutedEventArgs e) => await Ejecutar(() => CrearPersona(Roles.Administrador));
    private async void CrearDocente_Click(object sender, RoutedEventArgs e) => await Ejecutar(() => CrearPersona(Roles.Docente));
    private async void CrearEstudiante_Click(object sender, RoutedEventArgs e) => await Ejecutar(() => CrearPersona(Roles.Estudiante));
    private async void ImportarNomina_Click(object sender, RoutedEventArgs e) => await Ejecutar(ImportarNomina);
    private async void PlantillaNomina_Click(object sender, RoutedEventArgs e) => await Ejecutar(() => Plantilla("plantilla-nomina", "nomina-docente.xlsx"));
    private async void EmitirCodigo_Click(object sender, RoutedEventArgs e) => await Ejecutar(EmitirCodigo);
    private async void CambiarEstado_Click(object sender, RoutedEventArgs e) => await Ejecutar(CambiarEstado);
    private async void CorregirCorreo_Click(object sender, RoutedEventArgs e) => await Ejecutar(CorregirCorreo);
    private async void AgregarCarrera_Click(object sender, RoutedEventArgs e) => await Ejecutar(() => EditarCarrera(null));
    private async void CorregirCarrera_Click(object sender, RoutedEventArgs e) => await Ejecutar(CorregirCarrera);
    private async void AgregarMateria_Click(object sender, RoutedEventArgs e) => await Ejecutar(CrearMateria);
    private async void CorregirMateria_Click(object sender, RoutedEventArgs e) => await Ejecutar(CorregirMateria);
    private async void AgregarExistente_Click(object sender, RoutedEventArgs e) => await Ejecutar(AgregarExistente);
    private async void QuitarMateria_Click(object sender, RoutedEventArgs e) => await Ejecutar(QuitarMateria);
    private async void ImportarPensum_Click(object sender, RoutedEventArgs e) => await Ejecutar(ImportarPensum);
    private async void PlantillaPensum_Click(object sender, RoutedEventArgs e) => await Ejecutar(PlantillaPensum);
    private async void Reglas_Click(object sender, RoutedEventArgs e) => await Ejecutar(CambiarReglas);
    private async void AgregarSeccion_Click(object sender, RoutedEventArgs e) => await Ejecutar(() => EditarSeccion(null));
    private async void CorregirSeccion_Click(object sender, RoutedEventArgs e) => await Ejecutar(CorregirSeccion);
    private async void FormatoCodigo_Click(object sender, RoutedEventArgs e) => await Ejecutar(CambiarFormato);
    private async void AsignarPorSeccion_Click(object sender, RoutedEventArgs e) => await Ejecutar(AsignarEnBloque);
    private async void CopiarCarga_Click(object sender, RoutedEventArgs e) => await Ejecutar(CopiarCarga);
    private async void AsignarUna_Click(object sender, RoutedEventArgs e) => await Ejecutar(CrearOferta);
    private async void CambiarDocente_Click(object sender, RoutedEventArgs e) => await Ejecutar(CambiarDocente);
    private async void Retirar_Click(object sender, RoutedEventArgs e) => await Ejecutar(RetirarOferta);
    private async void ImportarMatricula_Click(object sender, RoutedEventArgs e) => await Ejecutar(ImportarMatricula);
    private async void PlantillaMatricula_Click(object sender, RoutedEventArgs e) => await Ejecutar(PlantillaMatricula);
    private async void NuevoPeriodo_Click(object sender, RoutedEventArgs e) => await Ejecutar(() => CrearReferencia("periodos", "período"));
    private async void CorregirPeriodo_Click(object sender, RoutedEventArgs e) => await Ejecutar(() => Renombrar(Periodos, "periodos", "período"));
    private async void RevisarCierre_Click(object sender, RoutedEventArgs e) => await Ejecutar(RevisarCierre);
    private async void CerrarPeriodo_Click(object sender, RoutedEventArgs e) => await Ejecutar(CerrarPeriodo);

    private async Task Cargar()
    {
        await Task.WhenAll(CargarPersonas(), CargarDocentes(), CargarNomina(), CargarPeriodos(), CargarCarreras(), CargarMaterias(), CargarSecciones(), CargarOfertas(), CargarAuditoria(), CargarFormato(), CargarReglas());
        PrepararFiltros();
        await CargarEstudiantes();
    }
    private async Task CargarPersonas() => Personas.ItemsSource = paginaPersonas.Recibir(await Api.Obtener<Pagina<Persona>>($"usuarios?rol={Roles.Administrador}&{paginaPersonas.Consulta}"));
    private async Task CargarDocentes() => listaDocentes = await Api.Obtener<List<Persona>>("docentes");
    private async Task CargarNomina() => Docentes.ItemsSource = paginaDocentes.Recibir(await Api.Obtener<Pagina<FilaDocente>>($"nomina-docente?{paginaDocentes.Consulta}"));
    private async Task CargarPeriodos() { listaPeriodos = await Api.Obtener<List<Periodo>>("periodos"); Periodos.ItemsSource = listaPeriodos; }

    // Estudiantes: los del período elegido, y si se elige una sección, solo los inscritos en ella.
    private sealed record OpcionSeccionFiltro(long? Id, string Nombre);

    private void PrepararFiltros()
    {
        var periodo = (FiltroPeriodo.SelectedItem as Periodo)?.Id;
        FiltroPeriodo.ItemsSource = listaPeriodos;
        FiltroPeriodo.SelectedItem = listaPeriodos.FirstOrDefault(p => p.Id == periodo) ?? listaPeriodos.FirstOrDefault(p => !p.Cerrado) ?? listaPeriodos.FirstOrDefault();
        var seccion = (FiltroSeccion.SelectedItem as OpcionSeccionFiltro)?.Id;
        var opciones = new List<OpcionSeccionFiltro> { new(null, "Todas las secciones") };
        opciones.AddRange(listaSecciones.Select(s => new OpcionSeccionFiltro(s.Id, $"{s.Codigo} · {s.Carrera}")));
        FiltroSeccion.ItemsSource = opciones;
        FiltroSeccion.SelectedItem = opciones.FirstOrDefault(o => o.Id == seccion) ?? opciones[0];
    }

    private async Task CargarEstudiantes()
    {
        var periodo = (FiltroPeriodo.SelectedItem as Periodo)?.Id;
        var seccion = (FiltroSeccion.SelectedItem as OpcionSeccionFiltro)?.Id;
        Estudiantes.ItemsSource = paginaEstudiantes.Recibir(await Api.Obtener<Pagina<FilaEstudiante>>(
            $"estudiantes?{paginaEstudiantes.Consulta}" + (periodo is null ? "" : $"&periodo={periodo}") + (seccion is null ? "" : $"&seccion={seccion}")));
    }

    private async Task CargarCuentas() => await Task.WhenAll(CargarPersonas(), CargarDocentes(), CargarNomina(), CargarEstudiantes());

    // La persona elegida en la pestaña visible: docentes, estudiantes o cuentas de administración.
    private Persona PersonaElegida() => AutomationProperties.GetName((DependencyObject)Pestanas.SelectedItem) switch
    {
        "Docentes" => (Docentes.SelectedItem as FilaDocente)?.Persona,
        "Estudiantes" => (Estudiantes.SelectedItem as FilaEstudiante)?.Persona,
        _ => Personas.SelectedItem as Persona,
    } ?? throw new ArgumentException("Selecciona primero una persona de la lista.");
    private async Task CargarCarreras()
    {
        listaCarreras = await Api.Obtener<List<Carrera>>("carreras");
        Carreras.ItemsSource = listaCarreras;
        // Se conserva la carrera elegida en el pensum aunque la lista se recargue.
        var elegida = CarreraElegida?.Id;
        CarreraPensum.ItemsSource = listaCarreras;
        CarreraPensum.SelectedItem = listaCarreras.FirstOrDefault(c => c.Id == elegida) ?? listaCarreras.FirstOrDefault();
        MostrarPensum();
    }
    private async Task CargarMaterias()
    {
        var materias = Api.Obtener<List<Materia>>("materias");
        var pensum = Api.Obtener<List<MateriaPensum>>("pensum");
        (listaMaterias, listaPensum) = (await materias, await pensum);
        MostrarPensum();
    }
    private async Task CargarSecciones() { listaSecciones = await Api.Obtener<List<Seccion>>("secciones"); Secciones.ItemsSource = listaSecciones; }
    private async Task CargarOfertas() => Ofertas.ItemsSource = paginaOfertas.Recibir(await Api.Obtener<Pagina<Oferta>>($"ofertas?{paginaOfertas.Consulta}"));
    private async Task CargarAuditoria() => Auditoria.ItemsSource = paginaAuditoria.Recibir(await Api.Obtener<Pagina<EventoAuditoria>>($"auditoria?{paginaAuditoria.Consulta}"));

    // Cuentas

    private async Task CambiarEstado()
    {
        var persona = PersonaElegida();
        var accion = persona.Activo ? "Desactivar" : "Activar";
        Confirmar($"¿{accion} el acceso de {persona.NombreCompleto}? Se conserva su historial académico.", Propietario,
            $"{accion} cuenta", accion, persona.Activo);
        await Api.Enviar<bool>(HttpMethod.Put, $"usuarios/{persona.Id}/estado", new EstadoCuenta(!persona.Activo));
        await CargarCuentas();
        Notificar($"Cuenta de {persona.NombreCompleto} {(persona.Activo ? "desactivada" : "activada")}.", Tono.Exito);
    }

    private async Task CrearPersona(string rol)
    {
        var (titulo, quien) = rol switch { Roles.Docente => ("Agregar docente", "El docente"), Roles.Estudiante => ("Agregar estudiante", "El estudiante"), _ => ("Agregar administrador", "La persona") };
        var formulario = new Formulario(Propietario, titulo, $"Con correo, {quien.ToLowerInvariant()} crea su contraseña solo desde «¿Primera vez?». Sin correo, administración le entrega un código.");
        var cedula = formulario.Texto("Cédula");
        var nombres = formulario.Texto("Nombres");
        var apellidos = formulario.Texto("Apellidos");
        var correo = formulario.Texto("Correo (opcional)", "", "Con el correo crea y recupera su acceso solo, con un código que le llega ahí.");
        if (formulario.ShowDialog() != true) throw new OperacionCancelada();
        await Api.Enviar<long>(HttpMethod.Post, "usuarios", new NuevaPersona(cedula.Text, nombres.Text, apellidos.Text, rol, correo.Text));
        await CargarCuentas();
        Notificar(string.IsNullOrWhiteSpace(correo.Text)
            ? $"{nombres.Text.Trim()} {apellidos.Text.Trim()} agregado. Emite un código de acceso para que active su cuenta."
            : $"{nombres.Text.Trim()} {apellidos.Text.Trim()} agregado. Ya puede crear su contraseña desde «¿Primera vez?» con su correo.", Tono.Exito);
    }

    private async Task ImportarNomina()
    {
        var abrir = new Microsoft.Win32.OpenFileDialog { Filter = "Libro de Excel|*.xlsx", Multiselect = false };
        if (abrir.ShowDialog(Propietario) != true) throw new OperacionCancelada();
        Logica.ServicioMatricula.ValidarTamano(new System.IO.FileInfo(abrir.FileName).Length);
        var vista = await Api.Subir<VistaNomina>("nomina-docente/leer-excel", await System.IO.File.ReadAllBytesAsync(abrir.FileName));
        if (vista.Errores.Count > 0) { new VentanaErroresExcel(Propietario, vista.Errores, vista.Filas.Count).ShowDialog(); throw new OperacionCancelada(); }
        var nuevos = vista.Filas.Count(f => f.Accion == "Docente nuevo");
        var revision = new VentanaImportacion(Propietario, "Revisar nómina docente", Cantidad(vista.Filas.Count, "docente", "docentes"),
            $"{Cantidad(nuevos, "docente nuevo", "docentes nuevos")}; los que ya están no se duplican.",
            vista.Filas, "Cédula=CedulaTexto; Docente=NombreCompleto; Correo=Correo; Qué pasa=Accion", "Importar nómina");
        if (revision.ShowDialog() != true) throw new OperacionCancelada();
        var resultado = await Api.Enviar<ResultadoNomina>(HttpMethod.Post, "nomina-docente/importar", new ImportarNomina(vista.Filas));
        await CargarCuentas();
        Notificar($"Nómina importada: {Cantidad(resultado.Nuevos, "docente nuevo", "docentes nuevos")}" +
            (resultado.Actualizados > 0 ? $", {resultado.Actualizados} con correo completado" : "") + ".", Tono.Exito);
    }

    private async Task Plantilla(string ruta, string nombre)
    {
        var guardar = new Microsoft.Win32.SaveFileDialog { FileName = nombre, Filter = "Libro de Excel|*.xlsx" };
        if (guardar.ShowDialog(Propietario) != true) throw new OperacionCancelada();
        await System.IO.File.WriteAllBytesAsync(guardar.FileName, await Api.Descargar(ruta));
        Notificar($"Plantilla guardada en {guardar.FileName}.", Tono.Exito);
    }

    private async Task CorregirCorreo()
    {
        var persona = PersonaElegida();
        var formulario = new Formulario(Propietario, "Corregir correo", persona.Registrada
            ? $"{persona.NombreCompleto} ya creó su acceso. Este correo es el que usará para recuperar su contraseña."
            : $"{persona.NombreCompleto} todavía no creó su acceso: lo hará con un código que llegará a este correo.");
        var correo = formulario.Texto("Correo", persona.Correo ?? "", "Vacío si la cuenta no tiene correo.");
        if (formulario.ShowDialog() != true) throw new OperacionCancelada();
        await Api.Enviar<int>(HttpMethod.Put, $"usuarios/{persona.Id}/correo", new CambioCorreo(correo.Text));
        await CargarCuentas();
        Notificar($"Correo de {persona.NombreCompleto} actualizado.", Tono.Exito);
    }

    private async Task EmitirCodigo()
    {
        var persona = PersonaElegida();
        Confirmar($"¿Verificaste la identidad de {persona.NombreCompleto}? Se emitirá un código para establecer o recuperar su contraseña.",
            Propietario, "Emitir código de acceso", "Emitir código");
        var codigo = await Api.Enviar<CodigoAcceso>(HttpMethod.Post, $"usuarios/{persona.Id}/codigo", new { });
        new VentanaCodigo(Propietario, persona, codigo).ShowDialog();
        Notificar($"Código emitido para {persona.NombreCompleto}.", Tono.Exito);
    }

    // Estructura

    private async Task CrearReferencia(string tipo, string singular)
    {
        var formulario = new Formulario(Propietario, $"Agregar {singular}", "Usa la denominación oficial de la institución.");
        var nombre = formulario.Texto("Nombre");
        if (formulario.ShowDialog() != true) throw new OperacionCancelada();
        await Api.Enviar<long>(HttpMethod.Post, $"catalogos/{tipo}", new Referencia(0, nombre.Text));
        await Cargar();
        Notificar($"Registro agregado: «{nombre.Text.Trim()}».", Tono.Exito);
    }

    private async Task Renombrar(DataGrid tabla, string tipo, string singular)
    {
        var (id, actual) = tabla.SelectedItem switch
        {
            Referencia r => (r.Id, r.Nombre),
            Periodo p when p.Cerrado => throw new ArgumentException("Un período cerrado conserva su nombre: ya aparece en los récords."),
            Periodo p => (p.Id, p.Nombre),
            _ => throw new ArgumentException(tipo == "periodos" ? "Selecciona el período que quieres corregir." : $"Selecciona la {singular} que quieres corregir.")
        };
        var formulario = new Formulario(Propietario, $"Corregir {singular}", "El cambio se ve en todas las planillas y récords donde aparece.");
        var nombre = formulario.Texto("Nombre", actual);
        if (formulario.ShowDialog() != true) throw new OperacionCancelada();
        await Api.Enviar<int>(HttpMethod.Put, $"catalogos/{tipo}/{id}", new CambioNombre(nombre.Text));
        await Cargar();
        Notificar("Nombre corregido.", Tono.Exito);
    }

    // Carreras y secciones con código

    private string formatoSeccion = Logica.FormatoSeccion.PorDefecto;
    private async Task CargarFormato() => formatoSeccion = (await Api.Obtener<FormatoCodigo>("formato-seccion")).Formato;

    // Vista previa: el mismo cálculo que hace el servidor, sin guardar nada.
    private static string Previa(string formato, string carrera, string? codigoCarrera, int semestre, string seccion)
    {
        try { return "Código: " + Logica.FormatoSeccion.Generar(Logica.FormatoSeccion.Validar(formato), carrera, codigoCarrera, semestre, seccion.Trim().Length == 0 ? "…" : seccion.Trim()); }
        catch (Logica.ReglaException error) { return error.Message; }
    }

    private static int LeerSemestre(string texto) => texto.Trim().Length == 0 ? 0
        : int.TryParse(texto.Trim(), out var n) ? n : throw new ArgumentException("El semestre es un número, por ejemplo 2. Déjalo vacío si no aplica.");

    private Task CorregirCarrera() => Carreras.SelectedItem is Carrera carrera ? EditarCarrera(carrera)
        : throw new ArgumentException("Selecciona la carrera que quieres corregir.");

    private async Task EditarCarrera(Carrera? carrera)
    {
        var usaCodigo = Logica.FormatoSeccion.UsaCarrera(formatoSeccion);
        var formulario = new Formulario(Propietario, carrera is null ? "Agregar carrera" : "Corregir carrera",
            "Usa la denominación oficial. Si cambias el código, se actualizan los códigos de todas sus secciones.");
        var nombre = formulario.Texto("Nombre", carrera?.Nombre ?? "");
        var codigo = formulario.Texto("Código de la carrera" + (usaCodigo ? "" : " (opcional)"), carrera?.Codigo ?? "",
            usaCodigo ? "El formato de sección lo usa, por ejemplo 2603." : "El que usa la universidad, por ejemplo 2603. Se usa si el formato de sección lo incluye.");
        if (formulario.ShowDialog() != true) throw new OperacionCancelada();
        var datos = new NuevaCarrera(nombre.Text, codigo.Text);
        if (carrera is null) await Api.Enviar<long>(HttpMethod.Post, "carreras", datos);
        else await Api.Enviar<int>(HttpMethod.Put, $"carreras/{carrera.Id}", datos);
        await Cargar();
        Notificar(carrera is null ? $"Carrera agregada: «{nombre.Text.Trim()}»." : "Carrera corregida.", Tono.Exito);
    }

    private Task CorregirSeccion() => Secciones.SelectedItem is Seccion seccion ? EditarSeccion(seccion)
        : throw new ArgumentException("Selecciona la sección que quieres corregir.");

    private async Task EditarSeccion(Seccion? seccion)
    {
        if (seccion is null && listaCarreras.Count == 0) throw new ArgumentException("Primero agrega la carrera en la pestaña Carreras.");
        var formulario = new Formulario(Propietario, seccion is null ? "Agregar sección" : "Corregir sección",
            seccion is null ? "Una sección pertenece a una carrera. El código se arma con el formato que definiste." : $"{seccion.Carrera}. El cambio se ve en todas las planillas y récords.");
        var carrera = seccion is null ? formulario.Seleccion("Carrera", listaCarreras, "Nombre") : null;
        var semestre = formulario.Texto("Semestre" + (Logica.FormatoSeccion.UsaSemestre(formatoSeccion) ? "" : " (opcional)"),
            seccion is { Semestre: > 0 } ? seccion.Semestre.ToString() : "", "Número del semestre, por ejemplo 2.");
        var nombre = formulario.Texto("Sección", seccion?.Nombre ?? "", "El identificador de la sección, por ejemplo A o D1.");
        var previa = formulario.Vista();
        void Actualizar()
        {
            var elegida = carrera?.SelectedItem as Carrera ?? listaCarreras.FirstOrDefault(c => c.Id == seccion?.CarreraId);
            if (elegida is null) { previa.Text = "Elige la carrera para ver el código."; return; }
            previa.Text = int.TryParse(semestre.Text.Trim(), out var n) || semestre.Text.Trim().Length == 0
                ? Previa(formatoSeccion, elegida.Nombre, elegida.Codigo, n, nombre.Text)
                : "El semestre es un número.";
        }
        semestre.TextChanged += (_, _) => Actualizar();
        nombre.TextChanged += (_, _) => Actualizar();
        if (carrera is not null) carrera.Elegido += (_, _) => Actualizar();
        Actualizar();
        if (formulario.ShowDialog() != true) throw new OperacionCancelada();
        if (seccion is null)
            await Api.Enviar<long>(HttpMethod.Post, "secciones", new NuevaSeccion(Formulario.Elegido<Carrera>(carrera!).Id, nombre.Text, LeerSemestre(semestre.Text)));
        else
            await Api.Enviar<int>(HttpMethod.Put, $"secciones/{seccion.Id}", new CambioSeccion(nombre.Text, LeerSemestre(semestre.Text)));
        await CargarSecciones();
        Notificar(seccion is null ? "Sección agregada." : "Sección corregida.", Tono.Exito);
    }

    private async Task CambiarFormato()
    {
        var ejemplo = listaSecciones.FirstOrDefault(s => s.Semestre > 0) ?? listaSecciones.FirstOrDefault();
        var carreraEjemplo = listaCarreras.FirstOrDefault(c => c.Id == ejemplo?.CarreraId);
        var actual = PiezasFormato.Leer(formatoSeccion);
        var formulario = new Formulario(Propietario, "Código de las secciones",
            "Arma el código como lo usa la universidad. Al guardar, se actualiza el de todas las secciones.", 520);
        Referencia[] semestres = [new(0, "No lleva semestre"), new(2, "Con dos cifras: 02"), new(1, "Sin ceros: 2")];
        Referencia[] carreras = [new(1, "Sí, el código de la carrera"), new(0, "No")];
        var prefijo = formulario.Texto("Empieza con (opcional)", actual.Prefijo, "Un texto fijo al inicio, por ejemplo SEC. Casi siempre va vacío.");
        var semestre = formulario.Seleccion("Semestre", semestres, "Nombre", semestres.First(s => s.Id == actual.Semestre));
        var letra = formulario.Texto("Letra después del semestre (opcional)", actual.Letra, "Por ejemplo S, para que quede 02S.");
        var carrera = formulario.Seleccion("¿Lleva la carrera?", carreras, "Nombre", carreras.First(c => c.Id == (actual.Carrera ? 1 : 0)));
        var separador = formulario.Texto("Separador", actual.Separador, "Lo que va entre las partes, por ejemplo un guion. Puede ir vacío.");
        var previa = formulario.Vista();
        string Formato() => PiezasFormato.Armar(prefijo.Text, (int)((semestre.SelectedItem as Referencia)?.Id ?? 0), letra.Text,
            (carrera.SelectedItem as Referencia)?.Id == 1, separador.Text);
        void Actualizar() => previa.Text = ejemplo is null
            ? Previa(Formato(), "Ing. de Sistemas", "2603", 2, "D1").Replace("Código:", "Por ejemplo, carrera 2603, semestre 2 y sección D1:")
            : Previa(Formato(), ejemplo.Carrera, carreraEjemplo?.Codigo, ejemplo.Semestre, ejemplo.Nombre).Replace("Código:", $"Así quedaría {ejemplo.Codigo}:");
        prefijo.TextChanged += (_, _) => Actualizar();
        letra.TextChanged += (_, _) => Actualizar();
        separador.TextChanged += (_, _) => Actualizar();
        semestre.Elegido += (_, _) => Actualizar();
        carrera.Elegido += (_, _) => Actualizar();
        Actualizar();
        if (formulario.ShowDialog() != true) throw new OperacionCancelada();
        var resultado = await Api.Enviar<ResultadoFormato>(HttpMethod.Put, "formato-seccion", new FormatoCodigo(Formato()));
        formatoSeccion = resultado.Formato;
        await Task.WhenAll(CargarSecciones(), CargarOfertas());
        Notificar($"Formato guardado. Se revisaron los códigos de {resultado.Secciones} secciones.", Tono.Exito);
    }

    // Pensum

    private Carrera? CarreraElegida => CarreraPensum.SelectedItem as Carrera;

    private void MostrarPensum()
    {
        var carrera = CarreraElegida;
        var filas = carrera is null ? [] : listaPensum.Where(m => m.CarreraId == carrera.Id).Select(m => m with { General = reglas?.NotaAprobatoria ?? 0 }).ToList();
        Materias.ItemsSource = filas;
        var semestres = filas.Where(m => m.Semestre > 0).Select(m => m.Semestre).Distinct().Count();
        ResumenPensum.Text = carrera is null ? "" : filas.Count == 0 ? "Sin materias todavía. Agrégalas una por una o importa el Excel del pensum."
            : $"{Cantidad(filas.Count, "materia", "materias")}, {filas.Sum(m => m.Uc)} UC" + (semestres > 0 ? $" en {Cantidad(semestres, "semestre", "semestres")}" : "");
    }

    private Carrera ExigirCarrera() => CarreraElegida ?? throw new ArgumentException(listaCarreras.Count == 0
        ? "Primero agrega la carrera en la pestaña Carreras." : "Elige arriba la carrera del pensum.");

    private static int LeerEntero(string texto, string campo, int vacio = 0) => texto.Trim().Length == 0 ? vacio
        : int.TryParse(texto.Trim(), out var n) ? n : throw new ArgumentException($"{campo}: escribe un número entero.");

    private static decimal? LeerNotaOpcional(string texto) => texto.Trim().Length == 0 ? null : Logica.ReglasAcademicas.LeerDecimal(texto);

    private Task CrearMateria() => EditarMateria(null);

    private Task CorregirMateria() => Materias.SelectedItem is MateriaPensum materia ? EditarMateria(materia)
        : throw new ArgumentException("Selecciona la materia que quieres corregir.");

    private async Task EditarMateria(MateriaPensum? materia)
    {
        var carrera = ExigirCarrera();
        var compartida = materia is { Pensums: > 1 };
        var formulario = new Formulario(Propietario, materia is null ? $"Materia nueva en {carrera.Nombre}" : "Corregir materia",
            materia is null ? "Si la materia ya existe en otra carrera (ciclo básico), usa «Agregar existente» para no duplicarla."
            : compartida ? $"Está en el pensum de {materia.Pensums} carreras: código, nombre y UC cambian en todas. El semestre es solo de {carrera.Nombre}."
            : "Los períodos cerrados conservan las UC y la nota aprobatoria con las que cerraron.");
        var codigo = formulario.Texto("Código", materia?.Codigo ?? "", "Como en el pensum oficial, por ejemplo MAT-21114.");
        var nombre = formulario.Texto("Nombre", materia?.Nombre ?? "");
        var uc = formulario.Texto("Unidades de crédito", materia?.Uc.ToString() ?? "");
        var semestre = formulario.Texto($"Semestre en {carrera.Nombre}", materia is { Semestre: > 0 } ? materia.Semestre.ToString() : "", "Vacío si es electiva.");
        var aprobatoria = formulario.Texto("Nota aprobatoria propia (opcional)", materia?.NotaAprobatoria?.ToString("0.##") ?? "",
            $"Solo si esta materia aprueba con otra nota, por ejemplo un proyecto. Vacío: aprueba con {reglas?.NotaAprobatoria:0.##}, como las demás.");
        if (formulario.ShowDialog() != true) throw new OperacionCancelada();
        var datos = new NuevaMateria(codigo.Text, nombre.Text, LeerEntero(uc.Text, "Unidades de crédito", -1), LeerNotaOpcional(aprobatoria.Text));
        var enSemestre = LeerEntero(semestre.Text, "Semestre");
        if (materia is null)
            await Api.Enviar<long>(HttpMethod.Post, $"materias?carrera={carrera.Id}&semestre={enSemestre}", datos);
        else
        {
            await Api.Enviar<int>(HttpMethod.Put, $"materias/{materia.MateriaId}", datos);
            if (enSemestre != materia.Semestre) await Api.Enviar<int>(HttpMethod.Put, $"carreras/{carrera.Id}/pensum", new EnPensum(materia.MateriaId, enSemestre));
        }
        await CargarMaterias();
        Notificar(materia is null ? $"Materia agregada al pensum: {datos.Codigo.Trim().ToUpperInvariant()} {datos.Nombre.Trim()}." : "Materia corregida.", Tono.Exito);
    }

    // Ciclo básico: la misma materia, con su mismo código, en el pensum de otra carrera.
    private async Task AgregarExistente()
    {
        var carrera = ExigirCarrera();
        var yaEstan = listaPensum.Where(m => m.CarreraId == carrera.Id).Select(m => m.MateriaId).ToHashSet();
        var disponibles = listaMaterias.Where(m => !yaEstan.Contains(m.Id)).Select(m => new OpcionMateria(m)).ToList();
        if (disponibles.Count == 0) throw new ArgumentException("No hay materias de otras carreras para agregar. Crea una con «Materia nueva».");
        var formulario = new Formulario(Propietario, $"Agregar al pensum de {carrera.Nombre}",
            "Elige una materia que ya existe, por ejemplo del ciclo básico. Es la misma materia: no se duplica.");
        var materia = formulario.Seleccion("Materia", disponibles, "Nombre", ayuda: "Escribe parte del código o del nombre.");
        var semestre = formulario.Texto($"Semestre en {carrera.Nombre}", "", "Vacío si es electiva.");
        if (formulario.ShowDialog() != true) throw new OperacionCancelada();
        var elegida = Formulario.Elegido<OpcionMateria>(materia).Materia;
        await Api.Enviar<int>(HttpMethod.Put, $"carreras/{carrera.Id}/pensum", new EnPensum(elegida.Id, LeerEntero(semestre.Text, "Semestre")));
        await CargarMaterias();
        Notificar($"{elegida.Descripcion} agregada al pensum de {carrera.Nombre}.", Tono.Exito);
    }

    private sealed record OpcionMateria(Materia Materia)
    {
        public string Nombre => Materia.Carreras.Length == 0 ? Materia.Descripcion : $"{Materia.Descripcion} ({Materia.Carreras})";
    }

    private async Task QuitarMateria()
    {
        var carrera = ExigirCarrera();
        if (Materias.SelectedItem is not MateriaPensum materia) throw new ArgumentException("Selecciona la materia que quieres quitar del pensum.");
        Confirmar($"¿Quitar {materia.Descripcion} del pensum de {carrera.Nombre}? " +
            (materia.Pensums > 1 ? "Sigue en las otras carreras que la tienen." : "La materia se conserva y se puede volver a agregar.") +
            " Si ya se asignó en esta carrera, no se puede quitar.", Propietario, "Quitar del pensum", "Quitar", true);
        await Api.Enviar<int>(HttpMethod.Delete, $"carreras/{carrera.Id}/pensum/{materia.MateriaId}");
        await CargarMaterias();
        Notificar($"{materia.Descripcion} ya no está en el pensum de {carrera.Nombre}.", Tono.Exito);
    }

    private async Task PlantillaPensum()
    {
        var guardar = new Microsoft.Win32.SaveFileDialog { FileName = "pensum.xlsx", Filter = "Libro de Excel|*.xlsx" };
        if (guardar.ShowDialog(Propietario) != true) throw new OperacionCancelada();
        await System.IO.File.WriteAllBytesAsync(guardar.FileName, await Api.Descargar("plantilla-pensum"));
        Notificar($"Plantilla guardada en {guardar.FileName}.", Tono.Exito);
    }

    private async Task ImportarPensum()
    {
        var carrera = ExigirCarrera();
        var abrir = new Microsoft.Win32.OpenFileDialog { Filter = "Libro de Excel|*.xlsx", Multiselect = false };
        if (abrir.ShowDialog(Propietario) != true) throw new OperacionCancelada();
        Logica.ServicioMatricula.ValidarTamano(new System.IO.FileInfo(abrir.FileName).Length);
        var vista = await Api.Subir<VistaPensum>($"carreras/{carrera.Id}/pensum/leer-excel", await System.IO.File.ReadAllBytesAsync(abrir.FileName));
        if (vista.Errores.Count > 0) { new VentanaErroresExcel(Propietario, vista.Errores.OrderBy(e => e.Fila).ToList(), vista.Filas.Count).ShowDialog(); throw new OperacionCancelada(); }
        var cambios = vista.Filas.Count(f => f.Accion != "Sin cambios");
        var revision = new VentanaImportacion(Propietario, "Revisar pensum", Cantidad(vista.Filas.Count, "materia", "materias"),
            $"Pensum de {carrera.Nombre}. {Cantidad(cambios, "cambio", "cambios")}; lo que ya estaba igual no se toca. Las materias que ya existen en otra carrera no se duplican.",
            vista.Filas, "Semestre=SemestreTexto; Código=Codigo; Materia=Nombre; UC=Uc; Qué pasa=Accion", "Importar pensum");
        if (revision.ShowDialog() != true) throw new OperacionCancelada();
        var resultado = await Api.Enviar<ResultadoPensum>(HttpMethod.Post, $"carreras/{carrera.Id}/pensum/importar", new ImportarPensum(vista.Filas));
        await CargarMaterias();
        Notificar($"Pensum importado: {Cantidad(resultado.Nuevas, "materia nueva", "materias nuevas")}, {resultado.Agregadas} de otras carreras, " +
            $"{resultado.Cambiadas} con semestre corregido y {resultado.SinCambios} sin cambios.", Tono.Exito);
    }

    // Reglas de calificación

    private ReglasCalificacion? reglas;
    private async Task CargarReglas()
    {
        reglas = await Api.Obtener<ReglasCalificacion>("reglas");
        MostrarPensum();
    }

    private static readonly Referencia[] Redondeos =
    [
        new(1, "Medio punto: menos de 0,50 baja, 0,50 se queda, más de 0,50 sube"),
        new(2, "Al entero: desde 0,50 sube"),
        new(3, "Sin redondeo: dos decimales"),
    ];
    private static readonly Referencia[] TiposIndice = [new(1, "Ponderado por unidades de crédito"), new(2, "Promedio simple de las definitivas")];
    private static readonly Referencia[] SiNo = [new(1, "Sí, todas las definitivas"), new(2, "No, solo las aprobadas")];

    private async Task CambiarReglas()
    {
        await CargarReglas();
        var actuales = reglas!;
        var formulario = new Formulario(Propietario, "Reglas de calificación",
            "Las que usa la universidad. La escala se fija antes de la primera nota; lo demás se puede ajustar después, y cada período cerrado conserva las reglas con que cerró.", 600);
        var (minima, maxima) = formulario.Par("Escala: desde", actuales.EscalaMinima.ToString("0.##"), "hasta", actuales.EscalaMaxima.ToString("0.##"));
        var aprobatoria = formulario.Texto("Nota aprobatoria", actuales.NotaAprobatoria.ToString("0.##"), "Una materia puede tener la suya en el pensum, por ejemplo un proyecto con 16.");
        var redondeo = formulario.Seleccion("Redondeo de la definitiva", Redondeos, "Nombre",
            Redondeos[actuales.Redondeo switch { Entidades.Redondeos.Entero => 1, Entidades.Redondeos.Ninguno => 2, _ => 0 }]);
        var indice = formulario.Seleccion("Índice del récord", TiposIndice, "Nombre", TiposIndice[actuales.Indice == Indices.Simple ? 1 : 0]);
        var aplazadas = formulario.Seleccion("¿El índice cuenta las materias aplazadas?", SiNo, "Nombre", SiNo[actuales.IndiceConAplazadas ? 0 : 1]);
        var previa = formulario.Vista();
        ReglasCalificacion Armar() => new(Logica.ReglasAcademicas.LeerDecimal(minima.Text), Logica.ReglasAcademicas.LeerDecimal(maxima.Text),
            Logica.ReglasAcademicas.LeerDecimal(aprobatoria.Text),
            (redondeo.SelectedItem as Referencia)?.Id switch { 2 => Entidades.Redondeos.Entero, 3 => Entidades.Redondeos.Ninguno, _ => Entidades.Redondeos.Media },
            (indice.SelectedItem as Referencia)?.Id == 2 ? Indices.Simple : Indices.Ponderado, (aplazadas.SelectedItem as Referencia)?.Id != 2);
        void Actualizar()
        {
            try
            {
                var r = Logica.Calificacion.Validar(Armar());
                var acumulado = r.NotaAprobatoria - 0.5m;
                var definitiva = Logica.Calificacion.Redondear(acumulado, r.Redondeo);
                previa.Text = $"Ejemplo: con {acumulado:0.##} acumulado la definitiva queda {definitiva:0.##}, " +
                    $"{(Logica.Calificacion.Condicion(definitiva, r.NotaAprobatoria) == Condiciones.Aprobado ? "aprobada" : "aplazada")}.";
            }
            catch (Exception error) when (error is Logica.ReglaException or ArgumentException) { previa.Text = error.Message; }
        }
        foreach (var caja in new[] { minima, maxima, aprobatoria }) caja.TextChanged += (_, _) => Actualizar();
        redondeo.Elegido += (_, _) => Actualizar();
        Actualizar();
        if (formulario.ShowDialog() != true) throw new OperacionCancelada();
        reglas = await Api.Enviar<ReglasCalificacion>(HttpMethod.Put, "reglas", Armar());
        MostrarPensum();
        Notificar($"Reglas guardadas: notas de {reglas.EscalaMinima:0.##} a {reglas.EscalaMaxima:0.##}, aprueba con {reglas.NotaAprobatoria:0.##}.", Tono.Exito);
    }
    // Carga académica

    private sealed record OpcionSeccion(Seccion Seccion)
    {
        public string Nombre => $"{Seccion.Carrera}, sección {Seccion.Codigo}";
    }

    private IEnumerable<MateriaPensum> MateriasDe(Seccion? seccion) =>
        seccion is null ? [] : listaPensum.Where(m => m.CarreraId == seccion.CarreraId);

    private async Task CrearOferta()
    {
        var formulario = new Formulario(Propietario, "Asignar una materia", "Para varias materias de la misma sección es más rápido «Asignar por sección».");
        var periodo = formulario.Seleccion("Período", listaPeriodos.Where(p => !p.Cerrado), "Nombre", listaPeriodos.FirstOrDefault(p => !p.Cerrado));
        var seccion = formulario.Seleccion("Sección", listaSecciones.Select(s => new OpcionSeccion(s)), "Nombre");
        var materia = formulario.Seleccion("Materia", MateriasDe(null), "Descripcion", ayuda: "Primero elige la sección: aparecen las materias del pensum de su carrera.");
        seccion.Elegido += (_, _) =>
        {
            var disponibles = MateriasDe((seccion.SelectedItem as OpcionSeccion)?.Seccion).ToList();
            materia.ItemsSource = disponibles;
            if (materia.SelectedItem is MateriaPensum m && !disponibles.Contains(m)) materia.SelectedItem = null;
        };
        var docente = formulario.Seleccion("Docente", listaDocentes, "ConCedula");
        if (formulario.ShowDialog() != true) throw new OperacionCancelada();
        await Api.Enviar<long>(HttpMethod.Post, "ofertas", new NuevaOferta(Formulario.Elegido<Periodo>(periodo).Id,
            Formulario.Elegido<OpcionSeccion>(seccion).Seccion.Id, Formulario.Elegido<MateriaPensum>(materia).MateriaId, Formulario.Elegido<Persona>(docente).Id));
        await CargarOfertas();
        Notificar("Materia asignada.", Tono.Exito);
    }

    private async Task AsignarEnBloque()
    {
        if (!listaPeriodos.Any(p => !p.Cerrado)) throw new ArgumentException("Primero crea un período abierto en la pestaña Períodos.");
        if (listaSecciones.Count == 0) throw new ArgumentException("Primero crea las secciones en Estructura.");
        var ventana = new VentanaCargaSeccion(Api, Propietario, listaPeriodos.Where(p => !p.Cerrado).ToList(), listaSecciones, listaPensum, listaDocentes);
        if (ventana.ShowDialog() != true || ventana.Resultado is not { } resultado) throw new OperacionCancelada();
        await CargarOfertas();
        Notificar(resultado.Existentes == 0 ? $"{Cantidad(resultado.Creadas, "materia asignada", "materias asignadas")}."
            : $"{Cantidad(resultado.Creadas, "materia asignada", "materias asignadas")}; {resultado.Existentes} ya lo estaban.", Tono.Exito);
    }

    private static string Cantidad(int n, string singular, string plural) => $"{n} {(n == 1 ? singular : plural)}";

    private async Task CopiarCarga()
    {
        var abiertos = listaPeriodos.Where(p => !p.Cerrado).ToList();
        if (abiertos.Count == 0) throw new ArgumentException("Primero crea el período nuevo en la pestaña Períodos.");
        var formulario = new Formulario(Propietario, "Copiar carga académica",
            "Repite en el período nuevo las mismas materias, secciones y docentes de otro período. Lo que ya esté asignado no se toca.");
        var destino = formulario.Seleccion("Copiar al período", abiertos, "Nombre", abiertos[0]);
        var origen = formulario.Seleccion("Tomar la carga de", listaPeriodos.Where(p => p != abiertos[0]), "Nombre", listaPeriodos.FirstOrDefault(p => p != abiertos[0]));
        if (formulario.ShowDialog() != true) throw new OperacionCancelada();
        var hacia = Formulario.Elegido<Periodo>(destino);
        var desde = Formulario.Elegido<Periodo>(origen);
        var resultado = await Api.Enviar<ResultadoCarga>(HttpMethod.Post, $"periodos/{hacia.Id}/copiar-carga", new CopiaDeCarga(desde.Id));
        await CargarOfertas();
        var texto = $"{Cantidad(resultado.Creadas, "materia copiada", "materias copiadas")} de {desde.Nombre} a {hacia.Nombre}.";
        if (resultado.Existentes > 0) texto += $" {resultado.Existentes} ya estaban asignadas.";
        if (resultado.SinDocente > 0)
        {
            Dialogo.Informar(Propietario, "Carga copiada con pendientes", texto + $" {Cantidad(resultado.SinDocente, "materia quedó", "materias quedaron")} sin copiar porque su docente " +
                "está desactivado. Asígnalas con «Asignar por sección».", Tono.Aviso);
            return;
        }
        Notificar(texto, Tono.Exito);
    }

    private async Task CambiarDocente()
    {
        if (Ofertas.SelectedItem is not Oferta oferta) throw new ArgumentException("Selecciona una materia de la carga académica.");
        var formulario = new Formulario(Propietario, "Cambiar docente", $"{oferta.Descripcion}. Se conservan las notas y publicaciones; el nuevo docente deberá publicar la versión vigente.");
        var docente = formulario.Seleccion("Nuevo docente", listaDocentes, "ConCedula");
        if (formulario.ShowDialog() != true) throw new OperacionCancelada();
        await Api.Enviar<int>(HttpMethod.Put, $"ofertas/{oferta.Id}/docente", new CambioDocente(Formulario.Elegido<Persona>(docente).Id, oferta.Revision));
        await CargarOfertas();
        Notificar("Docente actualizado.", Tono.Exito);
    }

    // Matrícula del período: el listado de control de estudios, todas las secciones en un solo Excel.
    private async Task ImportarMatricula()
    {
        var abiertos = listaPeriodos.Where(p => !p.Cerrado).ToList();
        if (abiertos.Count == 0) throw new ArgumentException("Primero crea un período abierto en la pestaña Períodos.");
        var formulario = new Formulario(Propietario, "Importar estudiantes inscritos",
            "Elige el período y luego el Excel de inscritos. Cada estudiante queda en las materias de su sección; antes de guardar verás la lista.");
        var periodo = formulario.Seleccion("Período", abiertos, "Nombre", abiertos[0]);
        if (formulario.ShowDialog() != true) throw new OperacionCancelada();
        var elegido = Formulario.Elegido<Periodo>(periodo);
        var abrir = new Microsoft.Win32.OpenFileDialog { Filter = "Libro de Excel|*.xlsx", Multiselect = false };
        if (abrir.ShowDialog(Propietario) != true) throw new OperacionCancelada();
        Logica.ServicioMatricula.ValidarTamano(new System.IO.FileInfo(abrir.FileName).Length);
        var vista = await Api.Subir<VistaMatriculaPeriodo>($"periodos/{elegido.Id}/matricula/leer-excel", await System.IO.File.ReadAllBytesAsync(abrir.FileName));
        if (vista.Errores.Count > 0)
        {
            // Correctas: las filas del plan sin ningún error; las que fallaron al leerse ni siquiera llegan al plan.
            new VentanaErroresExcel(Propietario, vista.Errores, vista.Plan.Select(p => p.Fila).Except(vista.Errores.Select(e => e.Fila)).Count()).ShowDialog();
            throw new OperacionCancelada();
        }
        var secciones = vista.Plan.Select(p => p.Seccion).Distinct().ToList();
        var detalle = $"Período {elegido.Nombre}, " + (secciones.Count == 1 ? $"sección {secciones[0]}" : Cantidad(secciones.Count, "sección", "secciones")) +
            ". Cada estudiante queda inscrito en las materias de su sección." +
            (vista.Omitidas.Count > 0 ? $" {Cantidad(vista.Omitidas.Count, "materia no se inscribe", "materias no se inscriben")} porque ya la aprobó o ya la cursa en otra sección; el motivo está en la lista." : "");
        var revision = new VentanaImportacion(Propietario, "Revisar estudiantes inscritos", Cantidad(vista.Estudiantes, "estudiante", "estudiantes"), detalle,
            vista.Plan, "Cédula=CedulaTexto; Estudiante=Estudiante; Correo=Correo; Sección=Seccion; Qué pasa=Detalle", "Importar estudiantes");
        if (revision.ShowDialog() != true) throw new OperacionCancelada();
        var resultado = await Api.Enviar<ResultadoMatriculaPeriodo>(HttpMethod.Post, $"periodos/{elegido.Id}/matricula", new ImportarMatriculaPeriodo(vista.Filas));
        FiltroPeriodo.SelectedItem = listaPeriodos.FirstOrDefault(p => p.Id == elegido.Id) ?? FiltroPeriodo.SelectedItem;
        await Task.WhenAll(CargarOfertas(), CargarEstudiantes());
        Notificar($"{Cantidad(resultado.Estudiantes, "estudiante inscrito", "estudiantes inscritos")} en {elegido.Nombre}, cada uno en las materias de su sección." +
            (resultado.Omitidas.Count > 0 ? $" {Cantidad(resultado.Omitidas.Count, "materia no se inscribió", "materias no se inscribieron")} porque ya estaba aprobada o en otra sección." : ""),
            resultado.Omitidas.Count > 0 ? Tono.Aviso : Tono.Exito);
    }

    private async Task PlantillaMatricula()
    {
        var guardar = new Microsoft.Win32.SaveFileDialog { FileName = "matricula.xlsx", Filter = "Libro de Excel|*.xlsx" };
        if (guardar.ShowDialog(Propietario) != true) throw new OperacionCancelada();
        await System.IO.File.WriteAllBytesAsync(guardar.FileName, await Api.Descargar("plantilla-matricula"));
        Notificar($"Plantilla guardada en {guardar.FileName}.", Tono.Exito);
    }

    private async Task RetirarOferta()
    {
        if (Ofertas.SelectedItem is not Oferta oferta) throw new ArgumentException("Selecciona la materia que quieres retirar.");
        if (oferta.Matriculados + oferta.Pendientes > 0 || oferta.RevisionPublicada is not null)
            throw new ArgumentException("Esta materia ya tiene estudiantes o publicaciones. Si el problema es el docente, usa «Cambiar docente».");
        Confirmar($"¿Retirar {oferta.Descripcion} de la carga académica? Solo se puede si todavía no tiene evaluaciones ni estudiantes.",
            Propietario, "Retirar materia", "Retirar", true);
        await Api.Enviar<int>(HttpMethod.Post, $"ofertas/{oferta.Id}/retirar", new RevisionEsperada(oferta.Revision));
        await CargarOfertas();
        Notificar($"{oferta.Materia} retirada de la sección {oferta.Seccion}.", Tono.Exito);
    }

    // Períodos

    private async Task RevisarCierre()
    {
        if (Periodos.SelectedItem is not Periodo periodo) throw new ArgumentException("Selecciona un período.");
        var resumen = await Api.Obtener<ResumenCierre>($"periodos/{periodo.Id}/cierre");
        new VentanaCierre(Propietario, resumen).ShowDialog();
    }

    private async Task CerrarPeriodo()
    {
        if (Periodos.SelectedItem is not Periodo periodo) throw new ArgumentException("Selecciona un período.");
        var resumen = await Api.Obtener<ResumenCierre>($"periodos/{periodo.Id}/cierre");
        if (!resumen.PuedeCerrar) throw new ArgumentException("El período tiene pendientes o ya está cerrado. Usa «Revisar cierre» para ver el detalle.");
        Confirmar($"¿Cerrar el período {periodo.Nombre}? Las calificaciones quedarán bloqueadas para edición.", Propietario, "Cerrar período", "Cerrar período", true);
        await Api.Enviar<ResumenCierre>(HttpMethod.Post, $"periodos/{periodo.Id}/cerrar", new { });
        await CargarPeriodos();
        Notificar($"Período {periodo.Nombre} cerrado.", Tono.Exito);
    }
}
