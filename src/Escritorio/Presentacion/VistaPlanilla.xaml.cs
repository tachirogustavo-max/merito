using System.Data;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Entidades;
using Escritorio.Servicios;
using Logica;
using Microsoft.Win32;

namespace Escritorio.Presentacion;

public sealed partial class VistaPlanilla : VistaBase
{
    private const string Pendiente = "Pendiente";
    private const string Revisar = "Revisar nota";
    private readonly long ofertaId;
    private readonly bool docente;
    private Planilla? planilla;
    private DataTable datos = new();
    private bool cambios;
    private readonly HashSet<(long Estudiante, long Evaluacion)> celdasModificadas = [];
    private bool calculando;
    private string filtroNotas = "";

    public VistaPlanilla(ClienteApi api, long oferta, bool cargarAlAbrir = true) : base(api)
    {
        InitializeComponent();
        // En pantallas bajas la tabla de notas manda: los indicadores pasan a un renglón y la explicación a un globo.
        SizeChanged += (_, _) =>
        {
            var baja = ActualHeight < 600;
            Indicadores.Visibility = baja ? Visibility.Collapsed : Visibility.Visible;
            ResumenCompacto.Visibility = baja ? Visibility.Visible : Visibility.Collapsed;
            Explicacion.Visibility = baja ? Visibility.Collapsed : Visibility.Visible;
            Titulo.FontSize = baja ? 20 : 25;
        };
        ofertaId = oferta;
        docente = api.Persona!.Rol == Roles.Docente;
        // El docente edita notas y evaluaciones; administración revisa la matrícula.
        if (!docente) BotonCargar.Visibility = BotonPublicar.Visibility = AccionesEvaluaciones.Visibility = Visibility.Collapsed;
        else BotonConfirmar.Visibility = BotonRechazar.Visibility = BotonRetirar.Visibility = Visibility.Collapsed;

        Registrar(PanelNotas, filtrar: FiltrarNotas);
        Registrar(PanelEvaluaciones);
        Registrar(PanelMatricula);
        if (docente)
        {
            Atajo(Key.N, ModifierKeys.Control, CargarNotas);
            Atajo(Key.U, ModifierKeys.Control, () => Evaluacion(false));
        }
        // La tabla solo muestra. Las notas se cargan en una ventana aparte: doble clic o Enter sobre un estudiante corrige las suyas.
        Notas.IsReadOnly = true;
        Notas.MouseDoubleClick += async (_, e) =>
        {
            if (e.OriginalSource is DependencyObject origen && ItemsControl.ContainerFromElement(Notas, origen) is DataGridRow { Item: DataRowView fila })
                await Ejecutar(() => CorregirEstudiante(fila));
        };
        Notas.PreviewKeyDown += async (_, e) =>
        {
            if (e.Key != Key.Enter || Notas.SelectedItem is not DataRowView fila) return;
            e.Handled = true;
            await Ejecutar(() => CorregirEstudiante(fila));
        };
        Atajo(Key.F5, ModifierKeys.None, Recargar, "Planilla actualizada.");
        InputBindings.Add(new KeyBinding(new Orden(() => Propietario?.Close()), Key.W, ModifierKeys.Control));
        HabilitarBusqueda();
        FocoInicial = Notas;
        if (cargarAlAbrir) Loaded += async (_, _) => { await Ejecutar(Cargar); EnfocarNotas(); };
    }

    private async void CargarNotas_Click(object sender, RoutedEventArgs e) => await Ejecutar(CargarNotas);
    private async void Publicar_Click(object sender, RoutedEventArgs e) => await Ejecutar(Publicar);
    private async void Recargar_Click(object sender, RoutedEventArgs e) => await Ejecutar(Recargar, "Planilla actualizada.");
    private async void AgregarEvaluacion_Click(object sender, RoutedEventArgs e) => await Ejecutar(() => Evaluacion(false));
    private async void CorregirEvaluacion_Click(object sender, RoutedEventArgs e) => await Ejecutar(() => Evaluacion(true));
    private async void EliminarEvaluacion_Click(object sender, RoutedEventArgs e) => await Ejecutar(EliminarEvaluacion);
    private async void Importar_Click(object sender, RoutedEventArgs e) => await Ejecutar(Importar);
    private async void Plantilla_Click(object sender, RoutedEventArgs e) => await Ejecutar(DescargarPlantilla);
    private async void Confirmar_Click(object sender, RoutedEventArgs e) => await Ejecutar(() => RevisarMatricula(false));
    private async void Rechazar_Click(object sender, RoutedEventArgs e) => await Ejecutar(() => RevisarMatricula(true));
    private async void Codigos_Click(object sender, RoutedEventArgs e) => await Ejecutar(Codigos);
    private async void Retirar_Click(object sender, RoutedEventArgs e) => await Ejecutar(Retirar);

