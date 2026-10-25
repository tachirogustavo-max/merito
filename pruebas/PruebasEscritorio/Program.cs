using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Entidades;
using Escritorio;
using Escritorio.Presentacion;
using Escritorio.Servicios;
using Logica;

internal static class Program
{
    private static int comprobaciones;
    private static string salida = "";

    [STAThread]
    private static void Main()
    {
        if (Environment.GetEnvironmentVariable("ACADEMICO_PRUEBAS") != "1")
            throw new InvalidOperationException("Solo se ejecuta con ACADEMICO_PRUEBAS=1 contra el servidor local de pruebas.");
        salida = Path.GetFullPath(Environment.GetEnvironmentVariable("ACADEMICO_EVIDENCIA") ?? ".evidencia");
        Directory.CreateDirectory(salida);
        var aplicacion = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown, MostrarVentanaInicial = false };
        aplicacion.InitializeComponent();
        aplicacion.Startup += async (_, _) =>
        {
            try { await Probar(); aplicacion.Shutdown(0); }
            catch (Exception error) { Console.Error.WriteLine(error); aplicacion.Shutdown(1); }
        };
        aplicacion.Run();
    }

    private static void Comprobar(bool condicion, string nombre)
    {
        if (!condicion) throw new InvalidOperationException(nombre);
        comprobaciones++;
        Console.WriteLine("correcto: " + nombre);
    }

    private static async Task Probar()
    {
        var direccion = Environment.GetEnvironmentVariable("ACADEMICO_URL_SERVIDOR") ?? "http://127.0.0.1:5187";
        if (!new Uri(direccion).IsLoopback) throw new InvalidOperationException("Solo se admiten pruebas locales.");
        using var apiAdmin = new ClienteApi();
        apiAdmin.Configurar(direccion);
        var ventanaLogin = new MainWindow(apiAdmin);
        ventanaLogin.Show();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Capturar(ventanaLogin, "01-acceso.png");
        var formulario = new Formulario(ventanaLogin, "Crear cuenta", "La persona establecerá su contraseña con un código entregado por administración.");
        formulario.Texto("Cédula", "12345678");
        formulario.Texto("Nombres");
        formulario.Seleccion("Rol", new[] { new Referencia(1, "Docente"), new Referencia(2, "Estudiante") }, "Nombre");
        formulario.Show();
        await EsperarVista(formulario);
        Capturar(formulario, "06-formulario.png");
        formulario.Close();
        var dialogo = new Dialogo(ventanaLogin, "Cerrar período", "¿Cerrar el período 2-2026? Las calificaciones quedarán bloqueadas para edición.", Tono.Peligro, "Cerrar período", "Cancelar");
        dialogo.Show();
        await EsperarVista(dialogo);
        Capturar(dialogo, "07-confirmacion.png");
        dialogo.Close();
        ventanaLogin.Notificar("La cuenta quedó creada. Emite un código para que active su acceso.", Tono.Exito, "Cuenta creada");
        ventanaLogin.Notificar("Selecciona una cuenta.", Tono.Aviso, "Revisa los datos");
        await Task.Delay(500);
        Capturar(ventanaLogin, "08-notificaciones.png");
        var fallo = new Dialogo(ventanaLogin, "Sin conexión con el servidor", "No hay conexión con el servidor. Comprueba la red; los cambios sin guardar se conservan en pantalla.", Tono.Peligro, "Entendido", null, "4f1c2a9be0d3");
        fallo.Show();
        await EsperarVista(fallo);
        Capturar(fallo, "09-error.png");
        fallo.Close();
        ventanaLogin.Hide();
        await apiAdmin.Iniciar(direccion, Environment.GetEnvironmentVariable("ACADEMICO_ADMIN_CEDULA")!, Environment.GetEnvironmentVariable("ACADEMICO_ADMIN_CLAVE")!);
        var sufijo = Guid.NewGuid().ToString("N")[..6];
        var cedula = Random.Shared.Next(10_000_000, 89_000_000);
        var clave = Seguridad.NuevoToken();
        var docente = await apiAdmin.Enviar<long>(HttpMethod.Post, "usuarios", new NuevaPersona(cedula.ToString(), "Ana", "Demostración", Roles.Docente));
        var estudiante = await apiAdmin.Enviar<long>(HttpMethod.Post, "usuarios", new NuevaPersona((cedula + 1).ToString(), "Elena", "Demostración", Roles.Estudiante));
        foreach (var (id, identificacion) in new[] { (docente, cedula), (estudiante, cedula + 1) })
        {
            var codigo = await apiAdmin.Enviar<CodigoAcceso>(HttpMethod.Post, $"usuarios/{id}/codigo", new { });
            await apiAdmin.Enviar(HttpMethod.Post, "activar", new ActivarCuenta(identificacion.ToString(), codigo.Codigo, clave));
        }
        var periodo = await apiAdmin.Enviar<long>(HttpMethod.Post, "catalogos/periodos", new Referencia(0, "2-2026 muestra " + sufijo));
        var carreraNombre = "Informática demostración " + sufijo;
        var carrera = await apiAdmin.Enviar<long>(HttpMethod.Post, "catalogos/carreras", new Referencia(0, carreraNombre));
        async Task<long> MateriaEn(string codigo, string nombre, int semestre, int uc) => await apiAdmin.Enviar<long>(HttpMethod.Post,
            $"materias?carrera={carrera}&semestre={semestre}", new NuevaMateria($"{codigo}-{sufijo}", $"{nombre} {sufijo}", uc));
        var materia = await MateriaEn("LOG", "Lógica de programación", 1, 3);
        var seccion = await apiAdmin.Enviar<long>(HttpMethod.Post, "secciones", new NuevaSeccion(carrera, "A"));
        var oferta = await apiAdmin.Enviar<long>(HttpMethod.Post, "ofertas", new NuevaOferta(periodo, seccion, materia, docente));
        var plan = await apiAdmin.Obtener<Planilla>($"ofertas/{oferta}");
        await apiAdmin.Enviar<ResultadoImportacion>(HttpMethod.Post, $"ofertas/{oferta}/importar", new ImportarMatricula(plan.Oferta.Revision,
            [new FilaMatricula((cedula + 1).ToString(), "Elena", "Demostración", carreraNombre, "A")]));
        using var apiDocente = new ClienteApi();
        await apiDocente.Iniciar(direccion, cedula.ToString(), clave);
        var evaluaciones = new List<long>();
        foreach (var (nombre, peso) in new[] { ("Primer parcial", 15m), ("Trabajo práctico", 35m), ("Evaluación final", 50m) })
        {
            plan = await apiDocente.Obtener<Planilla>($"ofertas/{oferta}");
            evaluaciones.Add(await apiDocente.Enviar<long>(HttpMethod.Post, $"ofertas/{oferta}/evaluaciones", new NuevaEvaluacion(nombre, peso, plan.Oferta.Revision)));
        }
        plan = await apiDocente.Obtener<Planilla>($"ofertas/{oferta}");
        await apiDocente.Enviar<int>(HttpMethod.Put, $"ofertas/{oferta}/notas", new GuardarNotas(plan.Oferta.Revision,
            [new Nota(evaluaciones[0], estudiante, 10m), new Nota(evaluaciones[1], estudiante, 17m)]));
        plan = await apiDocente.Obtener<Planilla>($"ofertas/{oferta}");
        await apiDocente.Enviar<long>(HttpMethod.Post, $"ofertas/{oferta}/publicar", new RevisionEsperada(plan.Oferta.Revision));
        var codigoCarrera = "C" + sufijo.ToUpperInvariant();
        await apiAdmin.Enviar<int>(HttpMethod.Put, $"carreras/{carrera}", new NuevaCarrera(carreraNombre, codigoCarrera));
        await apiAdmin.Enviar<long>(HttpMethod.Post, "secciones", new NuevaSeccion(carrera, "D1", 2));

        ventanaLogin.Show();
        ventanaLogin.MostrarAplicacion();
        await EsperarVista(ventanaLogin);
        Capturar(ventanaLogin, "02-administracion.png");
        Comprobar(Descendientes(ventanaLogin).OfType<TabItem>().Count() >= 4, "administración presenta cuentas, estructura, asignaciones y períodos");

        // Secciones con código y el formato: se mira la vista previa sin guardar nada.
        var pestanaEstructura = Descendientes(ventanaLogin).OfType<TabItem>().First(t => AutomationProperties.GetName(t) == "Estructura");
        pestanaEstructura.IsSelected = true;
        await EsperarVista(ventanaLogin);
        Descendientes(ventanaLogin).OfType<TabItem>().First(t => t.Header as string == "Secciones").IsSelected = true;
        await EsperarVista(ventanaLogin);
        Capturar(ventanaLogin, "19-secciones.png");
        var botonFormato = Descendientes(ventanaLogin).OfType<BotonAccion>().First(b => b.Texto == "Formato del código");
        string? previaFormato = null;
        var formatoGuardado = (await apiAdmin.Obtener<FormatoCodigo>("formato-seccion")).Formato;
        var vigilante = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        vigilante.Tick += async (_, _) =>
        {
            if (Application.Current.Windows.OfType<Formulario>().FirstOrDefault(f => f.IsVisible) is not { } dialogoFormato) return;
            vigilante.Stop();
            Descendientes(dialogoFormato).OfType<TextBox>().First().Text = "{semestre:00}S-{carrera}-{seccion}";
            await EsperarVista(dialogoFormato);
            previaFormato = Descendientes(dialogoFormato).OfType<TextBlock>().Select(b => b.Text).FirstOrDefault(t => t.StartsWith("Así quedaría") || t.StartsWith("Con carrera"));
            Capturar(dialogoFormato, "20-formato-seccion.png");
            dialogoFormato.Close();
        };
        vigilante.Start();
        botonFormato.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        for (var i = 0; i < 100 && previaFormato is null; i++) await Task.Delay(50);
        Comprobar(previaFormato is not null && previaFormato.Contains("S-") && !previaFormato.Contains('{'), "el formato del código muestra la vista previa armada");
        Comprobar((await apiAdmin.Obtener<FormatoCodigo>("formato-seccion")).Formato == formatoGuardado, "cancelar el formato no cambia nada");
        // Pensum de la carrera, reglas de calificación y menú de la cuenta.
        Descendientes(ventanaLogin).OfType<TabItem>().First(t => t.Header as string == "Pensum").IsSelected = true;
        await EsperarVista(ventanaLogin);
        var vistaAdmin = Descendientes(ventanaLogin).OfType<VistaAdministracion>().Single();
        vistaAdmin.CarreraPensum.Seleccionar(((System.Collections.IEnumerable)vistaAdmin.CarreraPensum.ItemsSource!).Cast<Carrera>().First(c => c.Id == carrera));
        await EsperarVista(ventanaLogin);
        Comprobar(vistaAdmin.Materias.Items.Count == 1 && vistaAdmin.ResumenPensum.Text.Contains("3 UC"), "la pestaña Pensum muestra las materias de la carrera elegida con sus UC");
        Capturar(ventanaLogin, "22-pensum.png");
        Descendientes(ventanaLogin).OfType<TabItem>().First(t => AutomationProperties.GetName(t) == "Períodos").IsSelected = true;
        await EsperarVista(ventanaLogin);
        string? previaReglas = null;
        var vigilanteReglas = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        vigilanteReglas.Tick += async (_, _) =>
        {
            if (Application.Current.Windows.OfType<Formulario>().FirstOrDefault(f => f.IsVisible) is not { } dialogoReglas) return;
            vigilanteReglas.Stop();
            Descendientes(dialogoReglas).OfType<TextBox>().ElementAt(2).Text = "12";
            await EsperarVista(dialogoReglas);
            previaReglas = Descendientes(dialogoReglas).OfType<TextBlock>().Select(b => b.Text).FirstOrDefault(t => t.StartsWith("Ejemplo"));
            Capturar(dialogoReglas, "23-reglas-calificacion.png");
            dialogoReglas.Close();
        };
        vigilanteReglas.Start();
        Descendientes(ventanaLogin).OfType<BotonAccion>().First(b => b.Texto == "Reglas de calificación")
            .RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        for (var i = 0; i < 100 && previaReglas is null; i++) await Task.Delay(50);
        Comprobar(previaReglas is not null && previaReglas.Contains("11,5") && previaReglas.Contains("aplazada"), "las reglas muestran un ejemplo con la nota aprobatoria que se escribe");
        ventanaLogin.Activate();
        await Task.Delay(200);
        ventanaLogin.TarjetaCuenta.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        await Task.Delay(400);
        Comprobar(ventanaLogin.MenuCuenta.IsOpen, "pulsar la cuenta abre el menú con contraseña y cierre de sesión");
        if (ventanaLogin.MenuCuenta.Child is FrameworkElement menu)
        {
            menu.UpdateLayout();
            var foto = new RenderTargetBitmap((int)menu.ActualWidth, (int)menu.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            foto.Render(menu);
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(foto));
            using var archivoMenu = File.Create(Path.Combine(salida, "24-menu-cuenta.png"));
            png.Save(archivoMenu);
        }
        ventanaLogin.MenuCuenta.IsOpen = false;
        Descendientes(ventanaLogin).OfType<TabItem>().First(t => AutomationProperties.GetName(t) == "Cuentas").IsSelected = true;
        await EsperarVista(ventanaLogin);

        // Asignar por sección: las materias del pensum de la carrera, cada una con su docente.
        foreach (var (codigoMateria, nombreMateria, semestreMateria, ucMateria) in new[] { ("MAT", "Cálculo I", 1, 4), ("PRO", "Programación I", 1, 3), ("CAL", "Cálculo II", 2, 4), ("ORA", "Oratoria", 0, 2) })
            await MateriaEn(codigoMateria, nombreMateria, semestreMateria, ucMateria);
        var abiertos = (await apiAdmin.Obtener<List<Periodo>>("periodos")).Where(p => !p.Cerrado).ToList();
        var todasSecciones = await apiAdmin.Obtener<List<Seccion>>("secciones");
        var todasMaterias = await apiAdmin.Obtener<List<MateriaPensum>>($"pensum?carrera={carrera}");
        var docentesActivos = await apiAdmin.Obtener<List<Persona>>("docentes");
        var cargaSeccion = new VentanaCargaSeccion(apiAdmin, ventanaLogin, abiertos, todasSecciones, todasMaterias, docentesActivos);
        cargaSeccion.Show();
        await EsperarVista(cargaSeccion);
        var selectores = Descendientes(cargaSeccion).OfType<ListaBuscable>().ToList();
        selectores[0].Seleccionar(abiertos.First(p => p.Id == periodo));
        await EsperarVista(cargaSeccion);
        var opcionSeccion = ((System.Collections.IEnumerable)cargaSeccion.Vista.ListaSeccion.ItemsSource!).Cast<object>()
            .First(o => (Seccion)o.GetType().GetProperty("Seccion")!.GetValue(o)! is { Id: var id } && id == seccion);
        cargaSeccion.Vista.ListaSeccion.Seleccionar(opcionSeccion);
        await EsperarVista(cargaSeccion);
        var porDocente = Descendientes(cargaSeccion).OfType<ListaBuscable>().Skip(4).Where(l => l.Visibility == Visibility.Visible).ToList();
        Comprobar(porDocente.Count == 4, "asignar por sección lista las materias libres del pensum de la carrera");
        porDocente[0].Seleccionar(docentesActivos.First(d => d.Id == docente));
        porDocente[1].Seleccionar(docentesActivos.First(d => d.Id == docente));
        await EsperarVista(cargaSeccion);
        Capturar(cargaSeccion, "17-carga-seccion.png");
        porDocente[2].Caja.Focus();
        porDocente[2].Caja.Text = "ana";
        await Task.Delay(400);
        if (porDocente[2].Emergente.Child is FrameworkElement desplegable)
        {
            desplegable.UpdateLayout();
            var foto = new RenderTargetBitmap((int)desplegable.ActualWidth, (int)desplegable.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            foto.Render(desplegable);
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(foto));
            using var archivo = File.Create(Path.Combine(salida, "18-lista-buscable.png"));
            png.Save(archivo);
        }
        Comprobar(porDocente[2].Emergente.IsOpen, "escribir en la lista de docentes la abre filtrada");
        porDocente[2].Caja.Text = "";
        cargaSeccion.Vista.InputBindings.OfType<System.Windows.Input.KeyBinding>().First(k => k.Key == System.Windows.Input.Key.G).Command.Execute(null);
        for (var i = 0; i < 60 && cargaSeccion.IsVisible; i++) await Task.Delay(100);
        Comprobar(cargaSeccion.Resultado is { Creadas: 2 }, "Ctrl+G guarda la carga de la sección");
        if (cargaSeccion.IsVisible) cargaSeccion.Close();

        // Una sección de 2.º semestre muestra primero las de su semestre y las electivas; el resto, a pedido.
        var cargaD1 = new VentanaCargaSeccion(apiAdmin, ventanaLogin, abiertos, todasSecciones, todasMaterias, docentesActivos);
        cargaD1.Show();
        await EsperarVista(cargaD1);
        cargaD1.Vista.ListaPeriodo.Seleccionar(abiertos.First(p => p.Id == periodo));
        var opcionD1 = ((System.Collections.IEnumerable)cargaD1.Vista.ListaSeccion.ItemsSource!).Cast<object>()
            .First(o => (Seccion)o.GetType().GetProperty("Seccion")!.GetValue(o)! is { Nombre: "D1", CarreraId: var c } && c == carrera);
        cargaD1.Vista.ListaSeccion.Seleccionar(opcionD1);
        await EsperarVista(cargaD1);
        int Visibles() => Descendientes(cargaD1).OfType<ListaBuscable>().Skip(4).Count(l => l.Visibility == Visibility.Visible);
        Comprobar(Visibles() == 2 && cargaD1.Vista.TodoElPensum.Visibility == Visibility.Visible, "la sección D1 de 2.º semestre ve Cálculo II y la electiva");
        cargaD1.Vista.TodoElPensum.IsChecked = true;
        cargaD1.Vista.TodoElPensum.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        await EsperarVista(cargaD1);
        Comprobar(Visibles() == 5, "«Ver todo el pensum» muestra las cinco materias de la carrera");
        Capturar(cargaD1, "21-carga-por-semestre.png");
        cargaD1.Close();

        var ventanaDocente = new MainWindow(apiDocente);
        ventanaDocente.Show();
        ventanaDocente.MostrarAplicacion();
        await EsperarVista(ventanaDocente);
        Capturar(ventanaDocente, "03-docente.png");
        var ventanaPlanilla = new VentanaPlanilla(apiDocente, oferta) { Owner = ventanaDocente };
        ventanaPlanilla.Show();
        await EsperarVista(ventanaPlanilla);
        Capturar(ventanaPlanilla, "04-planilla.png");
        var rejilla = Descendientes(ventanaPlanilla).OfType<DataGrid>().First(g => g.Columns.Any(c => c.Header?.ToString() == "Acumulado /20"));
        var fila = ((System.Data.DataView)rejilla.ItemsSource)[0];
        Comprobar(fila["acumulado"].ToString()!.Replace(',', '.') == "7.45", "planilla calcula el avance decimal exacto en pantalla");
        Comprobar(fila["final"].ToString() == "Pendiente", "planilla muestra pendiente cuando falta una evaluación");
        Comprobar(rejilla.IsReadOnly, "la tabla de notas solo muestra: no se escribe en sus celdas");
        // Carga de notas en su ventana: la nota fuera de escala se rechaza y lo válido queda listo para guardar.
        var cargaNotas = new VentanaNotas(ventanaPlanilla, "Parcial", "Prueba",
            [new CampoNota(1, "Ana", "V-1", ""), new CampoNota(2, "Beto", "V-2", "12")],
            texto => texto.Length == 0 || (decimal.TryParse(texto.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture, out var n) && n is >= 1 and <= 20) ? null : "Fuera de escala");
        cargaNotas.Show();
        await EsperarVista(cargaNotas);
        cargaNotas.Cajas[0].Text = "25";
        Comprobar(!cargaNotas.Aceptar() && cargaNotas.IsVisible, "una nota fuera de la escala no deja guardar y se marca para corregirla");
        Capturar(cargaNotas, "22-cargar-notas.png");
        cargaNotas.Cajas[0].Text = "15,5";
        Comprobar(cargaNotas.Aceptar() && cargaNotas.Cambios.Count == 1 && cargaNotas.Cambios[1] == "15,5", "la ventana devuelve solo las notas que cambiaron");
        var planillaServidor = await apiDocente.Obtener<Planilla>($"ofertas/{oferta}");
        var primeraEvaluacion = planillaServidor.Evaluaciones[0].Id;
        var primerEstudiante = planillaServidor.Estudiantes.First(a => a.Estado == "confirmada").EstudianteId;
        await ventanaPlanilla.Vista.AplicarNotas([((primerEstudiante, primeraEvaluacion), "18")]);
        await EsperarVista(ventanaPlanilla);
        Comprobar((await apiDocente.Obtener<Planilla>($"ofertas/{oferta}")).Notas.Any(n => n.EstudianteId == primerEstudiante && n.EvaluacionId == primeraEvaluacion && n.Valor == 18)
            && Descendientes(ventanaPlanilla).OfType<Notificacion>().Any(), "las notas cargadas se guardan como borrador y se confirma con una notificación");
        ventanaPlanilla.Close();
        ventanaDocente.Hide();

        using var apiEstudiante = new ClienteApi();
        await apiEstudiante.Iniciar(direccion, (cedula + 1).ToString(), clave);
        var ventanaEstudiante = new MainWindow(apiEstudiante);
        ventanaEstudiante.Show();
        ventanaEstudiante.MostrarAplicacion();
        await EsperarVista(ventanaEstudiante);
        Capturar(ventanaEstudiante, "05-record.png");
        var registro = await apiEstudiante.Obtener<RecordAcademico>("record");
        ExportadorRecord.Pdf(registro, Path.Combine(salida, "record-demostracion.pdf"));
        ExportadorRecord.Pdf(Muestra(), Path.Combine(salida, "record-muestra.pdf"));
        Comprobar(File.ReadAllBytes(Path.Combine(salida, "record-muestra.pdf")).Length > 2000, "PDF con logotipo, varios períodos y notas finales");
        // Hoja de códigos para entregar en clase: 11 tiras, para ver también el salto de página.
        var tiras = Enumerable.Range(1, 11).Select(i => new CodigoEstudiante((30_100_000 + i).ToString(), $"Estudiante {i}", "De Prueba Muestra",
            Logica.Seguridad.NuevoCodigoAcceso(), DateTime.UtcNow.AddDays(7))).ToList();
        ExportadorCodigos.Pdf(new CodigosDeSeccion("Introducción a la Programación, sección 01S-2603-D1, 2-2026", tiras, 4), Path.Combine(salida, "codigos-muestra.pdf"));
        Comprobar(File.ReadAllBytes(Path.Combine(salida, "codigos-muestra.pdf")).Take(4).SequenceEqual("%PDF"u8.ToArray()), "hoja de códigos en PDF con una tira por estudiante");
        ExportadorRecord.Csv(registro, Path.Combine(salida, "record-demostracion.csv"));
        Comprobar(File.ReadAllBytes(Path.Combine(salida, "record-demostracion.pdf")).Take(4).SequenceEqual("%PDF"u8.ToArray()), "PDF real generado sin impresora ni internet");
        Comprobar(File.ReadAllText(Path.Combine(salida, "record-demostracion.csv")).Contains("1.5"), "CSV conserva los puntos de cada evaluación");
        var extenso = registro with { Materias = Enumerable.Range(1, 8).Select(i => registro.Materias[0] with
        {
            Materia = "Materia de demostración con nombre extenso " + i,
            Evaluaciones = Enumerable.Range(1, 22).Select(n => new DetallePublicado("Evaluación de demostración con descripción larga para verificar el ajuste de texto " + n, 4m, 15m, 3m, 0.6m)).ToList()
        }).ToList() };
        ExportadorRecord.Pdf(extenso, Path.Combine(salida, "record-paginacion.pdf"));
        var imprimible = ExportadorRecord.Imprimible(registro);
        imprimible.PageWidth = 790;
        imprimible.PageHeight = 1100;
        var paginador = ((System.Windows.Documents.IDocumentPaginatorSource)imprimible).DocumentPaginator;
        paginador.ComputePageCount();
        Comprobar(paginador.PageCount >= 1, "documento imprimible pagina correctamente");
        var hoja = paginador.GetPage(0).Visual;
        var impresa = new RenderTargetBitmap(790, 1100, 96, 96, PixelFormats.Pbgra32);
        var fondo = new DrawingVisual();
        using (var lienzo = fondo.RenderOpen()) lienzo.DrawRectangle(Brushes.White, null, new Rect(0, 0, 790, 1100));
        impresa.Render(fondo);
        impresa.Render(hoja);
        var codificador = new PngBitmapEncoder();
        codificador.Frames.Add(BitmapFrame.Create(impresa));
        using (var archivo = File.Create(Path.Combine(salida, "14-record-impreso.png"))) codificador.Save(archivo);
        var perdida = new ClienteApi();
        perdida.Configurar("http://127.0.0.1:5199");
        try { await perdida.Obtener<RecordAcademico>("record"); throw new InvalidOperationException("No detectó servidor caído."); }
        catch (ErrorServicio error) { Comprobar(error.Message.Contains("conexión"), "caída de conexión produce mensaje comprensible"); }
        perdida.Dispose();

        // Planilla grande: 2000 estudiantes y 30 evaluaciones, sin pasar por el servidor.
        var evaluacionesGrandes = Enumerable.Range(1, 30).Select(i => new Evaluacion(i, $"Evaluación {i}", i <= 10 ? 4m : 3m, i)).ToList();
        var alumnosGrandes = Enumerable.Range(1, 2000).Select(i => new Alumno(i, i, (30_000_000 + i).ToString(), $"Estudiante {i}", "Prueba", "confirmada")).ToList();
        var notasGrandes = alumnosGrandes.SelectMany(a => evaluacionesGrandes.Select(e => new Nota(e.Id, a.EstudianteId, 10m + (a.EstudianteId + e.Id) % 10))).ToList();
        var ofertaGrande = new Oferta(999_999, 1, "Prueba de volumen", "Informática", "A", "Materia con 2000 inscritos", 1, "Docente de prueba", 1, false, 2000, 0, 1);
        var ventanaGrande = new VentanaPlanilla(apiDocente, ofertaGrande.Id, cargarAlAbrir: false) { Owner = ventanaDocente };
        ventanaGrande.Show();
        var cronometro = System.Diagnostics.Stopwatch.StartNew();
        ventanaGrande.Vista.Presentar(new Planilla(ofertaGrande, evaluacionesGrandes, alumnosGrandes, notasGrandes,
            new ReglasCalificacion(1, 20, 10, Redondeos.Media, Indices.Ponderado, true)));
        ventanaGrande.UpdateLayout();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        cronometro.Stop();
        Comprobar(cronometro.ElapsedMilliseconds < 4000, $"planilla de 2000 × 30 lista en {cronometro.ElapsedMilliseconds} ms");
        var rejillaGrande = Descendientes(ventanaGrande).OfType<DataGrid>().First(g => g.Columns.Any(c => c.Header?.ToString() == "Acumulado /20"));
        var vistaGrande = (System.Data.DataView)rejillaGrande.ItemsSource;
        Comprobar(vistaGrande.Count == 2000 && vistaGrande[0]["final"].ToString() is { Length: > 0 } f && f != "Pendiente", "planilla grande calcula notas finales");
        var buscador = Descendientes(ventanaGrande).OfType<PanelTabla>().First(p => p.IsVisible && p.Buscar is { Length: > 0 }).Buscador;
        cronometro.Restart();
        buscador.Text = "Estudiante 1999";
        cronometro.Stop();
        Comprobar(vistaGrande.Count == 1 && cronometro.ElapsedMilliseconds < 500, $"buscar entre 2000 estudiantes deja una fila en {cronometro.ElapsedMilliseconds} ms");
        buscador.Text = "";
        cronometro.Restart();
        vistaGrande[0].Row["e1"] = "20";
        cronometro.Stop();
        Comprobar(vistaGrande[0]["acumulado"].ToString() != "" && cronometro.ElapsedMilliseconds < 200, $"editar una nota recalcula la fila en {cronometro.ElapsedMilliseconds} ms");
        await Task.Delay(300);
        Comprobar(rejillaGrande.Columns.Skip(2).Take(30).All(c => c.ActualWidth >= 132), "con 30 evaluaciones las columnas conservan su ancho y aparece desplazamiento horizontal");
        Capturar(ventanaGrande, "10-planilla-grande.png");
        ventanaGrande.CerrarSinConfirmar();

        var atajos = new VentanaAtajos(ventanaDocente);
        atajos.Show();
        await EsperarVista(atajos);
        Capturar(atajos, "11-atajos.png");
        atajos.Close();

        // Sesión revocada mientras la ventana está abierta: debe volver al acceso y explicarlo.
        var cedulaSesion = Random.Shared.Next(10_000_000, 89_000_000);
        var cuentaSesion = await apiAdmin.Enviar<long>(HttpMethod.Post, "usuarios", new NuevaPersona(cedulaSesion.ToString(), "Sesión", "Revocada", Roles.Docente));
        var codigoSesion = await apiAdmin.Enviar<CodigoAcceso>(HttpMethod.Post, $"usuarios/{cuentaSesion}/codigo", new { });
        await apiAdmin.Enviar(HttpMethod.Post, "activar", new ActivarCuenta(cedulaSesion.ToString(), codigoSesion.Codigo, clave));
        using var apiSesion = new ClienteApi();
        await apiSesion.Iniciar(direccion, cedulaSesion.ToString(), clave);
        var ventanaSesion = new MainWindow(apiSesion);
        ventanaSesion.Show();
        ventanaSesion.MostrarAplicacion();
        await EsperarVista(ventanaSesion);
        await apiAdmin.Enviar<bool>(HttpMethod.Put, $"usuarios/{cuentaSesion}/estado", new EstadoCuenta(false));
        try { await apiSesion.Obtener<List<Oferta>>("ofertas"); } catch (ErrorServicio) { }
        for (var i = 0; i < 40 && ventanaSesion.Acceso.Visibility != Visibility.Visible; i++) await Task.Delay(50);
        var aviso = Application.Current.Windows.OfType<Dialogo>().FirstOrDefault();
        Comprobar(ventanaSesion.Acceso.Visibility == Visibility.Visible && apiSesion.Persona is null && aviso is not null,
            "sesión revocada vuelve al acceso y lo explica en un modal");
        aviso?.Close();
        ventanaSesion.Close();

        // Primera vez con el correo de la matrícula: la ventana de dos pasos, con el buzón de pruebas.
        var buzon = Environment.GetEnvironmentVariable("ACADEMICO_CORREO_CARPETA")!;
        var cedulaRegistro = Random.Shared.Next(10_000_000, 89_000_000).ToString();
        var correoRegistro = $"registro.{sufijo}@prueba.test";
        await apiAdmin.Enviar<long>(HttpMethod.Post, "usuarios", new NuevaPersona(cedulaRegistro, "Luisa", "Registro", Roles.Estudiante, correoRegistro));
        var anonimo = new ClienteApi();
        anonimo.Configurar(direccion);
        var ventanaRegistro = new VentanaRegistro(ventanaLogin, anonimo, Cedulas.Mostrar(cedulaRegistro));
        ventanaRegistro.Show();
        await EsperarVista(ventanaRegistro);
        Capturar(ventanaRegistro, "25-registro-cedula.png");
        ventanaRegistro.Enviar.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        for (var i = 0; i < 60 && ventanaRegistro.PasoCodigo.Visibility != Visibility.Visible; i++) await Task.Delay(100);
        var correoRecibido = Directory.GetFiles(buzon, "*.json").Order().Select(a => System.Text.Json.JsonDocument.Parse(File.ReadAllText(a)).RootElement)
            .Last(c => c.GetProperty("para").GetString() == correoRegistro);
        ventanaRegistro.Codigo.Text = System.Text.RegularExpressions.Regex.Match(correoRecibido.GetProperty("texto").GetString()!, @"\b(\d{6})\b").Groups[1].Value;
        ventanaRegistro.Nueva.Password = ventanaRegistro.Repetir.Password = clave;
        await EsperarVista(ventanaRegistro);
        Capturar(ventanaRegistro, "26-registro-codigo.png");
        File.WriteAllText(Path.Combine(salida, "27-correo-codigo.html"), correoRecibido.GetProperty("html").GetString());
        ventanaRegistro.Guardar.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        for (var i = 0; i < 60 && ventanaRegistro.IsVisible; i++) await Task.Delay(100);
        using (var nueva = new ClienteApi())
        {
            await nueva.Iniciar(direccion, cedulaRegistro, clave);
            Comprobar(ventanaRegistro.Listo && nueva.Persona?.Cedula == cedulaRegistro, "la ventana de primera vez envía el código al correo y la estudiante entra con su contraseña");
        }
        anonimo.Dispose();        await PantallasPequenas(apiAdmin, apiDocente, apiEstudiante, oferta);        await UsoCaotico(ventanaLogin, apiAdmin);

        // Cerrar sesión desde el menú: cierra las planillas abiertas y vuelve al acceso, listo para otra persona.
        var planillaAbierta = new VentanaPlanilla(apiAdmin, oferta) { Owner = ventanaLogin };
        planillaAbierta.Show();
        await EsperarVista(planillaAbierta);
        ventanaLogin.OpcionSalir.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        for (var i = 0; i < 60 && ventanaLogin.Acceso.Visibility != Visibility.Visible; i++) await Task.Delay(100);
        Comprobar(ventanaLogin.Acceso.Visibility == Visibility.Visible && !planillaAbierta.IsVisible && apiAdmin.Persona is null && ventanaLogin.Contenido.Content is null,
            "cerrar sesión cierra las planillas, borra la sesión y vuelve al acceso");
        await EsperarVista(ventanaLogin);
        await Task.Delay(2400); // que termine de dibujarse el trazo
        Capturar(ventanaLogin, "01-acceso.png");
        await apiAdmin.Iniciar(direccion, Environment.GetEnvironmentVariable("ACADEMICO_ADMIN_CEDULA")!, Environment.GetEnvironmentVariable("ACADEMICO_ADMIN_CLAVE")!);
        ventanaLogin.MostrarAplicacion();
        await EsperarVista(ventanaLogin);
        Comprobar(ventanaLogin.Aplicacion.Visibility == Visibility.Visible && Descendientes(ventanaLogin).OfType<VistaAdministracion>().Any(), "después de salir se puede volver a entrar sin reiniciar");
        ventanaEstudiante.Close();
        ventanaDocente.Close();
        ventanaLogin.Close();
        Console.WriteLine($"{comprobaciones} comprobaciones de escritorio correctas. Evidencia en {salida}");
    }

    /// <summary>
    /// Laptops y Canaimas: cada pantalla y cada ventana con el área de trabajo de 1024 × 560 y de 1366 × 728.
    /// Ningún botón puede quedar fuera de la ventana; lo que no cabe debe poder desplazarse.
    /// </summary>
    private static async Task PantallasPequenas(ClienteApi admin, ClienteApi docente, ClienteApi estudiante, long oferta)
    {
        var fallas = new List<string>();
        var revisadas = 0;
        // Un aviso de error en medio de la revisión es una falla; se anota y se cierra para no quedarse esperando.
        var vigia = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        vigia.Tick += (_, _) =>
        {
            foreach (var aviso in Application.Current.Windows.OfType<Dialogo>().Where(d => d.IsVisible && d.Title != "Cerrar período").ToList())
            {
                fallas.Add("aviso inesperado: " + string.Join(" ", Descendientes(aviso).OfType<TextBlock>().Select(b => b.Text).Take(3)));
                aviso.Close();
            }
        };
        vigia.Start();
        void Revisar(Window ventana, string nombre)
        {
            ventana.UpdateLayout();
            revisadas++;
            if (ventana.ActualWidth > Pantalla.Area.Width + 1 || ventana.ActualHeight > Pantalla.Area.Height + 1)
                fallas.Add($"{nombre}: la ventana mide {ventana.ActualWidth:0}×{ventana.ActualHeight:0} y no cabe en la pantalla");
            foreach (var boton in Descendientes(ventana).OfType<System.Windows.Controls.Primitives.ButtonBase>().Where(b => b.IsVisible && b.ActualWidth > 0))
            {
                if (Ancestro<ScrollViewer>(boton) is not null) continue;
                var caja = boton.TransformToAncestor(ventana).TransformBounds(new Rect(boton.RenderSize));
                if (caja.Left < -0.5 || caja.Top < -0.5 || caja.Right > ventana.ActualWidth + 0.5 || caja.Bottom > ventana.ActualHeight + 0.5)
                    fallas.Add($"{nombre}: botón «{(AutomationProperties.GetName(boton) is { Length: > 0 } n ? n : (boton as BotonAccion)?.Texto ?? boton.ToString())}» queda cortado");
            }
            // Botones que comparten renglón: misma altura y alineados.
            var botones = Descendientes(ventana).OfType<System.Windows.Controls.Button>()
                .Where(b => b.IsVisible && b.ActualHeight > 0 && b is not { Name: "PART_Minimizar" or "PART_Maximizar" or "PART_Cerrar" }
                    && Ancestro<DataGrid>(b) is null && Ancestro<System.Windows.Controls.Primitives.Popup>(b) is null)
                .Select(b => (Boton: b, Caja: b.TransformToAncestor(ventana).TransformBounds(new Rect(b.RenderSize)))).ToList();
            foreach (var (boton, caja) in botones)
                foreach (var (otro, otraCaja) in botones)
                {
                    if (ReferenceEquals(boton, otro)) continue;
                    var cruce = Rect.Intersect(caja, otraCaja);
                    if (!cruce.IsEmpty && cruce.Width > 1 && cruce.Height > 1 && otraCaja.Left >= caja.Left)
                        fallas.Add($"{nombre}: «{Texto(boton)}» y «{Texto(otro)}» se superponen");
                    if (otraCaja.Left <= caja.Left) continue;
                    var mismoRenglon = Math.Abs((caja.Top + caja.Bottom) / 2 - (otraCaja.Top + otraCaja.Bottom) / 2) < Math.Max(caja.Height, otraCaja.Height) / 2
                        && otraCaja.Left - caja.Right < 40 && otraCaja.Left - caja.Right > -1;
                    if (mismoRenglon && (Math.Abs(caja.Height - otraCaja.Height) > 1.5 || Math.Abs(caja.Top - otraCaja.Top) > 1.5))
                        fallas.Add($"{nombre}: «{Texto(boton)}» ({caja.Height:0} px) y «{Texto(otro)}» ({otraCaja.Height:0} px) están juntos pero no miden ni se alinean igual");
                }
            Capturar(ventana, $"pantalla-{Pantalla.Area.Width:0}-{nombre}.png");
        }
        foreach (var area in new[] { "1024x560", "1366x728", "1920x1040" })
        {
            Environment.SetEnvironmentVariable("MERITO_AREA", area);
            var acceso = new MainWindow(new ClienteApi());
            acceso.Show();
            await Task.Delay(2600);
            Revisar(acceso, "acceso");
            acceso.Close();

            var principal = new MainWindow(admin);
            principal.Show();
            principal.MostrarAplicacion();
            await EsperarVista(principal);
            var pestanas = Descendientes(principal).OfType<TabItem>().Where(t => AutomationProperties.GetName(t) is { Length: > 0 }).ToList();
            foreach (var pestana in pestanas)
            {
                pestana.IsSelected = true;
                await EsperarVista(principal);
                Revisar(principal, AutomationProperties.GetName(pestana));
                foreach (var interna in Descendientes(principal).OfType<TabItem>().Where(t => t.Header is string).ToList())
                {
                    interna.IsSelected = true;
                    await EsperarVista(principal);
                    Revisar(principal, $"{AutomationProperties.GetName(pestana)}-{interna.Header}");
                }
            }
            Descendientes(principal).OfType<System.Windows.Controls.Primitives.ToggleButton>().First(b => AutomationProperties.GetName(b) == "Materias y matrícula")
                .RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            await EsperarVista(principal);
            Revisar(principal, "materias-y-matricula");

            var planilla = new VentanaPlanilla(admin, oferta) { Owner = principal };
            planilla.Show();
            await EsperarVista(planilla);
            Revisar(planilla, "planilla");
            planilla.CerrarSinConfirmar();

            var periodos = (await admin.Obtener<List<Periodo>>("periodos")).Where(p => !p.Cerrado).ToList();
            var carga = new VentanaCargaSeccion(admin, principal, periodos, await admin.Obtener<List<Seccion>>("secciones"),
                await admin.Obtener<List<MateriaPensum>>("pensum"), await admin.Obtener<List<Persona>>("docentes"));
            carga.Show();
            await EsperarVista(carga);
            Revisar(carga, "asignar-por-seccion");
            carga.Close();

            var formulario = new Formulario(principal, "Reglas de calificación", "Prueba de un formulario largo en una pantalla pequeña.", 600);
            formulario.Par("Escala: desde", "1", "hasta", "20");
            foreach (var campo in new[] { "Nota aprobatoria", "Redondeo", "Índice", "Aplazadas", "Otro campo", "Uno más" }) formulario.Texto(campo, "", "Ayuda del campo.");
            formulario.Show();
            await EsperarVista(formulario);
            Revisar(formulario, "formulario-largo");
            formulario.Close();

            var filas = Enumerable.Range(1, 40).Select(i => new FilaPensum($"MAT-{i}", $"Materia {i}", 3, i % 10, "Materia nueva")).ToList();
            var importacion = new VentanaImportacion(principal, "Revisar pensum", "40 materias", "Pensum de prueba.", filas, "Semestre=SemestreTexto; Código=Codigo; Materia=Nombre; UC=Uc; Qué pasa=Accion", "Importar pensum");
            importacion.Show();
            await EsperarVista(importacion);
            Revisar(importacion, "revisar-importacion");
            importacion.Close();

            var errores = new VentanaErroresExcel(principal, Enumerable.Range(5, 30).Select(i => new ErrorFila(i, "El código está repetido en el archivo.")).ToList(), 12);
            errores.Show();
            await EsperarVista(errores);
            Revisar(errores, "errores-excel");
            errores.Close();

            var registro = new VentanaRegistro(principal, new ClienteApi(), "V-12345678");
            registro.PasoCedula.Visibility = Visibility.Collapsed;
            registro.PasoCodigo.Visibility = Visibility.Visible;
            registro.Reenviar.Content = "¿No te llegó? Podrás pedir otro en 59 s";
            registro.Show();
            await EsperarVista(registro);
            Revisar(registro, "registro-por-correo");
            registro.Close();
            var atajos = new VentanaAtajos(principal);
            atajos.Show();
            await EsperarVista(atajos);
            Revisar(atajos, "atajos");
            atajos.Close();

            var dialogo = new Dialogo(principal, "Cerrar período", "¿Cerrar el período? Las calificaciones quedarán bloqueadas para edición.", Tono.Peligro, "Cerrar período", "Cancelar");
            dialogo.Show();
            await EsperarVista(dialogo);
            Revisar(dialogo, "confirmacion");
            dialogo.Close();
            principal.Close();

            foreach (var (api, nombre) in new[] { (docente, "docente"), (estudiante, "estudiante") })
            {
                var otra = new MainWindow(api);
                otra.Show();
                otra.MostrarAplicacion();
                await EsperarVista(otra);
                Revisar(otra, nombre);
                otra.Close();
            }
        }
        Environment.SetEnvironmentVariable("MERITO_AREA", null);
        vigia.Stop();
        Comprobar(fallas.Count == 0, fallas.Count == 0 ? $"en 1024 × 560, 1366 × 728 y 1920 × 1040 caben las {revisadas} pantallas revisadas, sin botones cortados ni desalineados"
            : "pantallas pequeñas con problemas:\n  " + string.Join("\n  ", fallas.Distinct().Take(20)));
    }

    private static string Texto(System.Windows.Controls.Primitives.ButtonBase boton) =>
        AutomationProperties.GetName(boton) is { Length: > 0 } n ? n : (boton as BotonAccion)?.Texto ?? boton.Content?.ToString() ?? "botón";

    private static T? Ancestro<T>(DependencyObject? elemento) where T : DependencyObject
    {
        elemento = elemento is null ? null : VisualTreeHelper.GetParent(elemento);
        while (elemento is not null and not T) elemento = VisualTreeHelper.GetParent(elemento);
        return elemento as T;
    }
    /// <summary>
    /// Alguien que «loquea» con la app: pulsa cualquier botón, cambia de pestaña sin parar, escribe basura en cada campo,
    /// acepta formularios llenos de basura y cierra todo lo que se abre. La app no puede caerse ni llegar a «Algo salió mal».
    /// </summary>
    private static async Task UsoCaotico(MainWindow ventana, ClienteApi api)
    {
        var azar = new Random(20261020);
        var inesperados = new List<string>();
        var registro = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Merito", "diagnostico", $"errores-{DateTime.Today:yyyy-MM-dd}.log");
        var registroAntes = File.Exists(registro) ? new FileInfo(registro).Length : 0;
        DispatcherUnhandledExceptionEventHandler contar = (_, e) => inesperados.Add(e.Exception.GetType().Name + ": " + e.Exception.Message);
        Application.Current.DispatcherUnhandledException += contar;
        string[] basura = ["", " ", "abc", "-1", "0", "99999999999999999999", "12,345", "1e9", "'; DROP TABLE materias; --", "<script>alert(1)</script>",
            "😀 ñandú", new string('x', 400), "V-", "20.001", "{seccion}{", "\t\n", "NaN", "∞", "%", "\\\\servidor\\x"];
        // No se tocan los botones que abren diálogos de archivos o de impresión de Windows: una persona los cancelaría, la prueba se quedaría esperando.
        string[] sinTocar = ["Importar Excel", "Plantilla", "Descargar plantilla", "Importar", "Descargar PDF", "Exportar CSV", "Imprimir", "Exportar a Excel"];
        int aceptados = 0, cerrados = 0, acciones = 0;
        var cerrador = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(90) };
        cerrador.Tick += (_, _) =>
        {
            foreach (var otra in Application.Current.Windows.OfType<Window>().Where(v => v.IsVisible && v != ventana).ToList())
            {
                if (Descendientes(otra).OfType<TextBlock>().Any(b => b.Text == "Algo salió mal")) inesperados.Add("Aviso «Algo salió mal» en " + otra.Title);
                if (otra is Formulario formulario && azar.Next(3) > 0)
                {
                    foreach (var caja in Descendientes(formulario).OfType<TextBox>().Where(c => c.IsVisible)) caja.Text = basura[azar.Next(basura.Length)];
                    if (Descendientes(formulario).OfType<Button>().FirstOrDefault(b => b.IsDefault) is { } aceptar)
                    {
                        aceptar.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                        aceptados++;
                        if (!formulario.IsVisible) continue;
                    }
                }
                otra.Close();
                cerrados++;
            }
        };
        cerrador.Start();
        try
        {
            for (var paso = 0; paso < 450; paso++)
            {
                var visibles = Descendientes(ventana).OfType<FrameworkElement>().Where(e => e.IsVisible && e.IsEnabled).ToList();
                switch (azar.Next(10))
                {
                    case 0 or 1:
                        if (visibles.OfType<TabItem>().ToList() is { Count: > 0 } pestanas) pestanas[azar.Next(pestanas.Count)].IsSelected = true;
                        break;
                    case 2 or 3 or 4:
                        var botones = visibles.OfType<System.Windows.Controls.Primitives.ButtonBase>()
                            .Where(b => b is not System.Windows.Controls.Primitives.ToggleButton && (b is not BotonAccion accion || !sinTocar.Contains(accion.Texto)) && b.IsDescendantOf(ventana.Contenido)).ToList();
                        if (botones.Count > 0) botones[azar.Next(botones.Count)].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                        break;
                    case 5:
                        if (visibles.OfType<TextBox>().Where(c => c.IsDescendantOf(ventana.Contenido)).ToList() is { Count: > 0 } cajas) cajas[azar.Next(cajas.Count)].Text = basura[azar.Next(basura.Length)];
                        break;
                    case 6:
                        if (visibles.OfType<CheckBox>().ToList() is { Count: > 0 } casillas) casillas[azar.Next(casillas.Count)].IsChecked ^= true;
                        break;
                    case 7:
                        // Atajos de teclado de la vista: Ctrl+U, F5 y los demás, a cualquier ritmo.
                        var atajos = visibles.OfType<VistaBase>().SelectMany(v => v.InputBindings.OfType<System.Windows.Input.KeyBinding>()).ToList();
                        if (atajos.Count > 0 && atajos[azar.Next(atajos.Count)].Command is { } orden && orden.CanExecute(null)) orden.Execute(null);
                        break;
                    case 8:
                        if (visibles.OfType<Rejilla>().Where(r => r.Items.Count > 0).ToList() is { Count: > 0 } tablas)
                        {
                            var tabla = tablas[azar.Next(tablas.Count)];
                            tabla.SelectedIndex = azar.Next(tabla.Items.Count);
                        }
                        break;
                    default:
                        // La navegación lateral, incluso dos veces seguidas.
                        if (visibles.OfType<System.Windows.Controls.Primitives.ToggleButton>().Where(b => b.IsDescendantOf(ventana.Aplicacion) && !b.IsDescendantOf(ventana.Contenido)).ToList() is { Count: > 0 } secciones)
                            secciones[azar.Next(secciones.Count)].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                        break;
                }
                acciones++;
                if (azar.Next(4) == 0) await Task.Delay(azar.Next(0, 120));
                else await Dispatcher.Yield(DispatcherPriority.Background);
            }
            // Que termine lo que quedó en curso.
            for (var i = 0; i < 200 && Descendientes(ventana).OfType<VistaBase>().Any(v => v.Ocupada); i++) await Task.Delay(50);
            await Task.Delay(600);
        }
        finally
        {
            cerrador.Stop();
            Application.Current.DispatcherUnhandledException -= contar;
            foreach (var otra in Application.Current.Windows.OfType<VentanaPlanilla>().ToList()) otra.CerrarSinConfirmar();
            foreach (var otra in Application.Current.Windows.OfType<Window>().Where(v => v.IsVisible && v != ventana && v is not MainWindow).ToList()) otra.Close();
        }
        var registroDespues = File.Exists(registro) ? new FileInfo(registro).Length : 0;
        if (registroDespues > registroAntes)
            using (var lector = new StreamReader(new FileStream(registro, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            { lector.BaseStream.Seek(registroAntes, SeekOrigin.Begin); inesperados.Add("Registro de errores: " + lector.ReadToEnd()[..Math.Min(1500, (int)(registroDespues - registroAntes))]); }
        Comprobar(inesperados.Count == 0, inesperados.Count == 0
            ? $"uso caótico: {acciones} acciones al azar, {aceptados} formularios aceptados con basura y {cerrados} ventanas cerradas sin un solo fallo"
            : "fallos durante el uso caótico:\n  " + string.Join("\n  ", inesperados.Distinct().Take(8)));
        Comprobar(ventana.IsVisible && api.Persona is not null, "después del uso caótico la ventana sigue abierta y la sesión activa");
        // Lo que el caos pudo haber cambiado en las reglas se devuelve a fábrica para las pruebas siguientes.
        await api.Enviar<ReglasCalificacion>(System.Net.Http.HttpMethod.Put, "reglas", new ReglasCalificacion(1, 20, 10, Redondeos.Media, Indices.Ponderado, true));
    }

    private static IEnumerable<DependencyObject> Descendientes(DependencyObject raiz)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var hijo = VisualTreeHelper.GetChild(raiz, i);
            yield return hijo;
            foreach (var nieto in Descendientes(hijo)) yield return nieto;
        }
    }

    private static async Task EsperarVista(Window ventana)
    {
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        for (var i = 0; i < 100; i++)
        {
            if (Descendientes(ventana).OfType<VistaBase>().All(v => !v.Ocupada)) break;
            await Task.Delay(50);
        }
        // Deja terminar la transición de entrada antes de capturar.
        await Task.Delay(700);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var errores = Descendientes(ventana).OfType<VistaBase>().Where(v => v.UltimoError is not null).ToList();
        if (errores.Count > 0) throw new InvalidOperationException(string.Join("; ", errores.Select(e => e.UltimoError)));
    }

    // Récord ficticio con dos períodos para revisar el diseño del PDF completo.
    private static RecordAcademico Muestra()
    {
        DetallePublicado E(string n, decimal p, decimal? v) => new(n, p, v, v * p / 20m, v * p / 100m);
        MateriaRecord M(string periodo, string materia, bool cerrado, decimal? final, params DetallePublicado[] ev) => new(1, periodo, "Informática", "A",
            materia, new DateTime(2026, 7, 10, 15, 0, 0, DateTimeKind.Utc), cerrado, ev.ToList(), ev.Sum(e => e.AporteSobreVeinte ?? 0), ev.Where(e => e.Nota != null).Sum(e => e.Peso), 100m,
            final != null, final, final is null ? null : final >= 10 ? Condiciones.Aprobado : Condiciones.Aplazado);
        return new RecordAcademico(new Persona(1, "30111222", "Estudiante", "de muestra", Roles.Estudiante, true), [
            M("1-2026", "Matemática I", true, 15m, E("Primer parcial", 30, 14), E("Segundo parcial", 30, 16), E("Final", 40, 15)),
            M("1-2026", "Lógica de programación", true, 14.50m, E("Parcial", 25, 12), E("Proyecto", 35, 16), E("Final", 40, 14.75m)),
            M("1-2026", "Inglés técnico", true, 9m, E("Lectura", 50, 8), E("Oral", 50, 10)),
            M("2-2026", "Matemática II", false, null, E("Primer parcial", 30, 13.5m), E("Segundo parcial", 30, null), E("Final", 40, null))
        ], 12.83m);
    }
    private static void Capturar(Window ventana, string nombre)
    {
        ventana.UpdateLayout();
        // Se captura la ventana completa para revisar también la barra de título propia.
        var raiz = (FrameworkElement)VisualTreeHelper.GetChild(ventana, 0);
        var imagen = new RenderTargetBitmap((int)raiz.ActualWidth, (int)raiz.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        imagen.Render(raiz);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(imagen));
        using var archivo = File.Create(Path.Combine(salida, nombre));
        png.Save(archivo);
    }
}