    private Border Etiqueta(string texto, Tono tono = Tono.Neutro)
    {
        var (fondo, color) = Textos.Pinceles(tono);
        var etiqueta = new Border { Background = fondo, Margin = new Thickness(0, 0, 8, 6),
            Child = new TextBlock { Text = texto, Foreground = color, FontSize = 12.5, FontWeight = FontWeights.SemiBold } };
        etiqueta.SetResourceReference(StyleProperty, "Insignia");
        return etiqueta;
    }

    public bool PuedeCerrar()
    {
        if (Ocupada) { Dialogo.Informar(Propietario, "Operación en curso", "Espera a que termine la operación."); return false; }
        Notas.CommitEdit(DataGridEditingUnit.Cell, true);
        Notas.CommitEdit(DataGridEditingUnit.Row, true);
        return !cambios || Dialogo.Preguntar(Propietario, "Cambios sin guardar",
            "Hay notas sin guardar. Si sales ahora se descartarán.", "Salir sin guardar", true);
    }

    private async Task Cargar() => Presentar(await Api.Obtener<Planilla>($"ofertas/{ofertaId}"));

    internal void Presentar(Planilla recibida)
    {
        planilla = recibida;
        var oferta = planilla.Oferta;
        Titulo.Text = oferta.Descripcion;
        Propietario.Title = oferta.Descripcion;
        Etiquetas.Children.Clear();
        Etiquetas.Children.Add(Etiqueta(oferta.Carrera));
        Etiquetas.Children.Add(Etiqueta($"Docente: {oferta.Docente}"));
        Etiquetas.Children.Add(Etiqueta($"Período {oferta.Periodo}", oferta.Cerrado ? Tono.Neutro : Tono.Marca));
        var reglas = planilla.Reglas;
        Etiquetas.Children.Add(Etiqueta($"Notas de {reglas.EscalaMinima:0.##} a {reglas.EscalaMaxima:0.##}, aprueba con {reglas.NotaAprobatoria:0.##}"));
        TextoReglas.Text = "Los puntos conservan sus decimales. " + (reglas.Redondeo switch
        {
            Redondeos.Entero => "La definitiva se redondea una sola vez al entero: desde 0,50 sube.",
            Redondeos.Ninguno => "La definitiva no se redondea: queda con dos decimales.",
            _ => "La definitiva se redondea una sola vez: menos de 0,50 baja, 0,50 exacto se queda y más de 0,50 sube."
        }) + $" Se aprueba con {reglas.NotaAprobatoria:0.##}. Una nota pendiente no cuenta como cero.";
        ResumenCompacto.ToolTip = TextoReglas.Text;
        var peso = planilla.Evaluaciones.Sum(e => e.Peso);
        Confirmados.Text = oferta.Matriculados.ToString(CultureInfo.CurrentCulture);
        PorRevisar.Text = oferta.Pendientes.ToString(CultureInfo.CurrentCulture);
        PorRevisar.Foreground = (Brush)FindResource(oferta.Pendientes > 0 ? "Aviso" : "Texto");
        Plan.Text = $"{peso:0.##} % de 100 %";
        AvancePlan.Value = (double)Math.Min(peso, 100m);
        AvancePlan.Foreground = (Brush)FindResource(peso == 100m ? "Exito" : "Indigo500");
        EstadoPlanilla.Text = Textos.Capitalizar(oferta.Estado);
        EstadoPlanilla.Foreground = Textos.Pinceles(Textos.Clasificar(oferta.Estado, "Estado")).Texto;
        ResumenCompacto.Text = $"{oferta.Matriculados} {(oferta.Matriculados == 1 ? "estudiante confirmado" : "estudiantes confirmados")}, {oferta.Pendientes} por revisar, " +
            $"plan al {peso:0.##} %, {oferta.Estado}";
        Evaluaciones.ItemsSource = planilla.Evaluaciones;
        Alumnos.ItemsSource = planilla.Estudiantes;
        CrearTabla();
        // En un período cerrado las acciones de edición se ocultan en lugar de verse deshabilitadas.
        if (docente)
        {
            var edicion = oferta.Cerrado ? Visibility.Collapsed : Visibility.Visible;
            BotonCargar.Visibility = BotonPublicar.Visibility = AccionesEvaluaciones.Visibility = edicion;
        }
        if (oferta.Cerrado) Etiquetas.Children.Add(Etiqueta("Solo lectura: período cerrado", Tono.Aviso));
        cambios = false;
        celdasModificadas.Clear();
        LimpiarEstado();
    }

    private void CrearTabla()
    {
        datos = new DataTable();
        datos.Columns.Add("estudiante_id", typeof(long));
        datos.Columns.Add("cedula", typeof(string));
        datos.Columns.Add("nombre", typeof(string));
        foreach (var evaluacion in planilla!.Evaluaciones) datos.Columns.Add($"e{evaluacion.Id}", typeof(string));
        datos.Columns.Add("acumulado", typeof(string));
        datos.Columns.Add("final", typeof(string));
        datos.Columns.Add("condicion", typeof(string));
        var existentes = planilla.Notas.ToDictionary(n => (n.EstudianteId, n.EvaluacionId), n => n.Valor);
        foreach (var alumno in planilla.Estudiantes.Where(a => a.Estado == "confirmada"))
        {
            var fila = datos.NewRow();
            fila["estudiante_id"] = alumno.EstudianteId;
            fila["cedula"] = Cedulas.Mostrar(alumno.Cedula);
            fila["nombre"] = alumno.NombreCompleto;
            foreach (var evaluacion in planilla.Evaluaciones)
                fila[$"e{evaluacion.Id}"] = existentes.GetValueOrDefault((alumno.EstudianteId, evaluacion.Id))?.ToString("0.##", CultureInfo.CurrentCulture) ?? "";
            datos.Rows.Add(fila);
            CalcularFila(fila);
        }
        Notas.Columns.Clear();
        Columna("Cédula", "cedula", new DataGridLength(120), true, "TextoCeldaTenue");
        Columna("Estudiante", "nombre", new DataGridLength(1, DataGridLengthUnitType.Star), true, "TextoCelda");
        foreach (var evaluacion in planilla.Evaluaciones)
            Columna($"{evaluacion.Nombre}\n{evaluacion.Peso:0.##} %", $"e{evaluacion.Id}", new DataGridLength(132), false, "TextoCeldaCentro");
        Columna($"Acumulado /{planilla.Reglas.EscalaMaxima:0.##}", "acumulado", new DataGridLength(130), true, "TextoCeldaCentro");
        Columna("Definitiva", "final", new DataGridLength(112), true, "TextoCeldaCentro").FontWeight = FontWeights.SemiBold;
        Notas.Columns.Add(new DataGridTemplateColumn { Header = "Condición", Width = new DataGridLength(130), MinWidth = 130, CellTemplate = PlantillaCondicion(), SortMemberPath = "[condicion]" });
        datos.ColumnChanged += (_, e) =>
        {
            if (calculando || e.Column is null || !e.Column.ColumnName.StartsWith('e') || e.Column.ColumnName == "estudiante_id") return;
            cambios = true;
            celdasModificadas.Add(((long)e.Row["estudiante_id"], long.Parse(e.Column.ColumnName[1..], CultureInfo.InvariantCulture)));
            CalcularFila(e.Row);
            MarcarEstado("Hay cambios sin guardar. Guarda el borrador para no perderlos.");
        };
        Notas.ItemsSource = datos.DefaultView;
        FiltrarNotas(filtroNotas);
    }

    // Filtro por estudiante o cédula; con listas grandes es la forma rápida de llegar a una fila.
    private void FiltrarNotas(string texto)
    {
        filtroNotas = texto.Trim();
        var seguro = filtroNotas.Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("*", "[*]");
        datos.DefaultView.RowFilter = seguro.Length == 0 ? "" : $"nombre LIKE '%{seguro}%' OR cedula LIKE '%{seguro}%'";
    }

    private void EnfocarNotas()
    {
        if (Notas.Items.Count == 0 || Notas.Columns.Count < 3) { Notas.Focus(); return; }
        Notas.SelectedIndex = 0;
        Notas.CurrentCell = new DataGridCellInfo(Notas.Items[0], Notas.Columns[0]);
        Notas.Focus();
    }

    private static DataTemplate PlantillaCondicion()
    {
        var borde = new FrameworkElementFactory(typeof(Border));
        borde.SetResourceReference(StyleProperty, "Insignia");
        borde.SetBinding(Border.BackgroundProperty, new Binding("[condicion]") { Converter = new ConvertidorInsignia { Modo = "fondo" } });
        var texto = new FrameworkElementFactory(typeof(TextBlock));
        texto.SetBinding(TextBlock.TextProperty, new Binding("[condicion]") { Converter = new ConvertidorInsignia { Modo = "etiqueta" } });
        texto.SetBinding(TextBlock.ForegroundProperty, new Binding("[condicion]") { Converter = new ConvertidorInsignia { Modo = "texto" } });
        texto.SetValue(TextBlock.FontSizeProperty, 12.5);
        texto.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        borde.AppendChild(texto);
        return new DataTemplate { VisualTree = borde };
    }

    private DataGridTextColumn Columna(string tituloColumna, string propiedad, DataGridLength ancho, bool soloLectura, string estilo)
    {
        var celda = new Style(typeof(TextBlock), (Style)FindResource(estilo));
        celda.Triggers.Add(new Trigger { Property = TextBlock.TextProperty, Value = Revisar, Setters = { new Setter(TextBlock.ForegroundProperty, FindResource("Error")) } });
        celda.Triggers.Add(new Trigger { Property = TextBlock.TextProperty, Value = Pendiente, Setters = { new Setter(TextBlock.ForegroundProperty, FindResource("Texto3")) } });
        var columna = new DataGridTextColumn
        {
            Header = tituloColumna,
            Binding = new Binding($"[{propiedad}]") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
            Width = ancho,
            IsReadOnly = soloLectura,
            ElementStyle = celda,
            EditingElementStyle = (Style)FindResource("EdicionCelda")
        };
        // Sin mínimo, una columna proporcional hace que las fijas se encojan hasta volverse ilegibles.
        columna.MinWidth = ancho.IsAbsolute ? ancho.Value : 220;
        if (estilo == "TextoCeldaCentro") columna.HeaderStyle = (Style)FindResource("CabeceraCentrada");
        Notas.Columns.Add(columna);
        return columna;
    }

    private void CalcularFila(DataRow fila)
    {
        calculando = true;
        try
        {
            var total = 0m;
            var completas = planilla!.Evaluaciones.Count > 0 && planilla.Evaluaciones.Sum(e => e.Peso) == 100m;
            foreach (var evaluacion in planilla.Evaluaciones)
            {
                var texto = fila[$"e{evaluacion.Id}"].ToString() ?? "";
                if (string.IsNullOrWhiteSpace(texto)) { completas = false; continue; }
                var nota = ReglasAcademicas.LeerDecimal(texto);
                Calificacion.ValidarNota(nota, planilla.Reglas);
                total += Calificacion.Aporte(nota, evaluacion.Peso);
            }
            fila["acumulado"] = total.ToString("0.####");
            var final = completas ? Calificacion.Redondear(total, planilla.Reglas.Redondeo) : (decimal?)null;
            fila["final"] = final is { } valorFinal ? Textos.Nota(valorFinal) : Pendiente;
            fila["condicion"] = final is { } definitiva ? Calificacion.Condicion(definitiva, planilla.Reglas.NotaAprobatoria) : "pendiente";
        }
        catch (ReglaException) { fila["acumulado"] = Revisar; fila["final"] = Pendiente; fila["condicion"] = "pendiente"; }
        finally { calculando = false; }
    }

    private void ExigirGuardado()
    {
        Notas.CommitEdit(DataGridEditingUnit.Cell, true);
        Notas.CommitEdit(DataGridEditingUnit.Row, true);
        if (cambios) throw new ArgumentException("Guarda el borrador antes de realizar esta operación.");
    }

    private async Task Guardar()
    {
        Notas.CommitEdit(DataGridEditingUnit.Cell, true);
        Notas.CommitEdit(DataGridEditingUnit.Row, true);
        if (planilla is null || planilla.Oferta.Cerrado) throw new ArgumentException("La planilla no está disponible para edición.");
        if (!cambios) { Notificar("No hay cambios pendientes.", Tono.Neutro); return; }
        var valores = new List<Nota>();
        foreach (DataRow fila in datos.Rows)
        foreach (var evaluacion in planilla.Evaluaciones)
        {
            if (!celdasModificadas.Contains(((long)fila["estudiante_id"], evaluacion.Id))) continue;
            var texto = fila[$"e{evaluacion.Id}"].ToString() ?? "";
            decimal? valor = string.IsNullOrWhiteSpace(texto) ? null : ReglasAcademicas.LeerDecimal(texto);
            Calificacion.ValidarNota(valor, planilla.Reglas);
            valores.Add(new Nota(evaluacion.Id, (long)fila["estudiante_id"], valor));
        }
        var revision = await Api.Enviar<int>(HttpMethod.Put, $"ofertas/{ofertaId}/notas", new GuardarNotas(planilla.Oferta.Revision, valores));
        planilla = planilla with { Oferta = planilla.Oferta with { Revision = revision } };
        cambios = false;
        await Cargar();
        Notificar($"Borrador guardado a las {DateTime.Now:HH:mm}. Los estudiantes aún no lo ven.", Tono.Exito);
    }

    // Carga de notas fuera de la tabla: de una evaluación para todo el grupo, o de todas las evaluaciones de un estudiante.

    private bool PuedeCargar()
    {
        if (!docente || planilla is null) return false;
        if (planilla.Oferta.Cerrado) throw new ArgumentException("El período está cerrado: las notas ya no se pueden cambiar.");
        if (planilla.Evaluaciones.Count == 0) throw new ArgumentException("Primero agrega las evaluaciones en la pestaña Evaluaciones.");
        if (datos.Rows.Count == 0) throw new ArgumentException("Todavía no hay estudiantes confirmados en esta materia.");
        return true;
    }

    private string? ValidarNota(string texto)
    {
        if (texto.Length == 0) return null;
        try { Calificacion.ValidarNota(ReglasAcademicas.LeerDecimal(texto), planilla!.Reglas); return null; }
        catch (ReglaException error) { return error.Message; }
    }

    private string Valor(DataRow fila, long evaluacion) => fila[$"e{evaluacion}"].ToString() ?? "";

    private async Task CargarNotas()
    {
        if (!PuedeCargar()) return;
        var evaluaciones = planilla!.Evaluaciones;
        var elegida = evaluaciones[0];
        if (evaluaciones.Count > 1)
        {
            // Primero la que tiene más notas pendientes.
            var opciones = evaluaciones.Select(e => new Referencia(e.Id, $"{e.Nombre} ({e.Peso:0.##} %)")).ToList();
            var sugerida = evaluaciones.OrderByDescending(e => datos.Rows.Cast<DataRow>().Count(f => Valor(f, e.Id).Length == 0)).ThenBy(e => e.Orden).First();
            var formulario = new Formulario(Propietario, "Cargar notas", "Elige la evaluación. Después escribes la nota de cada estudiante.");
            var lista = formulario.Seleccion("Evaluación", opciones, "Nombre", opciones.First(o => o.Id == sugerida.Id));
            if (formulario.ShowDialog() != true) throw new OperacionCancelada();
            elegida = evaluaciones.First(e => e.Id == Formulario.Elegido<Referencia>(lista).Id);
        }
        var filas = datos.Rows.Cast<DataRow>().OrderBy(f => f["nombre"].ToString()).ToList();
        var ventana = new VentanaNotas(Propietario, $"{elegida.Nombre} · {elegida.Peso:0.##} %",
            $"Escribe la nota de cada estudiante, de {planilla.Reglas.EscalaMinima:0.##} a {planilla.Reglas.EscalaMaxima:0.##}. Enter pasa al siguiente; vacío queda pendiente.",
            filas.Select(f => new CampoNota((long)f["estudiante_id"], f["nombre"].ToString()!, f["cedula"].ToString()!, Valor(f, elegida.Id))).ToList(), ValidarNota);
        if (ventana.ShowDialog() != true) throw new OperacionCancelada();
        await AplicarNotas(ventana.Cambios.Select(c => ((c.Key, elegida.Id), c.Value)));
    }

    private async Task CorregirEstudiante(DataRowView fila)
    {
        if (!PuedeCargar()) return;
        var estudiante = (long)fila["estudiante_id"];
        var ventana = new VentanaNotas(Propietario, fila["nombre"].ToString()!,
            $"{fila["cedula"]}. Corrige sus notas, de {planilla!.Reglas.EscalaMinima:0.##} a {planilla.Reglas.EscalaMaxima:0.##}. Vacío queda pendiente.",
            planilla.Evaluaciones.Select(e => new CampoNota(e.Id, e.Nombre, $"{e.Peso:0.##} %", Valor(fila.Row, e.Id))).ToList(), ValidarNota);
        if (ventana.ShowDialog() != true) throw new OperacionCancelada();
        await AplicarNotas(ventana.Cambios.Select(c => ((estudiante, c.Key), c.Value)));
    }

    /// <summary>Pasa a la tabla las notas cargadas y las guarda como borrador de una vez.</summary>
    internal async Task AplicarNotas(IEnumerable<((long Estudiante, long Evaluacion) Clave, string Valor)> notas)
    {
        var lista = notas.ToList();
        if (lista.Count == 0) { Notificar("No cambiaste ninguna nota.", Tono.Neutro); return; }
        var porEstudiante = datos.Rows.Cast<DataRow>().ToDictionary(f => (long)f["estudiante_id"]);
        foreach (var ((estudiante, evaluacion), valor) in lista) porEstudiante[estudiante][$"e{evaluacion}"] = valor;
        await Guardar();
    }

    private async Task Publicar()
    {
        ExigirGuardado();
        Confirmar("Los estudiantes verán esta versión de las notas, incluidas las evaluaciones pendientes. El período seguirá abierto.",
            Propietario, "Publicar avance", "Publicar");
        await Api.Enviar<long>(HttpMethod.Post, $"ofertas/{ofertaId}/publicar", new RevisionEsperada(planilla!.Oferta.Revision));
        await Cargar();
        Notificar("Avance publicado. Los estudiantes ya pueden consultarlo.", Tono.Exito);
    }

    private async Task Recargar()
    {
        Notas.CommitEdit(DataGridEditingUnit.Cell, true);
        Notas.CommitEdit(DataGridEditingUnit.Row, true);
        if (cambios) Confirmar("Se descartarán los cambios sin guardar y se cargará la última versión.", Propietario, "Recargar planilla", "Descartar y recargar", true);
        await Cargar();
    }

    private async Task Evaluacion(bool editar)
    {
        ExigirGuardado();
        var elegida = editar ? Evaluaciones.SelectedItem as Evaluacion : null;
        if (editar && elegida is null) throw new ArgumentException("Selecciona una evaluación.");
        var formulario = new Formulario(Propietario, editar ? "Corregir evaluación" : "Agregar evaluación",
            "Entre todas las evaluaciones la ponderación debe sumar 100 %. Los estudiantes no ven los cambios hasta que publiques.");
        var nombre = formulario.Texto("Nombre", elegida?.Nombre ?? "");
        var peso = formulario.Texto("Ponderación (%)", elegida?.Peso.ToString("0.##") ?? "", "Por ejemplo: 15 o 12,5.");
        if (formulario.ShowDialog() != true) throw new OperacionCancelada();
        var nueva = new NuevaEvaluacion(nombre.Text, ReglasAcademicas.LeerDecimal(peso.Text), planilla!.Oferta.Revision);
        if (editar) await Api.Enviar<int>(HttpMethod.Put, $"ofertas/{ofertaId}/evaluaciones/{elegida!.Id}", nueva);
        else await Api.Enviar<long>(HttpMethod.Post, $"ofertas/{ofertaId}/evaluaciones", nueva);
        await Cargar();
        Notificar(editar ? "Evaluación corregida." : "Evaluación agregada.", Tono.Exito);
    }

    private async Task EliminarEvaluacion()
    {
        ExigirGuardado();
        if (Evaluaciones.SelectedItem is not Evaluacion evaluacion) throw new ArgumentException("Selecciona una evaluación.");
        Confirmar($"¿Eliminar «{evaluacion.Nombre}»? Solo se permite si nunca tuvo notas ni publicaciones.", Propietario, "Eliminar evaluación", "Eliminar", true);
        await Api.Enviar<int>(HttpMethod.Post, $"ofertas/{ofertaId}/evaluaciones/{evaluacion.Id}/eliminar", new RevisionEsperada(planilla!.Oferta.Revision));
        await Cargar();
        Notificar("Evaluación eliminada.", Tono.Exito);
    }

    private async Task DescargarPlantilla()
    {
        var guardar = new SaveFileDialog { FileName = "matricula.xlsx", Filter = "Libro de Excel|*.xlsx" };
        if (guardar.ShowDialog(Propietario) != true) throw new OperacionCancelada();
        await File.WriteAllBytesAsync(guardar.FileName, await Api.Descargar("plantilla-matricula"));
        Notificar($"Plantilla guardada en {guardar.FileName}.", Tono.Exito);
    }

    private async Task Importar()
    {
        ExigirGuardado();
        if (planilla!.Oferta.Cerrado) throw new ArgumentException("El período está cerrado.");
        var abrir = new OpenFileDialog { Filter = "Libro de Excel|*.xlsx", Multiselect = false };
        if (abrir.ShowDialog(Propietario) != true) throw new OperacionCancelada();
        ServicioMatricula.ValidarTamano(new FileInfo(abrir.FileName).Length);
        var vista = await Api.LeerExcel(ofertaId, await File.ReadAllBytesAsync(abrir.FileName));
        if (vista.Errores.Count > 0) { new VentanaErroresExcel(Propietario, vista).ShowDialog(); throw new OperacionCancelada(); }
        var aviso = docente ? "El listado quedará pendiente de confirmación administrativa." : "Verifica la matrícula institucional antes de confirmar.";
        if (new VentanaImportacion(Propietario, vista, $"{planilla.Oferta.Descripcion}. {aviso}").ShowDialog() != true) throw new OperacionCancelada();
        var resultado = await Api.Enviar<ResultadoImportacion>(HttpMethod.Post, $"ofertas/{ofertaId}/importar", new ImportarMatricula(planilla.Oferta.Revision, vista.Filas));
        await Cargar();
        var omitidas = resultado.Omitidas ?? [];
        Notificar($"Matrícula importada: {resultado.Nuevos} inscripciones nuevas ({resultado.Estado}); {resultado.Existentes} ya existían." +
            (omitidas.Count == 0 ? "" : $" {omitidas.Count} no se inscribieron: " +
                string.Join(" ", omitidas.Take(3).Select(o => $"{o.Estudiante}, {char.ToLowerInvariant(o.Motivo[0])}{o.Motivo[1..]}")) + (omitidas.Count > 3 ? " …" : "")),
            omitidas.Count == 0 ? Tono.Exito : Tono.Aviso);
    }

    private async Task Retirar()
    {
        ExigirGuardado();
        if (planilla!.Oferta.Cerrado) throw new ArgumentException("El período está cerrado.");
        var seleccion = Alumnos.SelectedItems.Cast<Alumno>().ToList();
        if (seleccion.Count == 0 || seleccion.Any(a => a.Estado != "confirmada"))
            throw new ArgumentException("Selecciona estudiantes confirmados. Las inscripciones pendientes se rechazan.");
        var quienes = seleccion.Count == 1 ? seleccion[0].NombreCompleto : $"los {seleccion.Count} estudiantes seleccionados";
        Confirmar($"¿Retirar a {quienes} de {planilla.Oferta.Materia}? Dejarán de ver la materia y saldrán de la planilla. " +
            "Sus notas no se borran: quedan en la historia, y pueden inscribirse en otra sección.", Propietario, "Retirar de la materia", "Retirar", true);
        await Api.Enviar<int>(HttpMethod.Post, $"ofertas/{ofertaId}/retirar-estudiantes",
            new ConfirmarMatricula(planilla.Oferta.Revision, seleccion.Select(a => a.InscripcionId).ToList()));
        await Cargar();
        Notificar(seleccion.Count == 1 ? $"{seleccion[0].NombreCompleto} ya no está en esta materia." : $"{seleccion.Count} estudiantes retirados de la materia.", Tono.Exito);
    }

    // Códigos para entregar en clase a quienes todavía no activaron su cuenta.
    private async Task Codigos()
    {
        if (planilla is null) throw new ArgumentException("Primero carga la planilla.");
        if (planilla.Oferta.Matriculados == 0) throw new ArgumentException("Primero confirma la matrícula: los códigos son para los estudiantes confirmados.");
        var formulario = new Formulario(Propietario, "Códigos de acceso",
            "Se genera una hoja con una tira por estudiante que todavía no activó su cuenta. En clase, verifica su cédula o su comprobante " +
            "de inscripción y entrégale su tira. Si vuelves a generarla, los códigos anteriores dejan de servir.");
        var dias = formulario.Texto("Los códigos duran (días)", "7", $"De 1 a {Logica.ServicioIdentidad.MaximoDiasCodigoSeccion}. Pasado ese plazo hay que generar otra hoja.");
        if (formulario.ShowDialog() != true) throw new OperacionCancelada();
        if (!int.TryParse(dias.Text.Trim(), out var vigencia)) throw new ArgumentException("Escribe los días como un número, por ejemplo 7.");
        // Primero dónde guardarla: emitir reemplaza los códigos anteriores, así que no se emite sin destino.
        var guardar = new SaveFileDialog { FileName = $"codigos-{planilla.Oferta.Seccion}.pdf", Filter = "Documento PDF|*.pdf" };
        if (guardar.ShowDialog(Propietario) != true) throw new OperacionCancelada();
        var hoja = await Api.Enviar<CodigosDeSeccion>(HttpMethod.Post, $"ofertas/{ofertaId}/codigos", new CodigosSeccion(vigencia));
        if (hoja.Codigos.Count == 0) { Notificar("Todos los estudiantes confirmados ya activaron su cuenta: no hace falta ningún código.", Tono.Neutro); return; }
        ExportadorCodigos.Pdf(hoja, guardar.FileName);
        Notificar($"{hoja.Codigos.Count} {(hoja.Codigos.Count == 1 ? "código" : "códigos")} en {guardar.FileName}" +
            (hoja.ConAcceso > 0 ? $". {hoja.ConAcceso} ya tenían su cuenta activa." : "."), Tono.Exito);
    }

    private async Task RevisarMatricula(bool rechazar)
    {
        ExigirGuardado();
        var seleccion = Alumnos.SelectedItems.Cast<Alumno>().ToList();
        if (seleccion.Count == 0 || seleccion.Any(a => a.Estado != "pendiente")) throw new ArgumentException("Selecciona únicamente estudiantes con matrícula pendiente.");
        var accion = rechazar ? "Rechazar" : "Confirmar";
        Confirmar($"¿{accion} las {seleccion.Count} inscripciones seleccionadas?", Propietario, $"{accion} matrícula", accion, rechazar);
        await Api.Enviar<int>(HttpMethod.Post, $"ofertas/{ofertaId}/{(rechazar ? "rechazar" : "confirmar")}",
            new ConfirmarMatricula(planilla!.Oferta.Revision, seleccion.Select(a => a.InscripcionId).ToList()));
        await Cargar();
        Notificar(rechazar ? "Inscripciones rechazadas." : "Inscripciones confirmadas.", Tono.Exito);
    }
}
