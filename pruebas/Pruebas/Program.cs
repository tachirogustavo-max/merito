using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Datos;
using Entidades;
using Logica;
using ClosedXML.Excel;

var comprobaciones = 0;
void Comprobar(bool condicion, string nombre)
{
    if (!condicion) throw new InvalidOperationException("FALLÓ: " + nombre);
    comprobaciones++;
    Console.WriteLine("correcto: " + nombre);
}
void Rechaza(Action accion, string nombre)
{
    try { accion(); throw new InvalidOperationException("No se rechazó: " + nombre); }
    catch (ReglaException) { Comprobar(true, nombre); }
}

// Reglas de fábrica y las de un PNF: escala de 1 a 20, aprueba con 12, redondeo al entero.
var fabrica = new ReglasCalificacion(1, 20, 10, Redondeos.Media, Indices.Ponderado, true);
var pnf = new ReglasCalificacion(1, 20, 12, Redondeos.Entero, Indices.Ponderado, true);
Comprobar(Calificacion.Aporte(10m, 15m) == 1.5m, "aporte sobre veinte sin redondeo prematuro");
Comprobar(Calificacion.PorcentajeObtenido(10m, 15m, 20m) == 7.5m, "aporte porcentual de 10/20 en evaluación de 15 %");
Comprobar(Calificacion.PorcentajeObtenido(50m, 15m, 100m) == 7.5m, "aporte porcentual en escala de 100");
Comprobar(Calificacion.Redondear(0.51m, Redondeos.Media) == 1, "0,51 redondea a 1");
Comprobar(Calificacion.Redondear(14.50m, Redondeos.Media) == 14.50m, "exactamente 0,50 se conserva sin redondear");
Comprobar(Calificacion.Redondear(10.5001m, Redondeos.Media) == 11, "fracción mayor a 0,50 sube sin perder precisión");
Comprobar(Calificacion.Redondear(12.49m, Redondeos.Media) == 12, "fracción menor a 0,50 se descarta");
Comprobar(Calificacion.Redondear(11.50m, Redondeos.Entero) == 12, "redondeo al entero: 11,50 sube a 12");
Comprobar(Calificacion.Redondear(11.495m, Redondeos.Ninguno) == 11.50m, "sin redondeo deja dos decimales");
Comprobar(Calificacion.Condicion(10m, fabrica.NotaAprobatoria) == Condiciones.Aprobado, "10 es aprobado con las reglas de fábrica");
Comprobar(Calificacion.Condicion(9.50m, fabrica.NotaAprobatoria) == Condiciones.Aplazado, "9,50 conservado sigue aplazado");
Comprobar(Calificacion.Condicion(11m, pnf.NotaAprobatoria) == Condiciones.Aplazado, "en un PNF 11 queda aplazado");
Comprobar(Calificacion.Calcular([(50m, 10m), (50m, 19m)], Redondeos.Media).Final == 14.50m, "nota final de 14,50 se conserva");
Comprobar(Calificacion.Calcular([(50m, 10m), (50m, 19m)], Redondeos.Entero).Final == 15m, "con redondeo al entero 14,50 es 15");
Comprobar(Calificacion.Calcular([(50m, 10m), (40m, 19m)], Redondeos.Media).Final is null, "plan incompleto no produce nota final");
Comprobar(Calificacion.Indice([(18m, 4, true), (10m, 1, true)], fabrica) == 16.40m, "índice ponderado por UC: (18x4 + 10x1) / 5 = 16,40");
Comprobar(Calificacion.Indice([(18m, 4, true), (10m, 1, true)], fabrica with { Indice = Indices.Simple }) == 14m, "promedio simple: 14");
Comprobar(Calificacion.Indice([(18m, 4, true), (8m, 2, false)], fabrica with { IndiceConAplazadas = false }) == 18m, "el índice puede dejar fuera las aplazadas");
Comprobar(Calificacion.Indice([(18m, 0, true)], fabrica) is null, "sin UC cargadas el índice ponderado queda pendiente, no divide entre cero");
Rechaza(() => Calificacion.Validar(fabrica with { EscalaMinima = 20 }), "la escala exige mínimo menor que máximo");
Rechaza(() => Calificacion.Validar(fabrica with { EscalaMaxima = 101 }), "la escala no pasa de 100");
Rechaza(() => Calificacion.Validar(fabrica with { NotaAprobatoria = 21 }), "la nota aprobatoria cabe en la escala");
Rechaza(() => Calificacion.Validar(fabrica with { Redondeo = "otro" }), "redondeo desconocido rechazado");
Comprobar(Pensum.Codigo(" mat-21114 ") == "MAT-21114", "el código de materia se normaliza a mayúsculas");
Rechaza(() => Pensum.Codigo("MAT 21114"), "el código de materia no lleva espacios");
Rechaza(() => Pensum.Uc(61), "las UC no pasan de 60");
var (filasPensum, erroresPensum) = Pensum.Revisar([new FilaCrudaPensum(5, "MAT-1", "Cálculo I", "4", "1", false),
    new FilaCrudaPensum(6, "mat-1", "Cálculo I", "4", "1", false), new FilaCrudaPensum(7, "FIS-1", "Física", "cuatro", "1", false),
    new FilaCrudaPensum(8, "ELE-1", "Oratoria", "2", "", false)]);
Comprobar(filasPensum.Count == 2 && erroresPensum.Select(e => e.Fila).SequenceEqual([6, 7]), "el Excel del pensum señala código repetido y UC no numérica por fila");
Comprobar(filasPensum[1].Fila.Semestre == 0, "semestre vacío en el Excel es electiva");
var existente = new Materia(9, "MAT-1", "Cálculo I", 4, null, "Ing. Civil");
var (plan1, conflictos1) = Pensum.Planear([(5, new FilaPensum("MAT-1", "cálculo i", 4, 1))], new Dictionary<string, Materia> { ["MAT-1"] = existente }, new Dictionary<long, int>());
Comprobar(conflictos1.Count == 0 && plan1[0].Accion.StartsWith("Se agrega"), "ciclo básico: el mismo código de otra carrera se agrega, no se duplica");
var (_, conflictos2) = Pensum.Planear([(5, new FilaPensum("MAT-1", "Química", 4, 1))], new Dictionary<string, Materia> { ["MAT-1"] = existente }, new Dictionary<long, int>());
Comprobar(conflictos2.Count == 1, "un código existente con otro nombre es un error de fila");
var (_, conflictos3) = Pensum.Planear([(5, new FilaPensum("MAT-1", "Cálculo I", 5, 1))], new Dictionary<string, Materia> { ["MAT-1"] = existente }, new Dictionary<long, int>());
Comprobar(conflictos3.Count == 1, "un código existente con otras UC es un error de fila");
Comprobar(RedLocal.EsPrivada(System.Net.IPAddress.Parse("192.168.1.20")) && !RedLocal.EsPrivada(System.Net.IPAddress.Parse("8.8.8.8")), "red local distingue direcciones privadas");
Comprobar(RedLocal.EsDireccionLocal(new Uri("http://servidor-notas:5187")) && !RedLocal.EsDireccionLocal(new Uri("http://ejemplo.com")), "nombre de equipo local admitido sin HTTPS");
Comprobar(ServicioIdentidad.NormalizarCodigo("k7qf-2m9x 4ab3-ZZ99") == "K7QF2M9X4AB3ZZ99", "el código de configuración admite guiones, espacios y minúsculas");
Rechaza(() => Seguridad.ConfirmarClave("una contraseña larga", "otra contraseña larga"), "contraseñas distintas rechazadas por la lógica");
var revision = ServicioMatricula.Revisar([new FilaCruda(2, "12345678", "Ana", "Pérez", "Informática", "A", false),
    new FilaCruda(3, "12345678", "Ana", "Pérez", "Informática", "A", false), new FilaCruda(4, "x", "Luis", "Gil", "Informática", "A", false)]);
Comprobar(revision.Filas.Count == 1 && revision.Errores.Select(e => e.Fila).SequenceEqual([3, 4]), "errores de Excel identifican cada fila");
Comprobar(ServicioAcademico.PendientesDeCierre(new EstadoPlanilla(new Oferta(1, 1, "p", "c", "s", "m", 1, "d", 3, false, 2, 0, 3), 100m, 0)).Count == 0, "planilla completa y publicada lista para cierre");
Comprobar(ReglasAcademicas.Cedula("V-00.123.456") == "00123456", "cédula conserva ceros y normaliza prefijo nacional");
Comprobar(ReglasAcademicas.Cedula("v12.345.678") == "12345678" && Cedulas.Mostrar("12345678") == "V-12345678", "cédula con o sin V- se guarda en números y se muestra con V-");
try { ReglasAcademicas.Cedula("E-12345678"); Comprobar(false, "cédula con E- rechazada"); } catch (ReglaException) { Comprobar(true, "cédula con E- rechazada"); }
Comprobar(ReglasAcademicas.Correo(" Ana.Perez+v123@Gmail.COM ") == "ana.perez+v123@gmail.com" && ReglasAcademicas.Correo("") is null,
    "un correo válido se guarda limpio y en minúsculas; vacío es sin correo");
string[] correosMalos = ["ana,perez@gmail.com", "ana perez@gmail.com", "ana@gmail..com", ".ana@gmail.com", "ana.@gmail.com", "ana@-gmail.com",
    "ana@gmail.c0m", "ana@gmail", "ana@@gmail.com", "<ana>@gmail.com", "ana@gmail.com;beto@gmail.com", new string('a', 65) + "@gmail.com"];
Comprobar(correosMalos.All(c => { try { ReglasAcademicas.Correo(c); return false; } catch (ReglaException) { return true; } }),
    $"se rechazan {correosMalos.Length} correos mal escritos: comas, espacios, puntos dobles o al borde, dominio sin punto o con guion al borde");
Comprobar(FormatoSeccion.Generar(FormatoSeccion.Validar("{semestre:00}S-{carrera}-{seccion}"), "Ing. de Sistemas", "2603", 2, "D1") == "02S-2603-D1", "formato configurable arma 02S-2603-D1");
Comprobar(FormatoSeccion.Generar(FormatoSeccion.PorDefecto, "Informática", null, 0, "A") == "A", "el formato inicial deja la sección como se escribió");
Comprobar(FormatoSeccion.Generar("S{semestre}-{seccion}", "Informática", null, 3, "B") == "S3-B", "semestre sin ceros y texto fijo en el formato");
Rechaza(() => FormatoSeccion.Validar("{carrera}-{semestre}"), "el formato exige {seccion}");
Rechaza(() => FormatoSeccion.Validar("{curso}-{seccion}"), "un marcador desconocido se rechaza");
Rechaza(() => FormatoSeccion.Validar("{seccion}-{carrera"), "una llave suelta se rechaza");
Rechaza(() => FormatoSeccion.Generar("{carrera}-{seccion}", "Informática", null, 0, "A"), "el formato con {carrera} exige el código de la carrera");
Rechaza(() => FormatoSeccion.Generar("{semestre}-{seccion}", "Informática", "26", 0, "A"), "el formato con {semestre} exige el semestre");
Rechaza(() => FormatoSeccion.Semestre(25), "el semestre no pasa de 20");
Comprobar(ReglasAcademicas.LeerDecimal("15,25") == 15.25m, "entrada decimal española");
Rechaza(() => Calificacion.ValidarNota(0, fabrica), "cero no es una nota admitida en escala de 1 a 20");
Calificacion.ValidarNota(0, fabrica with { EscalaMinima = 0 });
Comprobar(true, "en escala de 0 a 20 el cero sí es nota");
Rechaza(() => Calificacion.ValidarNota(20.01m, fabrica), "nota superior a veinte rechazada");
Rechaza(() => Calificacion.ValidarNota(12.333m, fabrica), "no se truncan decimales de notas");
Rechaza(() => ReglasAcademicas.ValidarPeso(0), "peso cero rechazado");
Rechaza(() => ReglasAcademicas.ValidarPeso(100.01m), "peso excesivo rechazado");
Rechaza(() => ReglasAcademicas.ValidarPeso(0.001m), "no se truncan decimales de pesos");
Rechaza(() => Seguridad.ValidarClave("corta"), "contraseña corta rechazada");
var clavePrueba = Seguridad.NuevoToken();
var hashUno = Seguridad.DerivarClave(clavePrueba);
var hashDos = Seguridad.DerivarClave(clavePrueba);
Comprobar(hashUno != hashDos, "sal individual de contraseñas");
Comprobar(Seguridad.VerificarClave(clavePrueba, hashUno), "contraseña correcta verifica");
Comprobar(!Seguridad.VerificarClave("otra contraseña", hashUno), "contraseña incorrecta rechazada");
Comprobar(!Seguridad.VerificarClave(null, hashUno), "clave nula no provoca excepción");
Comprobar(!Seguridad.VerificarClave(clavePrueba, "hash corrupto"), "hash inválido no provoca excepción");

await ComprobacionesArquitectura.Ejecutar(Comprobar);

if (Environment.GetEnvironmentVariable("ACADEMICO_PRUEBAS") != "1")
{
    Console.WriteLine($"{comprobaciones} comprobaciones de dominio y arquitectura. Para integración: ACADEMICO_PRUEBAS=1 y una BD local terminada en _pruebas.");
    return;
}
var cadena = Environment.GetEnvironmentVariable("ACADEMICO_BD") ?? "";
var opciones = new MySqlConnector.MySqlConnectionStringBuilder(cadena);
if (!opciones.Database.EndsWith("_pruebas", StringComparison.Ordinal) || opciones.Server != "127.0.0.1")
    throw new InvalidOperationException("Las pruebas de integración solo admiten una base local terminada en _pruebas.");
var direccion = Environment.GetEnvironmentVariable("ACADEMICO_URL_SERVIDOR") ?? "http://127.0.0.1:5187";
if (!new Uri(direccion).IsLoopback) throw new InvalidOperationException("Las pruebas HTTP solo admiten loopback.");
var bd = new BaseDatos(cadena);
// Los ajustes que la aplicación nunca hace (como vencer un código) requieren la cuenta de mantenimiento.
var mantenimiento = new BaseDatos(Environment.GetEnvironmentVariable("ACADEMICO_BD_MANTENIMIENTO") ?? cadena);
using var anonimo = new HttpClient { BaseAddress = new Uri(direccion), Timeout = TimeSpan.FromSeconds(20) };
using var administrador = new HttpClient { BaseAddress = new Uri(direccion), Timeout = TimeSpan.FromSeconds(20) };
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

async Task<T> Enviar<T>(HttpClient cliente, HttpMethod metodo, string ruta, object? cuerpo = null)
{
    using var solicitud = new HttpRequestMessage(metodo, ruta);
    if (cuerpo is not null) solicitud.Content = JsonContent.Create(cuerpo);
    using var respuesta = await cliente.SendAsync(solicitud);
    if (!respuesta.IsSuccessStatusCode) throw new InvalidOperationException($"{metodo} {ruta}: {(int)respuesta.StatusCode}, {await respuesta.Content.ReadAsStringAsync()}");
    return (await respuesta.Content.ReadFromJsonAsync<T>(json))!;
}
async Task EsperarEstado(HttpClient cliente, HttpMethod metodo, string ruta, object? cuerpo, HttpStatusCode esperado, string nombre)
{
    using var solicitud = new HttpRequestMessage(metodo, ruta);
    if (cuerpo is not null) solicitud.Content = JsonContent.Create(cuerpo);
    using var respuesta = await cliente.SendAsync(solicitud);
    Comprobar(respuesta.StatusCode == esperado, $"{nombre} ({(int)respuesta.StatusCode})");
}
async Task<HttpClient> Entrar(string cedula, string clave)
{
    var sesion = await Enviar<Sesion>(anonimo, HttpMethod.Post, "acceso", new Acceso(cedula, clave));
    var cliente = new HttpClient { BaseAddress = new Uri(direccion), Timeout = TimeSpan.FromSeconds(20) };
    cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sesion.Token);
    return cliente;
}
var accesoAdmin = await Enviar<Sesion>(anonimo, HttpMethod.Post, "acceso", new Acceso(
    Environment.GetEnvironmentVariable("ACADEMICO_ADMIN_CEDULA")!, Environment.GetEnvironmentVariable("ACADEMICO_ADMIN_CLAVE")!));
administrador.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accesoAdmin.Token);
await EsperarEstado(anonimo, HttpMethod.Get, "usuarios", null, HttpStatusCode.Unauthorized, "sin sesión no se leen usuarios");
Comprobar(!(await Enviar<EstadoConfiguracion>(anonimo, HttpMethod.Get, "configuracion")).Pendiente, "con administración creada no se ofrece la configuración inicial");
await EsperarEstado(anonimo, HttpMethod.Post, "configuracion", new ConfiguracionInicial("AAAA-BBBB-CCCC-DDDD", "12345679", "Intruso", "Prueba", clavePrueba),
    HttpStatusCode.BadRequest, "un código de configuración inventado no crea administración");
await EsperarEstado(anonimo, HttpMethod.Post, "activar", new ActivarCuenta("90000001", "incorrecto", clavePrueba), HttpStatusCode.BadRequest, "código inválido rechazado");
var semilla = Random.Shared.Next(10_000_000, 89_000_000);
var sufijo = Guid.NewGuid().ToString("N")[..8];
var cedulaDocente = (semilla + 1).ToString();
var cedulaOtro = (semilla + 2).ToString();
var cedulaEstudiante = (semilla + 3).ToString();
var cedulaSegunda = (semilla + 4).ToString();
async Task<long> Crear(string cedula, string nombres, string rol)
    => await Enviar<long>(administrador, HttpMethod.Post, "usuarios", new NuevaPersona(cedula, nombres, "Prueba", rol));
var docenteId = await Crear(cedulaDocente, "Docente uno", Roles.Docente);
var otroId = await Crear(cedulaOtro, "Docente dos", Roles.Docente);
var estudianteId = await Crear(cedulaEstudiante, "Estudiante uno", Roles.Estudiante);
var segundaId = await Crear(cedulaSegunda, "Estudiante dos", Roles.Estudiante);
async Task<CodigoAcceso> Codigo(long id) => await Enviar<CodigoAcceso>(administrador, HttpMethod.Post, $"usuarios/{id}/codigo", new { });
async Task Activar(long id, string cedula)
{
    var codigo = await Codigo(id);
    await EsperarEstado(anonimo, HttpMethod.Post, "activar", new ActivarCuenta(cedula, codigo.Codigo, clavePrueba), HttpStatusCode.NoContent, "activar cuenta por código");
    await EsperarEstado(anonimo, HttpMethod.Post, "activar", new ActivarCuenta(cedula, codigo.Codigo, clavePrueba), HttpStatusCode.BadRequest, "código usado no se reutiliza");
}
await Activar(docenteId, cedulaDocente);
await Activar(otroId, cedulaOtro);
await Activar(estudianteId, cedulaEstudiante);
using var docente = await Entrar(cedulaDocente, clavePrueba);
using var otro = await Entrar(cedulaOtro, clavePrueba);
using var estudiante = await Entrar(cedulaEstudiante, clavePrueba);
await EsperarEstado(docente, HttpMethod.Get, "usuarios", null, HttpStatusCode.Forbidden, "docente no enumera cuentas");
await EsperarEstado(estudiante, HttpMethod.Get, "ofertas", null, HttpStatusCode.Forbidden, "estudiante no enumera asignaciones");
await EsperarEstado(estudiante, HttpMethod.Post, "usuarios", new NuevaPersona("12345678", "Otra", "Persona", Roles.Administrador), HttpStatusCode.Forbidden, "estudiante no crea administradores");
await EsperarEstado(administrador, HttpMethod.Post, "usuarios", new NuevaPersona(cedulaDocente, "Otra", "Persona", Roles.Docente), HttpStatusCode.Conflict, "cédula duplicada protegida por SQL");
var periodoId = await Enviar<long>(administrador, HttpMethod.Post, "catalogos/periodos", new Referencia(0, "prueba " + sufijo));
var carreraNombre = "Carrera de prueba " + sufijo;
var carreraId = await Enviar<long>(administrador, HttpMethod.Post, "catalogos/carreras", new Referencia(0, carreraNombre));
// Las reglas de fábrica, por si una corrida anterior quedó a medias.
await Enviar<ReglasCalificacion>(administrador, HttpMethod.Put, "reglas", fabrica);
async Task<long> MateriaEn(long carrera, string codigo, string nombre, int semestre = 1, int uc = 3) =>
    await Enviar<long>(administrador, HttpMethod.Post, $"materias?carrera={carrera}&semestre={semestre}", new NuevaMateria(codigo, nombre, uc));
await EsperarEstado(administrador, HttpMethod.Post, "catalogos/materias", new Referencia(0, "Sin código " + sufijo), HttpStatusCode.BadRequest,
    "una materia ya no se crea sin código por el catálogo genérico");
var materiaId = await MateriaEn(carreraId, $"M1-{sufijo}", "Materia uno " + sufijo);
var otraMateriaId = await MateriaEn(carreraId, $"M2-{sufijo}", "Materia dos " + sufijo);
// Para las pruebas de otros períodos: el estudiante uno aprueba la materia uno, y lo aprobado no se vuelve a inscribir.
var materiaTres = await MateriaEn(carreraId, $"M3-{sufijo}", "Materia tres " + sufijo);
var seccionId = await Enviar<long>(administrador, HttpMethod.Post, "secciones", new NuevaSeccion(carreraId, "A"));
var ofertaId = await Enviar<long>(administrador, HttpMethod.Post, "ofertas", new NuevaOferta(periodoId, seccionId, materiaId, docenteId));
var otraOfertaId = await Enviar<long>(administrador, HttpMethod.Post, "ofertas", new NuevaOferta(periodoId, seccionId, otraMateriaId, otroId));
await EsperarEstado(otro, HttpMethod.Get, $"ofertas/{ofertaId}", null, HttpStatusCode.Forbidden, "docente no lee grupo ajeno");
await EsperarEstado(estudiante, HttpMethod.Get, $"ofertas/{ofertaId}", null, HttpStatusCode.Forbidden, "estudiante no lee planilla docente");
async Task<Planilla> Plan(HttpClient quien, long id) => await Enviar<Planilla>(quien, HttpMethod.Get, $"ofertas/{id}");
var plan = await Plan(docente, ofertaId);
var filaUno = new FilaMatricula(cedulaEstudiante, "Estudiante uno", "Prueba", carreraNombre, "A");
var filaDos = new FilaMatricula(cedulaSegunda, "Estudiante dos", "Prueba", carreraNombre, "A");
async Task<VistaImportacion> LeerExcel(bool formula, bool duplicada)
{
    using var libro = new XLWorkbook();
    var hoja = libro.AddWorksheet("matricula");
    string[] encabezados = ["cédula", "nombres", "apellidos", "carrera", "sección"];
    string[] valores = [filaUno.Cedula, filaUno.Nombres, filaUno.Apellidos, filaUno.Carrera, filaUno.Seccion];
    for (var i = 0; i < 5; i++) { hoja.Cell(1, i + 1).Value = encabezados[i]; hoja.Cell(2, i + 1).Value = valores[i]; }
    if (formula) hoja.Cell(2, 2).FormulaA1 = "1+1";
    if (duplicada) for (var i = 0; i < 5; i++) hoja.Cell(3, i + 1).Value = valores[i];
    using var memoria = new MemoryStream();
    libro.SaveAs(memoria);
    using var solicitud = new HttpRequestMessage(HttpMethod.Post, $"ofertas/{ofertaId}/leer-excel") { Content = new ByteArrayContent(memoria.ToArray()) };
    using var respuesta = await docente.SendAsync(solicitud);
    respuesta.EnsureSuccessStatusCode();
    return (await respuesta.Content.ReadFromJsonAsync<VistaImportacion>(json))!;
}
var vistaExcel = await LeerExcel(false, false);
Comprobar(vistaExcel.Errores.Count == 0 && vistaExcel.Filas.Single() == filaUno, "Excel se lee con encabezados acentuados y valores exactos");
Comprobar((await LeerExcel(true, false)).Errores.Count > 0, "Excel con fórmulas se rechaza sin ejecutarlas");
Comprobar((await LeerExcel(false, true)).Errores.Count > 0, "Excel con cédulas duplicadas informa el conflicto");
{
    // La plantilla descargada trae logotipo e indicaciones arriba; debe leerse igual.
    var plantilla = await docente.GetByteArrayAsync("plantilla-matricula");
    Directory.CreateDirectory(".evidencia"); File.WriteAllBytes(Path.Combine(".evidencia", "plantilla-matricula.xlsx"), plantilla);
    using var libro = new XLWorkbook(new MemoryStream(plantilla));
    var hoja = libro.Worksheet(1);
    Comprobar(hoja.Pictures.Count == 1, "la plantilla de matrícula lleva el logotipo de Mérito");
    string[] valores = [filaUno.Cedula, filaUno.Nombres, filaUno.Apellidos, filaUno.Carrera, filaUno.Seccion];
    for (var i = 0; i < 5; i++) hoja.Cell(5, i + 1).Value = valores[i];
    hoja.Cell(6, 1).Value = "12";
    using var memoria = new MemoryStream();
    libro.SaveAs(memoria);
    using var solicitud = new HttpRequestMessage(HttpMethod.Post, $"ofertas/{ofertaId}/leer-excel") { Content = new ByteArrayContent(memoria.ToArray()) };
    using var respuesta = await docente.SendAsync(solicitud);
    var leida = (await respuesta.Content.ReadFromJsonAsync<VistaImportacion>(json))!;
    Comprobar(leida.Filas.SingleOrDefault() == filaUno && leida.Errores.Count > 0 && leida.Errores.All(e => e.Fila == 6),
        "la plantilla con logotipo se lee y los errores citan la fila real de Excel");
}
var propuesta = await Enviar<ResultadoImportacion>(docente, HttpMethod.Post, $"ofertas/{ofertaId}/importar", new ImportarMatricula(plan.Oferta.Revision, vistaExcel.Filas));
Comprobar(propuesta.Estado == "pendiente", "matrícula del docente requiere confirmación administrativa");
plan = await Plan(docente, ofertaId);
var evaluacionUno = await Enviar<long>(docente, HttpMethod.Post, $"ofertas/{ofertaId}/evaluaciones", new NuevaEvaluacion("Parcial", 15, plan.Oferta.Revision));
plan = await Plan(docente, ofertaId);
await EsperarEstado(docente, HttpMethod.Put, $"ofertas/{ofertaId}/notas", new GuardarNotas(plan.Oferta.Revision, [new Nota(evaluacionUno, estudianteId, 10)]), HttpStatusCode.Forbidden, "matrícula pendiente no admite notas");
await EsperarEstado(docente, HttpMethod.Post, $"ofertas/{ofertaId}/confirmar", new ConfirmarMatricula(plan.Oferta.Revision, plan.Estudiantes.Select(a => a.InscripcionId).ToList()), HttpStatusCode.Forbidden, "docente no confirma su propia propuesta");
await Enviar<int>(administrador, HttpMethod.Post, $"ofertas/{ofertaId}/confirmar", new ConfirmarMatricula(plan.Oferta.Revision, plan.Estudiantes.Select(a => a.InscripcionId).ToList()));
plan = await Plan(docente, ofertaId);
await Enviar<ResultadoImportacion>(administrador, HttpMethod.Post, $"ofertas/{ofertaId}/importar", new ImportarMatricula(plan.Oferta.Revision, [filaDos]));
plan = await Plan(docente, ofertaId);
var evaluacionDos = await Enviar<long>(docente, HttpMethod.Post, $"ofertas/{ofertaId}/evaluaciones", new NuevaEvaluacion("Final", 85, plan.Oferta.Revision));
plan = await Plan(docente, ofertaId);
await EsperarEstado(docente, HttpMethod.Post, $"ofertas/{ofertaId}/evaluaciones", new NuevaEvaluacion("Extra", 1, plan.Oferta.Revision), HttpStatusCode.BadRequest, "suma de pesos superior a cien rechazada");
await EsperarEstado(otro, HttpMethod.Put, $"ofertas/{ofertaId}/notas", new GuardarNotas(plan.Oferta.Revision, [new Nota(evaluacionUno, estudianteId, 20)]), HttpStatusCode.Forbidden, "docente no modifica notas ajenas");
await EsperarEstado(docente, HttpMethod.Put, $"ofertas/{ofertaId}/notas", new GuardarNotas(plan.Oferta.Revision, [new Nota(evaluacionUno, estudianteId, 20.01m)]), HttpStatusCode.BadRequest, "API rechaza nota fuera de escala");
var revisionVieja = plan.Oferta.Revision;
await Enviar<int>(docente, HttpMethod.Put, $"ofertas/{ofertaId}/notas", new GuardarNotas(revisionVieja, [new Nota(evaluacionUno, estudianteId, 10), new Nota(evaluacionUno, segundaId, 18)]));
await EsperarEstado(docente, HttpMethod.Put, $"ofertas/{ofertaId}/notas", new GuardarNotas(revisionVieja, [new Nota(evaluacionUno, estudianteId, 19)]), HttpStatusCode.Conflict, "versión obsoleta no sobrescribe calificaciones");
var registro = await Enviar<RecordAcademico>(estudiante, HttpMethod.Get, "record");
Comprobar(registro.Materias.Count == 0, "borrador invisible para el estudiante");
plan = await Plan(docente, ofertaId);
var publicacion = await Enviar<long>(docente, HttpMethod.Post, $"ofertas/{ofertaId}/publicar", new RevisionEsperada(plan.Oferta.Revision));
var repetida = await Enviar<long>(docente, HttpMethod.Post, $"ofertas/{ofertaId}/publicar", new RevisionEsperada(plan.Oferta.Revision));
Comprobar(publicacion == repetida, "reintento de publicación no duplica versiones");
registro = await Enviar<RecordAcademico>(estudiante, HttpMethod.Get, "record");
Comprobar(registro.Materias.Single().Evaluaciones[0].Nota == 10, "estudiante ve su nota y no la del compañero");
Comprobar(registro.Materias.Single().Acumulado == 1.5m, "récord conserva aporte decimal exacto");
Comprobar(registro.Materias.Single().Evaluaciones[0].AportePorcentual == 7.5m, "récord muestra porcentaje obtenido");
Comprobar(registro.Materias.Single().NotaFinal is null, "una publicación parcial no inventa nota final");
await Enviar<int>(docente, HttpMethod.Put, $"ofertas/{ofertaId}/notas", new GuardarNotas(plan.Oferta.Revision, [new Nota(evaluacionUno, estudianteId, 12)]));
registro = await Enviar<RecordAcademico>(estudiante, HttpMethod.Get, "record");
Comprobar(registro.Materias.Single().Evaluaciones[0].Nota == 10, "corregir borrador no altera publicación anterior");
var cierre = await Enviar<ResumenCierre>(administrador, HttpMethod.Get, $"periodos/{periodoId}/cierre");
Comprobar(!cierre.PuedeCerrar && cierre.Ofertas.Any(o => o.Pendientes.Count > 0), "cierre detecta carga y publicación pendientes");
await EsperarEstado(administrador, HttpMethod.Post, $"periodos/{periodoId}/cerrar", new { }, HttpStatusCode.Conflict, "no cierra un período incompleto");

var otraPlanilla = await Plan(otro, otraOfertaId);
await Enviar<ResultadoImportacion>(administrador, HttpMethod.Post, $"ofertas/{otraOfertaId}/importar", new ImportarMatricula(otraPlanilla.Oferta.Revision, [filaUno]));
otraPlanilla = await Plan(otro, otraOfertaId);
var evaluacionAjena = await Enviar<long>(otro, HttpMethod.Post, $"ofertas/{otraOfertaId}/evaluaciones", new NuevaEvaluacion("Única", 100, otraPlanilla.Oferta.Revision));
plan = await Plan(docente, ofertaId);
await EsperarEstado(docente, HttpMethod.Put, $"ofertas/{ofertaId}/notas", new GuardarNotas(plan.Oferta.Revision, [new Nota(evaluacionAjena, estudianteId, 20)]), HttpStatusCode.Forbidden, "evaluación de otra materia no entra en la planilla");
var inscripcionAjena = otraPlanilla.Estudiantes.Single().InscripcionId;
try
{
    await bd.Directo(d => d.Ejecutar("INSERT INTO notas (oferta_id,evaluacion_id,inscripcion_id,valor) VALUES (@oferta,@evaluacion,@inscripcion,20)",
        ("@oferta", ofertaId), ("@evaluacion", evaluacionUno), ("@inscripcion", inscripcionAjena)));
    throw new InvalidOperationException("SQL permitió cruzar ofertas.");
}
catch (MySqlConnector.MySqlException error) when (error.Number == 1452)
{ Comprobar(true, "SQL impide cruzar una inscripción con una evaluación de otra oferta"); }
plan = await Plan(docente, ofertaId);
await Enviar<int>(docente, HttpMethod.Put, $"ofertas/{ofertaId}/notas", new GuardarNotas(plan.Oferta.Revision,
    [new Nota(evaluacionDos, estudianteId, 16), new Nota(evaluacionDos, segundaId, 20)]));
plan = await Plan(docente, ofertaId);
await Enviar<long>(docente, HttpMethod.Post, $"ofertas/{ofertaId}/publicar", new RevisionEsperada(plan.Oferta.Revision));
otraPlanilla = await Plan(otro, otraOfertaId);
await Enviar<int>(otro, HttpMethod.Put, $"ofertas/{otraOfertaId}/notas", new GuardarNotas(otraPlanilla.Oferta.Revision, [new Nota(evaluacionAjena, estudianteId, 11)]));
otraPlanilla = await Plan(otro, otraOfertaId);
await Enviar<long>(otro, HttpMethod.Post, $"ofertas/{otraOfertaId}/publicar", new RevisionEsperada(otraPlanilla.Oferta.Revision));
cierre = await Enviar<ResumenCierre>(administrador, HttpMethod.Get, $"periodos/{periodoId}/cierre");
Comprobar(cierre.PuedeCerrar, "cierre permite un período completo y publicado");
await EsperarEstado(docente, HttpMethod.Post, $"periodos/{periodoId}/cerrar", new { }, HttpStatusCode.Forbidden, "docente no cierra períodos");
await Enviar<ResumenCierre>(administrador, HttpMethod.Post, $"periodos/{periodoId}/cerrar", new { });
await EsperarEstado(docente, HttpMethod.Put, $"ofertas/{ofertaId}/notas", new GuardarNotas(plan.Oferta.Revision, [new Nota(evaluacionUno, estudianteId, 20)]), HttpStatusCode.Conflict, "período cerrado bloquea cambios");
registro = await Enviar<RecordAcademico>(estudiante, HttpMethod.Get, "record");
Comprobar(registro.Materias.Single(m => m.OfertaId == ofertaId).NotaFinal == 15, "nota final ponderada y redondeada correctamente");
Comprobar(registro.Promedio == 13m && registro.Indice == Indices.Ponderado, "índice ponderado por UC con dos materias de 3 UC: (15x3 + 11x3) / 6 = 13");
Comprobar(registro.UcAprobadas == 6 && registro.Materias.All(m => m.Uc == 3 && m.Codigo.StartsWith('M')), "el récord trae código, UC y UC aprobadas");
var codigoReinicio = await Codigo(estudianteId);
var claveNueva = Seguridad.NuevoToken();
await EsperarEstado(anonimo, HttpMethod.Post, "activar", new ActivarCuenta(cedulaEstudiante, codigoReinicio.Codigo, claveNueva), HttpStatusCode.NoContent, "restablecimiento sin correo");
await EsperarEstado(estudiante, HttpMethod.Get, "record", null, HttpStatusCode.Unauthorized, "restablecer contraseña revoca las sesiones anteriores");
using var nuevoAcceso = await Entrar(cedulaEstudiante, claveNueva);
Comprobar((await Enviar<RecordAcademico>(nuevoAcceso, HttpMethod.Get, "record")).Materias.Count == 2, "historial persiste después de recuperar acceso");
// Operaciones reales simultáneas contra una asignación independiente.
var periodoCarrera = await Enviar<long>(administrador, HttpMethod.Post, "catalogos/periodos", new Referencia(0, "Concurrencia " + sufijo));
var ofertaCarrera = await Enviar<long>(administrador, HttpMethod.Post, "ofertas", new NuevaOferta(periodoCarrera, seccionId, materiaTres, docenteId));
var inicial = await Plan(docente, ofertaCarrera);
await Enviar<ResultadoImportacion>(administrador, HttpMethod.Post, $"ofertas/{ofertaCarrera}/importar", new ImportarMatricula(inicial.Oferta.Revision, [filaUno]));
inicial = await Plan(docente, ofertaCarrera);
var evaluacionCarrera = await Enviar<long>(docente, HttpMethod.Post, $"ofertas/{ofertaCarrera}/evaluaciones", new NuevaEvaluacion("Única", 100, inicial.Oferta.Revision));
inicial = await Plan(docente, ofertaCarrera);
async Task<HttpStatusCode> GuardadoConcurrente(decimal nota)
{
    using var respuesta = await docente.PutAsJsonAsync($"ofertas/{ofertaCarrera}/notas", new GuardarNotas(inicial.Oferta.Revision, [new Nota(evaluacionCarrera, estudianteId, nota)]));
    return respuesta.StatusCode;
}
var resultados = await Task.WhenAll(GuardadoConcurrente(14), GuardadoConcurrente(18));
Comprobar(resultados.Count(e => e == HttpStatusCode.OK) == 1 && resultados.Count(e => e == HttpStatusCode.Conflict) == 1,
    "dos guardados simultáneos conservan un ganador y rechazan la revisión obsoleta");
inicial = await Plan(docente, ofertaCarrera);
Comprobar(inicial.Notas.Single().Valor is 14m or 18m, "la nota concurrente queda completa");
var revisionAsignada = await Enviar<int>(administrador, HttpMethod.Put, $"ofertas/{ofertaCarrera}/docente", new CambioDocente(otroId, inicial.Oferta.Revision));
await EsperarEstado(docente, HttpMethod.Get, $"ofertas/{ofertaCarrera}", null, HttpStatusCode.Forbidden, "reasignar retira inmediatamente el acceso del docente anterior");
var reasignada = await Plan(otro, ofertaCarrera);
Comprobar(reasignada.Oferta.Revision == revisionAsignada && reasignada.Notas.Single().Valor == inicial.Notas.Single().Valor,
    "el nuevo docente recibe las notas conservadas y una revisión nueva");
await EsperarEstado(docente, HttpMethod.Get, "auditoria", null, HttpStatusCode.Forbidden, "auditoría reservada a administración");
var reasignacion = (await Enviar<Pagina<EventoAuditoria>>(administrador, HttpMethod.Get, "auditoria?buscar=docente_reasignado")).Elementos.FirstOrDefault(a => a.Accion == "docente_reasignado");
Comprobar(reasignacion?.Detalle == $"Cambió el docente de Materia tres {sufijo}, sección A, Concurrencia {sufijo}", "el historial cuenta la reasignación en español, con materia, sección y período");
var historial = await Enviar<Pagina<EventoAuditoria>>(administrador, HttpMethod.Get, "auditoria?cantidad=200");
Comprobar(historial.Elementos.Count > 0 && historial.Elementos.All(a => a.Accion is not ("inicio_sesion" or "codigo_correo_enviado" or "prueba_correo")),
    "el historial de cambios no lista inicios de sesión ni envíos de códigos");
Comprobar((await Enviar<Pagina<EventoAuditoria>>(administrador, HttpMethod.Get, $"auditoria?buscar={Uri.EscapeDataString("Materia tres " + sufijo)}")).Elementos.Any(a => a.Accion == "docente_reasignado"),
    "el historial se busca por el nombre de la materia");
await Enviar<bool>(administrador, HttpMethod.Put, $"usuarios/{otroId}/estado", new EstadoCuenta(false));
await EsperarEstado(otro, HttpMethod.Get, "ofertas", null, HttpStatusCode.Unauthorized, "desactivar cuenta revoca su sesión");
await Enviar<bool>(administrador, HttpMethod.Put, $"usuarios/{otroId}/estado", new EstadoCuenta(true));
await EsperarEstado(otro, HttpMethod.Get, "ofertas", null, HttpStatusCode.Unauthorized, "reactivar no recupera un token revocado");
using var reactivado = await Entrar(cedulaOtro, clavePrueba);
Comprobar((await Plan(reactivado, ofertaCarrera)).Notas.Count == 1, "reactivación conserva el historial académico");
// La segunda fila falla después de insertar la primera: todo debe revertirse.
var cedulaRollback = "0" + semilla;
var antesRollback = await Plan(reactivado, ofertaCarrera);
await EsperarEstado(administrador, HttpMethod.Post, $"ofertas/{ofertaCarrera}/importar",
    new ImportarMatricula(antesRollback.Oferta.Revision,
        [new FilaMatricula(cedulaRollback, "Temporal", "Rollback", carreraNombre, "A"), filaUno with { Nombres = "Identidad distinta" }]),
    HttpStatusCode.Conflict, "una identidad conflictiva interrumpe la importación");
var despuesRollback = await Plan(reactivado, ofertaCarrera);
Comprobar(despuesRollback.Oferta.Revision == antesRollback.Oferta.Revision && despuesRollback.Estudiantes.Count == antesRollback.Estudiantes.Count,
    "rollback conserva matrícula y revisión anteriores");
Comprobar(await bd.Directo(d => d.Escalar<long>("SELECT COUNT(*) FROM usuarios WHERE cedula=@cedula", ("@cedula", cedulaRollback))) == 0,
    "rollback elimina también la cuenta creada por la primera fila");
var codigoVencido = await Codigo(segundaId);
await mantenimiento.Directo(d => d.Ejecutar("UPDATE codigos_acceso SET vence_en=UTC_TIMESTAMP(6)-INTERVAL 1 SECOND WHERE usuario_id=@id", ("@id", segundaId)));
await EsperarEstado(anonimo, HttpMethod.Post, "activar", new ActivarCuenta(cedulaSegunda, codigoVencido.Codigo, clavePrueba), HttpStatusCode.BadRequest, "código vencido no permite activar cuenta");
// Cierre del período disputado con guardado y publicación simultáneos.
async Task<(long Periodo, long Oferta, long Evaluacion)> PlanillaLista(string nombre)
{
    var periodo = await Enviar<long>(administrador, HttpMethod.Post, "catalogos/periodos", new Referencia(0, nombre + " " + sufijo));
    // Una materia propia por planilla: si el período anterior cerró, el estudiante ya la aprobó.
    var materia = await MateriaEn(carreraId, $"P{periodo}-{sufijo}", nombre + " " + sufijo);
    var oferta = await Enviar<long>(administrador, HttpMethod.Post, "ofertas", new NuevaOferta(periodo, seccionId, materia, docenteId));
    var planilla = await Plan(docente, oferta);
    await Enviar<ResultadoImportacion>(administrador, HttpMethod.Post, $"ofertas/{oferta}/importar", new ImportarMatricula(planilla.Oferta.Revision, [filaUno]));
    planilla = await Plan(docente, oferta);
    var evaluacion = await Enviar<long>(docente, HttpMethod.Post, $"ofertas/{oferta}/evaluaciones", new NuevaEvaluacion("Única", 100, planilla.Oferta.Revision));
    planilla = await Plan(docente, oferta);
    await Enviar<int>(docente, HttpMethod.Put, $"ofertas/{oferta}/notas", new GuardarNotas(planilla.Oferta.Revision, [new Nota(evaluacion, estudianteId, 15)]));
    return (periodo, oferta, evaluacion);
}
async Task<HttpStatusCode> Estado(HttpClient cliente, HttpMethod metodo, string ruta, object cuerpo)
{
    using var solicitud = new HttpRequestMessage(metodo, ruta) { Content = JsonContent.Create(cuerpo) };
    using var respuesta = await cliente.SendAsync(solicitud);
    return respuesta.StatusCode;
}
var (periodoPublicar, ofertaPublicar, _) = await PlanillaLista("Cierre y publicación");
var revisionPublicar = (await Plan(docente, ofertaPublicar)).Oferta.Revision;
var carreraPublicar = await Task.WhenAll(
    Estado(docente, HttpMethod.Post, $"ofertas/{ofertaPublicar}/publicar", new RevisionEsperada(revisionPublicar)),
    Estado(administrador, HttpMethod.Post, $"periodos/{periodoPublicar}/cerrar", new { }));
var tras = (await Enviar<Pagina<Oferta>>(administrador, HttpMethod.Get, "ofertas?cantidad=200&buscar=" + Uri.EscapeDataString("Cierre y publicación " + sufijo))).Elementos.Single(o => o.Id == ofertaPublicar);
Comprobar(carreraPublicar[0] == HttpStatusCode.OK && (!tras.Cerrado || tras.RevisionPublicada == tras.Revision),
    "publicar y cerrar a la vez nunca deja cerrada una versión sin publicar");

var (periodoGuardar, ofertaGuardar, evaluacionGuardar) = await PlanillaLista("Cierre y guardado");
var lista = await Plan(docente, ofertaGuardar);
await Enviar<long>(docente, HttpMethod.Post, $"ofertas/{ofertaGuardar}/publicar", new RevisionEsperada(lista.Oferta.Revision));
var carreraGuardar = await Task.WhenAll(
    Estado(docente, HttpMethod.Put, $"ofertas/{ofertaGuardar}/notas", new GuardarNotas(lista.Oferta.Revision, [new Nota(evaluacionGuardar, estudianteId, 19)])),
    Estado(administrador, HttpMethod.Post, $"periodos/{periodoGuardar}/cerrar", new { }));
var final = await Plan(docente, ofertaGuardar);
Comprobar(carreraGuardar.Count(e => e == HttpStatusCode.OK) == 1 && carreraGuardar.Count(e => e == HttpStatusCode.Conflict) == 1,
    "guardar y cerrar a la vez: exactamente una operación gana y la otra se rechaza");
Comprobar(!final.Oferta.Cerrado || (final.Notas.Single().Valor == 15 && final.Oferta.RevisionPublicada == final.Oferta.Revision),
    "si el período se cerró, la nota cerrada es la publicada y no la del guardado tardío");

// Paginación y búsqueda en el servidor.
var unaCuenta = await Enviar<Pagina<Persona>>(administrador, HttpMethod.Get, $"usuarios?buscar={cedulaDocente}");
Comprobar(unaCuenta.Total == 1 && unaCuenta.Elementos.Single().Id == docenteId, "buscar por cédula en el servidor encuentra una sola cuenta");
var primera = await Enviar<Pagina<Persona>>(administrador, HttpMethod.Get, "usuarios?desde=0&cantidad=5");
var segunda = await Enviar<Pagina<Persona>>(administrador, HttpMethod.Get, "usuarios?desde=5&cantidad=5");
Comprobar(primera.Elementos.Count == 5 && primera.Total > 10 && !primera.Elementos.Select(p => p.Id).Intersect(segunda.Elementos.Select(p => p.Id)).Any(),
    "páginas consecutivas no repiten registros y el total supera la página");
Comprobar((await Enviar<Pagina<Persona>>(administrador, HttpMethod.Get, "usuarios?buscar=%25")).Total == 0, "un % escrito por el usuario no actúa como comodín");
Comprobar((await Enviar<Pagina<Persona>>(administrador, HttpMethod.Get, $"usuarios?desde={primera.Total + 10}")).Elementos.Count == 0, "una página más allá del final llega vacía");
await EsperarEstado(administrador, HttpMethod.Get, "usuarios?cantidad=500", null, HttpStatusCode.BadRequest, "una página de más de 200 registros se rechaza");
Comprobar((await Enviar<List<Persona>>(administrador, HttpMethod.Get, "docentes")).All(p => p.Rol == Roles.Docente && p.Activo), "la lista de docentes trae solo docentes activos");
var paginaDocente = await Enviar<Pagina<Oferta>>(docente, HttpMethod.Get, "ofertas");
Comprobar(paginaDocente.Elementos.All(o => o.DocenteId == docenteId), "la paginación conserva el filtro por docente");

// Carga académica: materias por carrera, asignación por sección, copia entre períodos, correcciones y retiro.
var otraCarrera = await Enviar<long>(administrador, HttpMethod.Post, "catalogos/carreras", new Referencia(0, "Otra carrera " + sufijo));
var seccionOtra = await Enviar<long>(administrador, HttpMethod.Post, "secciones", new NuevaSeccion(otraCarrera, "B"));
var materiaPropia = await MateriaEn(carreraId, $"PRO-{sufijo}", "Propia " + sufijo);
// Ciclo básico: una sola materia en el pensum de dos carreras.
var materiaComun = await MateriaEn(carreraId, $"CB-{sufijo}", "Ciclo básico " + sufijo);
await Enviar<int>(administrador, HttpMethod.Put, $"carreras/{otraCarrera}/pensum", new EnPensum(materiaComun, 1));
var materiaAjena = await MateriaEn(otraCarrera, $"AJE-{sufijo}", "Ajena " + sufijo);
var periodoBloque = await Enviar<long>(administrador, HttpMethod.Post, "catalogos/periodos", new Referencia(0, "Bloque " + sufijo));
await EsperarEstado(administrador, HttpMethod.Post, "ofertas", new NuevaOferta(periodoBloque, seccionId, materiaAjena, docenteId), HttpStatusCode.BadRequest,
    "una materia de otra carrera no se asigna a la sección");
var docenteTemporal = await Crear((semilla + 7).ToString(), "Docente temporal", Roles.Docente);
var bloque = await Enviar<ResultadoCarga>(administrador, HttpMethod.Post, "ofertas/lote", new CargaEnBloque(periodoBloque, seccionId,
    [new MateriaDocente(materiaPropia, docenteId), new MateriaDocente(materiaComun, docenteTemporal)]));
Comprobar(bloque.Creadas == 2 && bloque.Existentes == 0, "asignar por sección crea varias materias en una operación");
var repetido = await Enviar<ResultadoCarga>(administrador, HttpMethod.Post, "ofertas/lote", new CargaEnBloque(periodoBloque, seccionId, [new MateriaDocente(materiaPropia, otroId)]));
Comprobar(repetido.Creadas == 0 && repetido.Existentes == 1, "asignar de nuevo no duplica ni cambia al docente de una materia ya asignada");
await EsperarEstado(administrador, HttpMethod.Post, "ofertas/lote", new CargaEnBloque(periodoBloque, seccionOtra, [new MateriaDocente(materiaPropia, docenteId)]),
    HttpStatusCode.BadRequest, "asignar por sección respeta la carrera de cada materia");
await EsperarEstado(docente, HttpMethod.Post, "ofertas/lote", new CargaEnBloque(periodoBloque, seccionId, [new MateriaDocente(materiaAjena, docenteId)]),
    HttpStatusCode.Forbidden, "un docente no arma la carga académica");
var cargaSeccion = await Enviar<List<MateriaAsignada>>(administrador, HttpMethod.Get, $"periodos/{periodoBloque}/secciones/{seccionId}/carga");
Comprobar(cargaSeccion.Select(c => c.MateriaId).Order().SequenceEqual(new[] { materiaPropia, materiaComun }.Order()), "la carga de una sección lista sus materias asignadas");
await Enviar<bool>(administrador, HttpMethod.Put, $"usuarios/{docenteTemporal}/estado", new EstadoCuenta(false));
var periodoCopia = await Enviar<long>(administrador, HttpMethod.Post, "catalogos/periodos", new Referencia(0, "Copia " + sufijo));
var copia = await Enviar<ResultadoCarga>(administrador, HttpMethod.Post, $"periodos/{periodoCopia}/copiar-carga", new CopiaDeCarga(periodoBloque));
Comprobar(copia.Creadas == 1 && copia.SinDocente == 1, "copiar la carga repite las materias y omite a los docentes desactivados");
var copiaRepetida = await Enviar<ResultadoCarga>(administrador, HttpMethod.Post, $"periodos/{periodoCopia}/copiar-carga", new CopiaDeCarga(periodoBloque));
Comprobar(copiaRepetida.Creadas == 0 && copiaRepetida.Existentes == 1, "copiar dos veces no duplica la carga");
await EsperarEstado(administrador, HttpMethod.Post, $"periodos/{periodoId}/copiar-carga", new CopiaDeCarga(periodoBloque), HttpStatusCode.BadRequest,
    "no se copia carga a un período cerrado");
await Enviar<int>(administrador, HttpMethod.Put, $"secciones/{seccionOtra}", new CambioSeccion("C", 0));
await Enviar<int>(administrador, HttpMethod.Put, $"catalogos/carreras/{otraCarrera}", new CambioNombre("Carrera corregida " + sufijo));
await Enviar<int>(administrador, HttpMethod.Put, $"materias/{materiaAjena}", new NuevaMateria($"AJE-{sufijo}", "Ajena corregida " + sufijo, 4));
var corregidas = await Enviar<List<Materia>>(administrador, HttpMethod.Get, "materias");
Comprobar(corregidas.Single(m => m.Id == materiaAjena) is { Uc: 4 } ajena && ajena.Nombre.StartsWith("Ajena corregida") && ajena.Carreras.StartsWith("Carrera corregida")
    && (await Enviar<List<Seccion>>(administrador, HttpMethod.Get, "secciones")).Single(s => s.Id == seccionOtra) is { Nombre: "C" } seccionCorregida && seccionCorregida.Carrera.StartsWith("Carrera corregida"),
    "se corrigen carrera, sección y materia (nombre y UC), y la materia sabe en qué pensum está");
await EsperarEstado(administrador, HttpMethod.Put, $"catalogos/periodos/{periodoId}", new CambioNombre("Otro nombre"), HttpStatusCode.Conflict,
    "un período cerrado conserva su nombre");
var paraRetirar = cargaSeccion.Single(x => x.MateriaId == materiaComun).OfertaId;
var revisionRetiro = (await Plan(administrador, paraRetirar)).Oferta.Revision;
await EsperarEstado(administrador, HttpMethod.Post, $"ofertas/{paraRetirar}/retirar", new RevisionEsperada(revisionRetiro), HttpStatusCode.OK, "una carga sin movimientos se retira");
await EsperarEstado(administrador, HttpMethod.Get, $"ofertas/{paraRetirar}", null, HttpStatusCode.NotFound, "la carga retirada ya no existe");
var conNotas = await Plan(administrador, ofertaCarrera);
await EsperarEstado(administrador, HttpMethod.Post, $"ofertas/{ofertaCarrera}/retirar", new RevisionEsperada(conNotas.Oferta.Revision), HttpStatusCode.Conflict,
    "una carga con evaluaciones y estudiantes no se retira");

// Pensum: una materia por código, vinculada al pensum de cada carrera que la tiene.
var pensumOtra = await Enviar<List<MateriaPensum>>(administrador, HttpMethod.Get, $"pensum?carrera={otraCarrera}");
Comprobar(pensumOtra.Single(m => m.MateriaId == materiaComun).Pensums == 2, "la materia del ciclo básico es una sola y está en dos pensums");
await EsperarEstado(administrador, HttpMethod.Post, $"materias?carrera={otraCarrera}&semestre=1", new NuevaMateria($"cb-{sufijo}", "Copia", 3), HttpStatusCode.Conflict,
    "no se crea una segunda materia con el mismo código");
await EsperarEstado(administrador, HttpMethod.Post, $"materias?carrera={otraCarrera}", new NuevaMateria("MAL CODIGO", "x", 3), HttpStatusCode.BadRequest, "código con espacios rechazado");
await EsperarEstado(administrador, HttpMethod.Post, $"materias?carrera={otraCarrera}", new NuevaMateria($"UC-{sufijo}", "x", 99), HttpStatusCode.BadRequest, "UC fuera de rango rechazadas");
await EsperarEstado(administrador, HttpMethod.Delete, $"carreras/{carreraId}/pensum/{materiaPropia}", null, HttpStatusCode.Conflict,
    "una materia ya asignada en la carrera no sale del pensum");
await EsperarEstado(administrador, HttpMethod.Delete, $"carreras/{otraCarrera}/pensum/{materiaComun}", null, HttpStatusCode.OK, "una materia sin asignaciones en la carrera sale de su pensum");
Comprobar((await Enviar<List<MateriaPensum>>(administrador, HttpMethod.Get, $"pensum?carrera={carreraId}")).Any(m => m.MateriaId == materiaComun),
    "quitarla de una carrera no la quita de las otras");
await EsperarEstado(docente, HttpMethod.Get, "pensum", null, HttpStatusCode.Forbidden, "un docente no administra el pensum");

byte[] ExcelPensum(params (string Codigo, string Nombre, object Uc, object Semestre)[] filas)
{
    using var libro = new XLWorkbook();
    var hoja = libro.AddWorksheet("pensum");
    hoja.Cell(4, 1).Value = "código"; hoja.Cell(4, 2).Value = "materia"; hoja.Cell(4, 3).Value = "UC"; hoja.Cell(4, 4).Value = "semestre";
    for (var i = 0; i < filas.Length; i++)
    {
        hoja.Cell(5 + i, 1).Value = filas[i].Codigo; hoja.Cell(5 + i, 2).Value = filas[i].Nombre;
        hoja.Cell(5 + i, 3).Value = XLCellValue.FromObject(filas[i].Uc); hoja.Cell(5 + i, 4).Value = XLCellValue.FromObject(filas[i].Semestre);
    }
    using var salida = new MemoryStream();
    libro.SaveAs(salida);
    return salida.ToArray();
}
async Task<VistaPensum> LeerPensum(long carrera, byte[] archivo)
{
    using var contenido = new ByteArrayContent(archivo);
    contenido.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    using var respuesta = await administrador.PostAsync($"carreras/{carrera}/pensum/leer-excel", contenido);
    if (!respuesta.IsSuccessStatusCode) throw new InvalidOperationException($"leer pensum: {(int)respuesta.StatusCode} {await respuesta.Content.ReadAsStringAsync()}");
    return (await respuesta.Content.ReadFromJsonAsync<VistaPensum>(json))!;
}
var civil = await Enviar<long>(administrador, HttpMethod.Post, "carreras", new NuevaCarrera("Ingeniería Civil " + sufijo, null));
var archivo = ExcelPensum(($"CB-{sufijo}", "Ciclo básico " + sufijo, 3, 1), ($"EST-{sufijo}", "Estática " + sufijo, 4, 3), ($"ELE-{sufijo}", "Oratoria " + sufijo, 2, ""));
var vistaPensum = await LeerPensum(civil, archivo);
Comprobar(vistaPensum.Errores.Count == 0 && vistaPensum.Filas.Count == 3 && vistaPensum.Filas[0].Accion.StartsWith("Se agrega") && vistaPensum.Filas[1].Accion == "Materia nueva",
    "leer el Excel del pensum dice qué pasará con cada fila, sin guardar nada");
Comprobar((await Enviar<List<MateriaPensum>>(administrador, HttpMethod.Get, $"pensum?carrera={civil}")).Count == 0, "revisar el Excel no escribe en la base");
var importado = await Enviar<ResultadoPensum>(administrador, HttpMethod.Post, $"carreras/{civil}/pensum/importar", new ImportarPensum(vistaPensum.Filas));
Comprobar(importado is { Nuevas: 2, Agregadas: 1, Cambiadas: 0 }, "importar el pensum crea las nuevas y agrega la del ciclo básico sin duplicarla");
var pensumCivil = await Enviar<List<MateriaPensum>>(administrador, HttpMethod.Get, $"pensum?carrera={civil}");
Comprobar(pensumCivil.Count == 3 && pensumCivil.Single(m => m.Codigo == $"ELE-{sufijo}".ToUpperInvariant()).Semestre == 0
    && pensumCivil.Single(m => m.MateriaId == materiaComun).Pensums == 2, "el pensum importado queda con sus semestres y la materia compartida");
var repetirImportacion = await Enviar<ResultadoPensum>(administrador, HttpMethod.Post, $"carreras/{civil}/pensum/importar", new ImportarPensum(vistaPensum.Filas));
Comprobar(repetirImportacion is { Nuevas: 0, Agregadas: 0, SinCambios: 3 }, "importar dos veces el mismo pensum no duplica nada");
var conflicto = await LeerPensum(civil, ExcelPensum(($"EST-{sufijo}", "Otro nombre", 4, 3), ($"EST2-{sufijo}", "Bien", "cuatro", 3)));
Comprobar(conflicto.Errores.Count == 2, "el Excel con un código ya usado por otra materia o UC no numérica se rechaza fila por fila");
await EsperarEstado(administrador, HttpMethod.Post, $"carreras/{civil}/pensum/importar", new ImportarPensum([new FilaPensum($"EST-{sufijo}", "Otro nombre", 4, 3)]),
    HttpStatusCode.Conflict, "aunque se salte la revisión, el servidor no importa un conflicto");
using (var basura = new ByteArrayContent([1, 2, 3, 4, 5]))
{
    basura.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    using var respuesta = await administrador.PostAsync($"carreras/{civil}/pensum/leer-excel", basura);
    Comprobar(respuesta.StatusCode == HttpStatusCode.BadRequest, "un archivo que no es Excel se rechaza con un mensaje, sin error del servidor");
}

// Reglas de calificación: configurables, y un período cerrado conserva las que regían.
await EsperarEstado(docente, HttpMethod.Put, "reglas", fabrica, HttpStatusCode.Forbidden, "un docente no cambia las reglas");
await EsperarEstado(administrador, HttpMethod.Put, "reglas", fabrica with { EscalaMinima = 0 }, HttpStatusCode.Conflict,
    "con notas cargadas la escala ya no cambia");
await EsperarEstado(administrador, HttpMethod.Put, "reglas", fabrica with { NotaAprobatoria = 25 }, HttpStatusCode.BadRequest, "una nota aprobatoria fuera de la escala se rechaza");
var antesDeCambiar = await Enviar<RecordAcademico>(nuevoAcceso, HttpMethod.Get, "record");
await Enviar<ReglasCalificacion>(administrador, HttpMethod.Put, "reglas", pnf with { Indice = Indices.Simple });
var despues = await Enviar<RecordAcademico>(nuevoAcceso, HttpMethod.Get, "record");
Comprobar(despues.Materias.Where(m => m.PeriodoCerrado).Select(m => (m.NotaFinal, m.Condicion)).SequenceEqual(antesDeCambiar.Materias.Where(m => m.PeriodoCerrado).Select(m => (m.NotaFinal, m.Condicion))),
    "subir la nota aprobatoria a 12 no cambia lo aprobado en un período ya cerrado");
var finalesCerradas = despues.Materias.Where(m => m.PeriodoCerrado && m.NotaFinal.HasValue).Select(m => m.NotaFinal!.Value).ToList();
Comprobar(despues.Indice == Indices.Simple && despues.Promedio == decimal.Round(finalesCerradas.Average(), 2, MidpointRounding.AwayFromZero),
    "el tipo de índice sí se puede cambiar después");
Comprobar((await Plan(administrador, ofertaCarrera)).Reglas is { NotaAprobatoria: 12, Redondeo: Redondeos.Entero }, "un período abierto usa las reglas nuevas");
await Enviar<int>(administrador, HttpMethod.Put, $"materias/{materiaTres}", new NuevaMateria($"M3-{sufijo}", "Materia tres " + sufijo, 3, 16));
await Enviar<int>(administrador, HttpMethod.Put, $"materias/{materiaId}", new NuevaMateria($"M1-{sufijo}", "Materia uno " + sufijo, 3, 16));
Comprobar((await Plan(administrador, ofertaCarrera)).Reglas.NotaAprobatoria == 16, "una materia puede aprobar con su propia nota, como un proyecto con 16");
await EsperarEstado(administrador, HttpMethod.Put, $"materias/{materiaId}", new NuevaMateria($"M1-{sufijo}", "Materia uno " + sufijo, 3, 21), HttpStatusCode.BadRequest,
    "la nota aprobatoria propia también cabe en la escala");
Comprobar((await Plan(administrador, ofertaId)).Reglas.NotaAprobatoria == 10, "la planilla del período cerrado sigue con la nota aprobatoria con que cerró");
await Enviar<int>(administrador, HttpMethod.Put, $"materias/{materiaId}", new NuevaMateria($"M1-{sufijo}", "Materia uno " + sufijo, 3));
await Enviar<int>(administrador, HttpMethod.Put, $"materias/{materiaTres}", new NuevaMateria($"M3-{sufijo}", "Materia tres " + sufijo, 3));
await Enviar<ReglasCalificacion>(administrador, HttpMethod.Put, "reglas", fabrica);
// Código de sección con formato configurable. La base de datos sostiene la integridad aunque se salte la lógica.
var formatoInicial = (await Enviar<FormatoCodigo>(administrador, HttpMethod.Get, "formato-seccion")).Formato;
Comprobar(formatoInicial.Contains("{seccion}"), "el formato vigente se puede consultar");
await EsperarEstado(docente, HttpMethod.Get, "formato-seccion", null, HttpStatusCode.Forbidden, "un docente no ve ni cambia el formato");
await EsperarEstado(administrador, HttpMethod.Put, "formato-seccion", new FormatoCodigo("{curso}-{seccion}"), HttpStatusCode.BadRequest, "el servidor rechaza un marcador desconocido");
await EsperarEstado(administrador, HttpMethod.Put, "formato-seccion", new FormatoCodigo("{carrera}-{seccion}"), HttpStatusCode.BadRequest,
    "no se aplica un formato con {carrera} mientras haya secciones de carreras sin código");
await EsperarEstado(administrador, HttpMethod.Put, "formato-seccion", new FormatoCodigo("{semestre:00}S-{seccion}"), HttpStatusCode.BadRequest,
    "no se aplica un formato con {semestre} mientras haya secciones sin semestre");
var codigosAntes = (await Enviar<List<Seccion>>(administrador, HttpMethod.Get, "secciones")).ToDictionary(s => s.Id, s => s.Codigo);
var aplicado = await Enviar<ResultadoFormato>(administrador, HttpMethod.Put, "formato-seccion", new FormatoCodigo("SEC-{seccion}"));
var recodificadas = await Enviar<List<Seccion>>(administrador, HttpMethod.Get, "secciones");
Comprobar(aplicado.Secciones == recodificadas.Count && recodificadas.All(s => s.Codigo == "SEC-" + s.Nombre), "cambiar el formato rearma el código de todas las secciones");
Comprobar((await Enviar<Pagina<Oferta>>(administrador, HttpMethod.Get, $"ofertas?buscar=SEC-")).Total > 0, "la carga académica muestra y busca el código nuevo");
await Enviar<ResultadoFormato>(administrador, HttpMethod.Put, "formato-seccion", new FormatoCodigo(formatoInicial));
Comprobar((await Enviar<List<Seccion>>(administrador, HttpMethod.Get, "secciones")).All(s => s.Codigo == codigosAntes[s.Id]), "volver al formato anterior deja los códigos como estaban");

var codigoCarrera = $"S{semilla}";
var sistemas = await Enviar<long>(administrador, HttpMethod.Post, "carreras", new NuevaCarrera("Ingeniería de Sistemas " + sufijo, codigoCarrera.ToLowerInvariant()));
Comprobar((await Enviar<List<Carrera>>(administrador, HttpMethod.Get, "carreras")).Single(c => c.Id == sistemas).Codigo == codigoCarrera, "el código de carrera se guarda en mayúsculas");
await EsperarEstado(administrador, HttpMethod.Post, "carreras", new NuevaCarrera("Otra con el mismo código " + sufijo, codigoCarrera), HttpStatusCode.Conflict,
    "dos carreras no comparten código");
var d1 = await Enviar<long>(administrador, HttpMethod.Post, "secciones", new NuevaSeccion(sistemas, "D1", 2));
await EsperarEstado(administrador, HttpMethod.Post, "secciones", new NuevaSeccion(sistemas, "D1", 3), HttpStatusCode.Conflict,
    "sin {semestre} en el formato, D1 en dos semestres daría el mismo código y se rechaza");
await EsperarEstado(administrador, HttpMethod.Post, "secciones", new NuevaSeccion(sistemas, "D2", 25), HttpStatusCode.BadRequest, "el semestre fuera de rango se rechaza");
await Enviar<int>(administrador, HttpMethod.Put, $"carreras/{sistemas}", new NuevaCarrera("Ingeniería de Sistemas " + sufijo, codigoCarrera + "X"));
Comprobar((await Enviar<List<Seccion>>(administrador, HttpMethod.Get, "secciones")).Single(s => s.Id == d1) is { Semestre: 2, Codigo: "D1" }, "corregir la carrera conserva semestre y código de sus secciones");

async Task<bool> BaseRechaza(string sql)
{
    try { await mantenimiento.Directo(d => d.Ejecutar(sql)); return false; }
    catch (MySqlConnector.MySqlException) { return true; }
}
Comprobar(await BaseRechaza($"INSERT INTO secciones (carrera_id,semestre,nombre,codigo) VALUES ({sistemas},5,'Z9','D1')"), "la base impide repetir un código de sección en la carrera");
Comprobar(await BaseRechaza($"INSERT INTO secciones (carrera_id,semestre,nombre,codigo) VALUES ({sistemas},2,'D1','OTRO')"), "la base impide repetir sección y semestre en la carrera");
Comprobar(await BaseRechaza($"INSERT INTO secciones (carrera_id,semestre,nombre,codigo) VALUES ({sistemas},30,'Z8','Z8')"), "la base impide un semestre fuera de rango");
Comprobar(await BaseRechaza($"INSERT INTO secciones (carrera_id,semestre,nombre,codigo) VALUES ({sistemas},1,'Z7','')"), "la base impide un código de sección vacío");
Comprobar(await BaseRechaza($"UPDATE carreras SET codigo='' WHERE id={sistemas}"), "la base impide un código de carrera vacío");
Comprobar(await BaseRechaza("INSERT INTO configuracion (clave,valor) VALUES ('otra','x')"), "la configuración solo admite sus claves");
Comprobar(await BaseRechaza("UPDATE configuracion SET valor='' WHERE clave='formato_seccion'"), "el formato no puede quedar vacío en la base");
Comprobar(await BaseRechaza($"DELETE FROM carreras WHERE id={sistemas}"), "una carrera con secciones no se puede borrar");
Comprobar(await BaseRechaza($"INSERT INTO ofertas (periodo_id,seccion_id,carrera_id,materia_id,docente_id) VALUES ({periodoBloque},{seccionId},{carreraId},{materiaAjena},{docenteId})"),
    "la base impide asignar una materia que no está en el pensum de la carrera");
Comprobar(await BaseRechaza($"INSERT INTO ofertas (periodo_id,seccion_id,carrera_id,materia_id,docente_id) VALUES ({periodoBloque},{seccionId},{otraCarrera},{materiaAjena},{docenteId})"),
    "la base impide que la asignación diga una carrera distinta de la de su sección");
Comprobar(await BaseRechaza($"DELETE FROM pensum WHERE carrera_id={carreraId} AND materia_id={materiaPropia}"), "la base impide quitar del pensum una materia asignada");
Comprobar(await BaseRechaza($"UPDATE materias SET codigo=(SELECT codigo FROM (SELECT codigo FROM materias WHERE id={materiaAjena}) x) WHERE id={materiaPropia}"), "la base impide repetir un código de materia");
Comprobar(await BaseRechaza($"UPDATE materias SET uc=61 WHERE id={materiaPropia}"), "la base impide UC fuera de rango");
Comprobar(await BaseRechaza($"UPDATE pensum SET semestre=21 WHERE carrera_id={carreraId} AND materia_id={materiaPropia}"), "la base impide un semestre de pensum fuera de rango");
Comprobar(await BaseRechaza("UPDATE periodos SET redondeo_cierre=NULL WHERE cerrado=TRUE LIMIT 1"), "un período cerrado no puede perder las reglas con que cerró");
Comprobar(await BaseRechaza($"UPDATE periodos SET cerrado=TRUE WHERE id={periodoBloque}"), "la base no deja cerrar un período sin guardar sus reglas");
Comprobar(await BaseRechaza("UPDATE notas SET valor=101 LIMIT 1"), "la base impide notas sobre 100 aunque la escala sea configurable");
Comprobar(await BaseRechaza("UPDATE configuracion SET clave='otra' WHERE clave='redondeo'"), "las reglas de calificación no cambian de clave");

// Códigos de acceso por sección, para entregarlos en clase: solo para cuentas que nunca se activaron.
var cedulaAjeno = (semilla + 9).ToString();
var ajenoId = await Crear(cedulaAjeno, "Docente ajeno", Roles.Docente);
await Activar(ajenoId, cedulaAjeno);
using var ajeno = await Entrar(cedulaAjeno, clavePrueba);
await EsperarEstado(ajeno, HttpMethod.Post, $"ofertas/{ofertaId}/codigos", new CodigosSeccion(7), HttpStatusCode.Forbidden, "otro docente no genera códigos de una sección ajena");
await EsperarEstado(nuevoAcceso, HttpMethod.Post, $"ofertas/{ofertaId}/codigos", new CodigosSeccion(7), HttpStatusCode.Forbidden, "un estudiante no genera códigos");
await EsperarEstado(docente, HttpMethod.Post, $"ofertas/{ofertaId}/codigos", new CodigosSeccion(0), HttpStatusCode.BadRequest, "los códigos duran al menos un día");
await EsperarEstado(docente, HttpMethod.Post, $"ofertas/{ofertaId}/codigos", new CodigosSeccion(31), HttpStatusCode.BadRequest, "los códigos duran como máximo 30 días");
var hojaCodigos = await Enviar<CodigosDeSeccion>(docente, HttpMethod.Post, $"ofertas/{ofertaId}/codigos", new CodigosSeccion(7));
Comprobar(hojaCodigos.Codigos.Count > 0 && hojaCodigos.Codigos.All(c => c.Cedula != cedulaEstudiante) && hojaCodigos.ConAcceso >= 1,
    "el docente genera códigos solo para quienes no activaron su cuenta; la cuenta ya activa no se toca");
Comprobar(hojaCodigos.Codigos.All(c => System.Text.RegularExpressions.Regex.IsMatch(c.Codigo, "^[A-HJ-NP-Z2-9]{4}(-[A-HJ-NP-Z2-9]{4}){3}$")
    && c.VenceEn > DateTime.UtcNow.AddDays(6.9)), "los códigos son cortos, sin caracteres que se confundan, y vencen en los días pedidos");
var tira = hojaCodigos.Codigos.First(c => c.Cedula == cedulaSegunda);
// A esta altura la prueba ya usó los 20 intentos por minuto de acceso y activación: se espera a que se renueve el límite.
await Task.Delay(TimeSpan.FromSeconds(61));
await EsperarEstado(anonimo, HttpMethod.Post, "activar", new ActivarCuenta(cedulaSegunda, tira.Codigo.ToLowerInvariant().Replace("-", " "), clavePrueba),
    HttpStatusCode.NoContent, "el código de la tira activa la cuenta aunque se escriba en minúsculas y sin guiones");
await EsperarEstado(anonimo, HttpMethod.Post, "activar", new ActivarCuenta(cedulaSegunda, tira.Codigo, clavePrueba), HttpStatusCode.BadRequest,
    "la tira no sirve dos veces");
Comprobar((await Enviar<CodigosDeSeccion>(administrador, HttpMethod.Post, $"ofertas/{ofertaId}/codigos", new CodigosSeccion(3))).Codigos.All(c => c.Cedula != cedulaSegunda),
    "generar otra hoja no incluye a quien ya activó su cuenta");
// Registro y recuperación por correo, con el buzón de pruebas: los correos quedan en archivos.
var buzon = Environment.GetEnvironmentVariable("ACADEMICO_CORREO_CARPETA") ?? throw new InvalidOperationException("Falta ACADEMICO_CORREO_CARPETA para las pruebas de correo.");
string CodigoRecibido(string para)
{
    var ultimo = Directory.GetFiles(buzon, "*.json").Order().Select(a => JsonDocument.Parse(File.ReadAllText(a)).RootElement)
        .Last(c => c.GetProperty("para").GetString() == para);
    return System.Text.RegularExpressions.Regex.Match(ultimo.GetProperty("texto").GetString()!, @"\b(\d{6})\b").Groups[1].Value;
}
Comprobar((await Enviar<EstadoConfiguracion>(anonimo, HttpMethod.Get, "configuracion")).CorreoDisponible, "con correo configurado el acceso ofrece el registro por correo");
// Los intentos de acceso tienen un límite por minuto; esta parte usa varios.
await Task.Delay(TimeSpan.FromSeconds(61));
var cedulaCorreoA = (semilla + 11).ToString();
var cedulaCorreoB = (semilla + 12).ToString();
var correoA = $"ana.{sufijo}@prueba.test";
var idA = await Enviar<long>(administrador, HttpMethod.Post, "usuarios", new NuevaPersona(cedulaCorreoA, "Ana", "Correo", Roles.Estudiante, correoA.ToUpperInvariant()));
var idB = await Enviar<long>(administrador, HttpMethod.Post, "usuarios", new NuevaPersona(cedulaCorreoB, "Beto", "Correo", Roles.Estudiante, $"beto.{sufijo}@prueba.test"));
await EsperarEstado(administrador, HttpMethod.Post, "usuarios", new NuevaPersona((semilla + 13).ToString(), "Otra", "Persona", Roles.Estudiante, correoA), HttpStatusCode.Conflict,
    "dos cuentas no comparten correo");
await EsperarEstado(administrador, HttpMethod.Post, "usuarios", new NuevaPersona((semilla + 14).ToString(), "Otra", "Persona", Roles.Estudiante, "no es correo"), HttpStatusCode.BadRequest,
    "un correo mal escrito se rechaza");
var cuentaA = (await Enviar<Pagina<Persona>>(administrador, HttpMethod.Get, $"usuarios?buscar={cedulaCorreoA}")).Elementos.Single();
Comprobar(cuentaA is { Correo: var guardado, Registrada: false, EstadoCuenta: "sin registrar" } && guardado == correoA, "el correo se guarda en minúsculas y la cuenta queda sin registrar");
var enviado = await Enviar<SolicitudEnviada>(anonimo, HttpMethod.Post, "registro/codigo", new SolicitarCodigo("V-" + cedulaCorreoA));
Comprobar(enviado.Correo.StartsWith("a") && enviado.Correo.Contains('•') && enviado.Correo.EndsWith("@prueba.test") && !enviado.Registrada,
    "pedir el código muestra el correo a medias y avisa que es la primera vez");
var codigoA = CodigoRecibido(correoA);
Comprobar(codigoA.Length == 6, "el código de seis cifras llega al correo de la matrícula");
await EsperarEstado(anonimo, HttpMethod.Post, "registro/codigo", new SolicitarCodigo(cedulaCorreoA), HttpStatusCode.TooManyRequests, "no se envía otro código antes de un minuto");
await EsperarEstado(anonimo, HttpMethod.Post, "registro/codigo", new SolicitarCodigo("12"), HttpStatusCode.BadRequest, "una cédula mal escrita se rechaza");
await EsperarEstado(anonimo, HttpMethod.Post, "registro/codigo", new SolicitarCodigo((semilla + 15).ToString()), HttpStatusCode.NotFound, "una cédula sin correo no recibe código");
var otroCodigo = codigoA == "000000" ? "111111" : "000000";
await EsperarEstado(anonimo, HttpMethod.Post, "registro/confirmar", new ConfirmarCorreo(cedulaCorreoA, otroCodigo, clavePrueba), HttpStatusCode.BadRequest, "un código equivocado no registra");
await EsperarEstado(anonimo, HttpMethod.Post, "registro/confirmar", new ConfirmarCorreo(cedulaCorreoA, codigoA, "corta"), HttpStatusCode.BadRequest, "la contraseña nueva cumple el mínimo");
await EsperarEstado(anonimo, HttpMethod.Post, "registro/confirmar", new ConfirmarCorreo(cedulaCorreoA, codigoA[..3] + " " + codigoA[3..], clavePrueba), HttpStatusCode.NoContent,
    "con el código del correo la estudiante crea su contraseña, aunque lo escriba con espacios");
await EsperarEstado(anonimo, HttpMethod.Post, "registro/confirmar", new ConfirmarCorreo(cedulaCorreoA, codigoA, clavePrueba), HttpStatusCode.BadRequest, "el código del correo no sirve dos veces");
using (var entra = await Entrar(cedulaCorreoA, clavePrueba))
    Comprobar((await Enviar<RecordAcademico>(entra, HttpMethod.Get, "record")).Estudiante.Cedula == cedulaCorreoA, "la estudiante entra con la contraseña que eligió");
Comprobar((await Enviar<Pagina<Persona>>(administrador, HttpMethod.Get, $"usuarios?buscar={cedulaCorreoA}")).Elementos.Single().EstadoCuenta == "activa", "la cuenta pasa a activa");
// Cinco intentos fallidos agotan el código.
await Enviar<SolicitudEnviada>(anonimo, HttpMethod.Post, "registro/codigo", new SolicitarCodigo(cedulaCorreoB));
var codigoB = CodigoRecibido($"beto.{sufijo}@prueba.test");
var malo = codigoB == "999999" ? "888888" : "999999";
for (var i = 0; i < 5; i++) await EsperarEstado(anonimo, HttpMethod.Post, "registro/confirmar", new ConfirmarCorreo(cedulaCorreoB, malo, clavePrueba), HttpStatusCode.BadRequest, $"intento fallido {i + 1}");
await EsperarEstado(anonimo, HttpMethod.Post, "registro/confirmar", new ConfirmarCorreo(cedulaCorreoB, codigoB, clavePrueba), HttpStatusCode.BadRequest,
    "después de cinco intentos fallidos el código correcto ya no sirve");
// Corregir el correo invalida el código pendiente; la matrícula de un docente no puede poner correos.
await Enviar<int>(administrador, HttpMethod.Put, $"usuarios/{idB}/correo", new CambioCorreo($"beto.nuevo.{sufijo}@prueba.test"));
await EsperarEstado(administrador, HttpMethod.Put, $"usuarios/{idB}/correo", new CambioCorreo(correoA), HttpStatusCode.Conflict, "corregir con un correo ajeno se rechaza");
await EsperarEstado(docente, HttpMethod.Put, $"usuarios/{idB}/correo", new CambioCorreo("x@prueba.test"), HttpStatusCode.Forbidden, "un docente no cambia correos");
Comprobar(await BaseRechaza($"UPDATE usuarios SET correo='MAYUSCULA@PRUEBA.TEST' WHERE id={idB}"), "la base exige el correo en minúsculas");
Comprobar(await BaseRechaza($"UPDATE usuarios SET correo='sin-arroba' WHERE id={idB}"), "la base exige forma de correo");
Comprobar(await BaseRechaza($"UPDATE usuarios SET correo='{correoA}' WHERE id={idB}"), "la base impide dos cuentas con el mismo correo");
Comprobar(await BaseRechaza($"INSERT INTO verificaciones_correo (usuario_id,huella,vence_en,intentos) VALUES ({idB},REPEAT('a',64),UTC_TIMESTAMP(),6)"),
    "la base no admite más de cinco intentos por código");
var planillaCorreo = await Plan(administrador, ofertaCarrera);
var cedulaMatricula = (semilla + 16).ToString();
await Enviar<ResultadoImportacion>(administrador, HttpMethod.Post, $"ofertas/{ofertaCarrera}/importar", new ImportarMatricula(planillaCorreo.Oferta.Revision,
    [new FilaMatricula(cedulaMatricula, "Carla", "Matrícula", planillaCorreo.Oferta.Carrera, planillaCorreo.Oferta.Seccion, $"Carla.{sufijo}@Prueba.test"),
     new FilaMatricula(cedulaCorreoA, "Ana", "Correo", planillaCorreo.Oferta.Carrera, planillaCorreo.Oferta.Seccion, $"otra.{sufijo}@prueba.test")]));
var importadas = (await Enviar<Pagina<Persona>>(administrador, HttpMethod.Get, $"usuarios?buscar=Correo")).Elementos.Concat(
    (await Enviar<Pagina<Persona>>(administrador, HttpMethod.Get, $"usuarios?buscar={cedulaMatricula}")).Elementos).ToList();
Comprobar(importadas.Single(p => p.Cedula == cedulaMatricula).Correo == $"carla.{sufijo}@prueba.test", "la matrícula de administración guarda el correo del Excel");
Comprobar(importadas.Single(p => p.Cedula == cedulaCorreoA).Correo == correoA, "la matrícula no reemplaza el correo de una cuenta que ya se registró");await EsperarEstado(docente, HttpMethod.Post, "correo/prueba", new PruebaCorreo("x@prueba.test"), HttpStatusCode.Forbidden, "solo administración prueba el correo");
await EsperarEstado(administrador, HttpMethod.Post, "correo/prueba", new PruebaCorreo($"prueba.{sufijo}@prueba.test"), HttpStatusCode.NoContent, "administración envía un correo de prueba");
// Matrícula del período: un solo Excel con todas las secciones; cada materia una vez por período y nada de lo ya aprobado.
var periodoMat = await Enviar<long>(administrador, HttpMethod.Post, "catalogos/periodos", new Referencia(0, "Matrícula " + sufijo));
var x1 = await MateriaEn(carreraId, $"X1-{sufijo}", "Equis uno " + sufijo);
var x2 = await MateriaEn(carreraId, $"X2-{sufijo}", "Equis dos " + sufijo);
var seccionB = await Enviar<long>(administrador, HttpMethod.Post, "secciones", new NuevaSeccion(carreraId, "B"));
var x1A = await Enviar<long>(administrador, HttpMethod.Post, "ofertas", new NuevaOferta(periodoMat, seccionId, x1, docenteId));
var x2A = await Enviar<long>(administrador, HttpMethod.Post, "ofertas", new NuevaOferta(periodoMat, seccionId, x2, docenteId));
var m1A = await Enviar<long>(administrador, HttpMethod.Post, "ofertas", new NuevaOferta(periodoMat, seccionId, materiaId, docenteId));
var x1B = await Enviar<long>(administrador, HttpMethod.Post, "ofertas", new NuevaOferta(periodoMat, seccionB, x1, docenteId));
var (codigoX1, codigoX2, codigoM1) = ($"X1-{sufijo}".ToUpperInvariant(), $"X2-{sufijo}".ToUpperInvariant(), $"M1-{sufijo}".ToUpperInvariant());
var cedulaP1 = (semilla + 21).ToString();
var cedulaP2 = (semilla + 22).ToString();
byte[] ExcelMatricula(params string[][] filas)
{
    using var libro = new XLWorkbook();
    var hoja = libro.AddWorksheet("matricula");
    string[] titulos = ["cédula", "nombres", "apellidos", "carrera", "sección", "correo", "materias"];
    for (var c = 0; c < titulos.Length; c++) hoja.Cell(4, c + 1).Value = titulos[c];
    for (var f = 0; f < filas.Length; f++)
        for (var c = 0; c < filas[f].Length; c++) hoja.Cell(5 + f, c + 1).Value = filas[f][c];
    using var salida = new MemoryStream();
    libro.SaveAs(salida);
    return salida.ToArray();
}
async Task<VistaMatriculaPeriodo> LeerMatricula(long periodo, byte[] contenido)
{
    using var cuerpo = new ByteArrayContent(contenido);
    cuerpo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    using var respuesta = await administrador.PostAsync($"periodos/{periodo}/matricula/leer-excel", cuerpo);
    if (!respuesta.IsSuccessStatusCode) throw new InvalidOperationException($"leer matrícula: {(int)respuesta.StatusCode} {await respuesta.Content.ReadAsStringAsync()}");
    return (await respuesta.Content.ReadFromJsonAsync<VistaMatriculaPeriodo>(json))!;
}
var excelPeriodo = ExcelMatricula(
    [cedulaEstudiante, "Estudiante uno", "Prueba", carreraNombre, "A", "", ""],
    [cedulaP1, "Pedro", "Período", carreraNombre, "A", $"pedro.{sufijo}@prueba.test", ""],
    [cedulaP2, "Paula", "Arrastre", carreraNombre, "A", "", $"{codigoX2.ToLowerInvariant()}, {codigoM1}"],
    [cedulaP2, "Paula", "Arrastre", carreraNombre, "B", "", codigoX1]);
var vistaPeriodo = await LeerMatricula(periodoMat, excelPeriodo);
Comprobar(vistaPeriodo.Errores.Count == 0 && vistaPeriodo.Estudiantes == 3 && vistaPeriodo.Inscripciones == 8 && vistaPeriodo.Plan.Count == 4,
    "un solo Excel inscribe a cada estudiante en las materias de su sección, y a quien arrastra en la otra sección");
Comprobar(vistaPeriodo.Omitidas.Count == 1 && vistaPeriodo.Omitidas[0].Cedula == cedulaEstudiante && vistaPeriodo.Omitidas[0].Motivo.Contains("aprobó"),
    "la revisión avisa que una materia ya aprobada no se vuelve a inscribir");
Comprobar((await Plan(administrador, x1A)).Estudiantes.Count == 0, "revisar la matrícula del período no escribe en la base");
await EsperarEstado(docente, HttpMethod.Post, $"periodos/{periodoMat}/matricula", new ImportarMatriculaPeriodo(vistaPeriodo.Filas), HttpStatusCode.Forbidden,
    "un docente no importa la matrícula del período");
var importadaPeriodo = await Enviar<ResultadoMatriculaPeriodo>(administrador, HttpMethod.Post, $"periodos/{periodoMat}/matricula", new ImportarMatriculaPeriodo(vistaPeriodo.Filas));
Comprobar(importadaPeriodo is { Estudiantes: 3, Inscripciones: 8 } && importadaPeriodo.Omitidas.Count == 1, "importar la matrícula del período guarda lo revisado");
var enX1A = await Plan(administrador, x1A);
var enX1B = await Plan(administrador, x1B);
Comprobar(enX1A.Estudiantes.Select(a => a.Cedula).Order().SequenceEqual(new[] { cedulaEstudiante, cedulaP1 }.Order()) && enX1B.Estudiantes.Single().Cedula == cedulaP2
    && enX1A.Estudiantes.All(a => a.Estado == "confirmada"), "cada estudiante queda confirmado en su sección, y quien arrastra en la otra");
Comprobar((await Plan(administrador, m1A)).Estudiantes.Select(a => a.Cedula).Order().SequenceEqual(new[] { cedulaP1, cedulaP2 }.Order()),
    "lo ya aprobado no se inscribe; quien no lo aprobó sí");
var enSeccionB = await Enviar<Pagina<FilaEstudiante>>(administrador, HttpMethod.Get, $"estudiantes?periodo={periodoMat}&seccion={seccionB}");
Comprobar(enSeccionB.Elementos.Select(e => e.Cedula).SequenceEqual([cedulaP2]) && enSeccionB.Elementos[0].Materias == 3 && enSeccionB.Elementos[0].Secciones.Split(", ").Length == 2,
    "la pestaña Estudiantes filtra por sección y muestra todo lo que el estudiante cursa en el período: sus dos secciones y sus 3 materias");
Comprobar((await Enviar<Pagina<FilaEstudiante>>(administrador, HttpMethod.Get, $"estudiantes?periodo={periodoMat}&buscar={cedulaP1}")).Elementos.Single().Materias == 3,
    "un estudiante se busca por cédula y muestra cuántas materias cursa en el período");
await EsperarEstado(docente, HttpMethod.Get, "estudiantes", null, HttpStatusCode.Forbidden, "un docente no lista estudiantes");
var repetidaPeriodo = await Enviar<ResultadoMatriculaPeriodo>(administrador, HttpMethod.Post, $"periodos/{periodoMat}/matricula", new ImportarMatriculaPeriodo(vistaPeriodo.Filas));
Comprobar(repetidaPeriodo is { Inscripciones: 0, YaEstaban: 8 }, "importar dos veces la misma matrícula no duplica nada");
var conErrores = await LeerMatricula(periodoMat, ExcelMatricula(
    [cedulaP1, "Pedro", "Período", carreraNombre, "Z", "", ""],
    [cedulaP2, "Paula", "Arrastre", carreraNombre, "A", "", "NOPE-1"],
    [cedulaP2, "Paula", "Arrastre", carreraNombre, "A", "", codigoX1],
    [cedulaP2, "Paula", "Arrastre", carreraNombre, "B", "", codigoX1],
    [(semilla + 23).ToString(), "Otra", "Persona", carreraNombre, "A", $"pedro.{sufijo}@prueba.test", ""]));
Comprobar(conErrores.Errores.Select(e => e.Fila).Order().SequenceEqual([5, 6, 8, 9]),
    "la revisión señala por fila la sección que no existe, la materia que la sección no tiene, la materia repetida y el correo de otra cuenta");
await EsperarEstado(administrador, HttpMethod.Post, $"periodos/{periodoMat}/matricula", new ImportarMatriculaPeriodo(conErrores.Filas), HttpStatusCode.BadRequest,
    "aunque se salte la revisión, el servidor no importa un archivo con errores");
await EsperarEstado(administrador, HttpMethod.Post, $"periodos/{periodoId}/matricula", new ImportarMatriculaPeriodo(vistaPeriodo.Filas), HttpStatusCode.Conflict,
    "no se importa matrícula a un período cerrado");
// La misma materia en otra sección del período se omite también desde la planilla.
var planX1A = await Plan(administrador, x1A);
var porPlanilla = await Enviar<ResultadoImportacion>(administrador, HttpMethod.Post, $"ofertas/{x1A}/importar",
    new ImportarMatricula(planX1A.Oferta.Revision, [new FilaMatricula(cedulaP2, "Paula", "Arrastre", carreraNombre, "A")]));
Comprobar(porPlanilla.Nuevos == 0 && porPlanilla.Omitidas?.Single().Motivo.StartsWith("Ya la cursa en la sección") == true, "desde la planilla tampoco se inscribe una materia que ya cursa en otra sección");
var idP2 = (await Enviar<Pagina<Persona>>(administrador, HttpMethod.Get, $"usuarios?buscar={cedulaP2}")).Elementos.Single().Id;
Comprobar(await BaseRechaza($"INSERT INTO inscripciones (oferta_id,periodo_id,materia_id,estudiante_id,estado,propuesta_por) VALUES ({x1A},{periodoMat},{x1},{idP2},'confirmada',{accesoAdmin.Persona.Id})"),
    "la base impide inscribir dos veces la misma materia en un período, aunque sea en otra sección");
Comprobar(await BaseRechaza($"INSERT INTO inscripciones (oferta_id,periodo_id,materia_id,estudiante_id,estado,propuesta_por) VALUES ({x2A},{periodoMat},{x1},{idA},'confirmada',{accesoAdmin.Persona.Id})"),
    "la base impide que la inscripción diga otra materia que la de su asignación");
// Retirar de una materia: deja de verla, sus notas quedan en la historia y puede ir a otra sección.
var planPublicar = await Plan(docente, x1A);
var evaluacionX1 = await Enviar<long>(docente, HttpMethod.Post, $"ofertas/{x1A}/evaluaciones", new NuevaEvaluacion("Única", 100, planPublicar.Oferta.Revision));
planPublicar = await Plan(docente, x1A);
await Enviar<int>(docente, HttpMethod.Put, $"ofertas/{x1A}/notas", new GuardarNotas(planPublicar.Oferta.Revision, [new Nota(evaluacionX1, estudianteId, 17)]));
planPublicar = await Plan(docente, x1A);
await Enviar<long>(docente, HttpMethod.Post, $"ofertas/{x1A}/publicar", new RevisionEsperada(planPublicar.Oferta.Revision));
Comprobar((await Enviar<RecordAcademico>(nuevoAcceso, HttpMethod.Get, "record")).Materias.Any(m => m.OfertaId == x1A), "el estudiante ve la materia publicada");
planPublicar = await Plan(administrador, x1A);
var inscripcionUno = planPublicar.Estudiantes.Single(a => a.EstudianteId == estudianteId).InscripcionId;
await EsperarEstado(docente, HttpMethod.Post, $"ofertas/{x1A}/retirar-estudiantes", new ConfirmarMatricula(planPublicar.Oferta.Revision, [inscripcionUno]), HttpStatusCode.Forbidden,
    "un docente no retira estudiantes");
await Enviar<int>(administrador, HttpMethod.Post, $"ofertas/{x1A}/retirar-estudiantes", new ConfirmarMatricula(planPublicar.Oferta.Revision, [inscripcionUno]));
var trasRetiro = await Plan(administrador, x1A);
Comprobar(trasRetiro.Estudiantes.All(a => a.EstudianteId != estudianteId) && trasRetiro.Notas.All(n => n.EstudianteId != estudianteId),
    "quien se retira sale de la planilla y de sus notas");
Comprobar((await Enviar<RecordAcademico>(nuevoAcceso, HttpMethod.Get, "record")).Materias.All(m => m.OfertaId != x1A),
    "quien se retira deja de ver la materia, aunque ya estuviera publicada");
Comprobar(await bd.Directo(d => d.Escalar<long>("SELECT COUNT(*) FROM notas WHERE inscripcion_id=@id", ("@id", inscripcionUno))) == 1, "retirar no borra las notas: quedan en la historia");
await EsperarEstado(administrador, HttpMethod.Post, $"ofertas/{x1A}/retirar-estudiantes", new ConfirmarMatricula(trasRetiro.Oferta.Revision, [inscripcionUno]), HttpStatusCode.Conflict,
    "no se retira dos veces");
var planX1B = await Plan(administrador, x1B);
var aOtraSeccion = await Enviar<ResultadoImportacion>(administrador, HttpMethod.Post, $"ofertas/{x1B}/importar",
    new ImportarMatricula(planX1B.Oferta.Revision, [new FilaMatricula(cedulaEstudiante, "Estudiante uno", "Prueba", carreraNombre, "B")]));
Comprobar(aOtraSeccion.Nuevos == 1, "después de retirarse puede inscribirse en otra sección de la misma materia");
var sinReinscribir = await Enviar<ResultadoMatriculaPeriodo>(administrador, HttpMethod.Post, $"periodos/{periodoMat}/matricula", new ImportarMatriculaPeriodo(vistaPeriodo.Filas));
Comprobar(sinReinscribir.Inscripciones == 0 && sinReinscribir.Omitidas.Any(o => o.Cedula == cedulaEstudiante && o.Motivo.StartsWith("Ya la cursa en la sección")),
    "volver a importar el período no devuelve a nadie a la sección de la que se retiró");
// Nómina docente desde Excel: crea a los nuevos y no duplica a quien ya está.
byte[] ExcelNomina(params string[][] filas)
{
    using var libro = new XLWorkbook();
    var hoja = libro.AddWorksheet("nomina");
    string[] titulos = ["cédula", "nombres", "apellidos", "correo"];
    for (var c = 0; c < titulos.Length; c++) hoja.Cell(4, c + 1).Value = titulos[c];
    for (var f = 0; f < filas.Length; f++)
        for (var c = 0; c < filas[f].Length; c++) hoja.Cell(5 + f, c + 1).Value = filas[f][c];
    using var salida = new MemoryStream();
    libro.SaveAs(salida);
    return salida.ToArray();
}
var cedulaNomina = (semilla + 31).ToString();
using (var cuerpoNomina = new ByteArrayContent(ExcelNomina([cedulaNomina, "Norma", "Nómina", $"norma.{sufijo}@prueba.test"], [cedulaDocente, "Docente uno", "Prueba", ""])))
{
    cuerpoNomina.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    using var respuesta = await administrador.PostAsync("nomina-docente/leer-excel", cuerpoNomina);
    var nomina = (await respuesta.Content.ReadFromJsonAsync<VistaNomina>(json))!;
    Comprobar(nomina.Errores.Count == 0 && nomina.Filas.Select(f => f.Accion).SequenceEqual(["Docente nuevo", "Ya está, sin cambios"]),
        "revisar la nómina dice quién es nuevo y quién ya está, sin guardar nada");
    var importadaNomina = await Enviar<ResultadoNomina>(administrador, HttpMethod.Post, "nomina-docente/importar", new ImportarNomina(nomina.Filas));
    Comprobar(importadaNomina is { Nuevos: 1, SinCambios: 1 }, "importar la nómina crea solo al docente nuevo");
    var listaNomina = await Enviar<Pagina<FilaDocente>>(administrador, HttpMethod.Get, $"nomina-docente?buscar={cedulaNomina}");
    Comprobar(listaNomina.Elementos.Single() is { Persona.Rol: Roles.Docente, Correo: var correoNomina } && correoNomina == $"norma.{sufijo}@prueba.test",
        "el docente importado aparece en la nómina con su correo");
}
await EsperarEstado(administrador, HttpMethod.Post, "nomina-docente/importar", new ImportarNomina([new FilaNomina(cedulaEstudiante, "Estudiante uno", "Prueba")]),
    HttpStatusCode.Conflict, "la nómina no convierte en docente a un estudiante");
// Excel armados a mano: columnas en otro orden, de más, de menos, repetidas o con otro nombre.
byte[] ExcelLibre(int filaTitulos, string[] titulos, string[][] filas, bool otraHoja = false)
{
    using var libro = new XLWorkbook();
    var hoja = libro.AddWorksheet("datos");
    for (var c = 0; c < titulos.Length; c++) hoja.Cell(filaTitulos, c + 1).Value = titulos[c];
    for (var f = 0; f < filas.Length; f++)
        for (var c = 0; c < filas[f].Length; c++) if (filas[f][c].Length > 0) hoja.Cell(filaTitulos + 1 + f, c + 1).Value = filas[f][c];
    if (otraHoja) libro.AddWorksheet("notas").Cell(1, 1).Value = "x";
    using var salida = new MemoryStream();
    libro.SaveAs(salida);
    return salida.ToArray();
}
async Task<(HttpStatusCode Estado, string Cuerpo)> Subir(string ruta, byte[] contenido)
{
    using var cuerpo = new ByteArrayContent(contenido);
    cuerpo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    using var respuesta = await administrador.PostAsync(ruta, cuerpo);
    return (respuesta.StatusCode, await respuesta.Content.ReadAsStringAsync());
}
var rutaPeriodo = $"periodos/{periodoMat}/matricula/leer-excel";
var cedulaExcel = (semilla + 41).ToString();
var (estadoOrden, cuerpoOrden) = await Subir(rutaPeriodo, ExcelLibre(1, ["Sección", "teléfono", "APELLIDOS", "Cédula", "Nombres", "Carrera", "Correo electrónico"],
    [["A", "0414-0000000", "Orden", "V-" + cedulaExcel, "Columnas", carreraNombre, ""]]));
var leidaOrden = JsonSerializer.Deserialize<VistaMatriculaPeriodo>(cuerpoOrden, json)!;
Comprobar(estadoOrden == HttpStatusCode.OK && leidaOrden.Errores.Count == 0 && leidaOrden.Filas.Single().Cedula == cedulaExcel && leidaOrden.Inscripciones == 3,
    "las columnas se reconocen por su título en cualquier orden, sin importar mayúsculas ni tildes; una columna de más se ignora");
var (estadoSoloMaterias, cuerpoSoloMaterias) = await Subir(rutaPeriodo, ExcelLibre(1, ["cédula", "nombres", "apellidos", "carrera", "sección", "materias"],
    [[cedulaExcel, "Columnas", "Orden", carreraNombre, "A", codigoX2]]));
Comprobar(estadoSoloMaterias == HttpStatusCode.OK && JsonSerializer.Deserialize<VistaMatriculaPeriodo>(cuerpoSoloMaterias, json)!.Inscripciones == 1,
    "la columna materias se respeta aunque falte la de correo: el estudiante queda solo en esa materia");
var (estadoFalta, cuerpoFalta) = await Subir(rutaPeriodo, ExcelLibre(1, ["cédula", "nombres", "apellidos", "carrera"], [[cedulaExcel, "a", "b", carreraNombre]]));
Comprobar(estadoFalta == HttpStatusCode.BadRequest && cuerpoFalta.Contains("Faltan columnas: sección"), "si falta una columna obligatoria, el aviso dice cuál");
var (estadoRepetida, cuerpoRepetida) = await Subir(rutaPeriodo, ExcelLibre(1, ["cédula", "nombres", "apellidos", "carrera", "sección", "sección"], [[cedulaExcel, "a", "b", carreraNombre, "A", "B"]]));
Comprobar(estadoRepetida == HttpStatusCode.BadRequest && cuerpoRepetida.Contains("dos veces"), "una columna repetida se rechaza para no elegir al azar entre las dos");
var (estadoLejos, cuerpoLejos) = await Subir(rutaPeriodo, ExcelLibre(12, ["cédula", "nombres", "apellidos", "carrera", "sección"], [[cedulaExcel, "a", "b", carreraNombre, "A"]]));
Comprobar(estadoLejos == HttpStatusCode.BadRequest && cuerpoLejos.Contains("primeras 10 filas"), "los títulos deben estar en las primeras 10 filas");
var (estadoHojas, cuerpoHojas) = await Subir(rutaPeriodo, ExcelLibre(1, ["cédula", "nombres", "apellidos", "carrera", "sección"], [[cedulaExcel, "a", "b", carreraNombre, "A"]], otraHoja: true));
Comprobar(estadoHojas == HttpStatusCode.BadRequest && cuerpoHojas.Contains("una sola hoja"), "un libro con varias hojas se rechaza");
var (estadoVacio, cuerpoVacio) = await Subir(rutaPeriodo, ExcelLibre(1, ["cédula", "nombres", "apellidos", "carrera", "sección"], []));
Comprobar(estadoVacio == HttpStatusCode.BadRequest && cuerpoVacio.Contains("entre 1 y"), "una plantilla sin filas se rechaza");
var (estadoValores, cuerpoValores) = await Subir(rutaPeriodo, ExcelLibre(1, ["cédula", "nombres", "apellidos", "carrera", "sección", "correo"],
    [[(semilla + 44).ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-VE")), "Puntos", "Cédula", carreraNombre, "A", ""], ["", "", "", "", "", ""], ["E-123", "Mala", "Cédula", carreraNombre, "A", ""],
     [(semilla + 42).ToString(), "Mal", "Correo", carreraNombre, "A", "sin arroba"], [(semilla + 43).ToString(), "", "SinNombre", carreraNombre, "A", ""]]));
var leidaValores = JsonSerializer.Deserialize<VistaMatriculaPeriodo>(cuerpoValores, json)!;
Comprobar(estadoValores == HttpStatusCode.OK && leidaValores.Errores.Select(e => e.Fila).Order().SequenceEqual([4, 5, 6]) && leidaValores.Filas.Any(f => f.Cedula == (semilla + 44).ToString()),
    "cédula con puntos se acepta; fila vacía se salta; cédula extranjera, correo mal escrito y nombre vacío se señalan en su fila");
var (estadoPensum, cuerpoPensum) = await Subir($"carreras/{civil}/pensum/leer-excel", ExcelLibre(1, ["Semestre", "Asignatura", "Código", "U.C."], [["2", "Columnas sueltas", $"COL-{sufijo}", "3"]]));
Comprobar(estadoPensum == HttpStatusCode.OK && JsonSerializer.Deserialize<VistaPensum>(cuerpoPensum, json)!.Filas.Single().Uc == 3,
    "el pensum también se lee por títulos, con equivalentes como «asignatura» o «U.C.»");
var (estadoNominaMal, cuerpoNominaMal) = await Subir("nomina-docente/leer-excel", ExcelLibre(1, ["Correo", "Apellidos", "Nombres"], [["x@prueba.test", "a", "b"]]));
Comprobar(estadoNominaMal == HttpStatusCode.BadRequest && cuerpoNominaMal.Contains("Faltan columnas: cédula"), "la nómina sin columna de cédula se rechaza diciendo cuál falta");
// Uso caótico de la API: basura en cada ruta. Ninguna respuesta puede ser un error del servidor (5xx).
var largo = new string('ñ', 5000);
string[] cuerpos =
[
    "", "{", "null", "[]", "\"texto\"", "12345", "{\"x\":1}", "{\"nombre\":null,\"codigo\":null,\"filas\":null,\"materias\":null,\"notas\":null}",
    "{\"nombre\":123,\"uc\":\"muchas\",\"semestre\":-5,\"revision\":\"x\",\"periodoId\":\"uno\"}",
    "{\"nombre\":\"" + largo + "\",\"codigo\":\"" + largo + "\",\"formato\":\"" + largo + "\"}",
    "{\"nombre\":\"'; DROP TABLE materias; --\",\"codigo\":\"X'OR'1'='1\",\"cedula\":\"<script>\"}",
    "{\"filas\":[null,{},{\"codigo\":null}],\"notas\":[null,{\"evaluacionId\":-1,\"estudianteId\":-1,\"valor\":1e30}],\"materias\":[{}],\"inscripciones\":[0,-1]}",
    "{\"escalaMinima\":-1e30,\"escalaMaxima\":1e30,\"notaAprobatoria\":null,\"redondeo\":null,\"indice\":null}",
];
(HttpMethod Metodo, string Ruta)[] rutas =
[
    (HttpMethod.Post, "usuarios"), (HttpMethod.Put, $"usuarios/{estudianteId}/estado"), (HttpMethod.Post, "catalogos/periodos"), (HttpMethod.Post, "catalogos/xyz"),
    (HttpMethod.Put, "catalogos/carreras/999999999"), (HttpMethod.Post, "materias"), (HttpMethod.Post, $"materias?carrera=abc&semestre=99999999999"),
    (HttpMethod.Put, $"materias/{materiaId}"), (HttpMethod.Put, "materias/-1"), (HttpMethod.Put, $"carreras/{carreraId}/pensum"), (HttpMethod.Put, "carreras/0/pensum"),
    (HttpMethod.Delete, "carreras/999999999/pensum/1"), (HttpMethod.Post, $"carreras/{civil}/pensum/importar"), (HttpMethod.Post, $"carreras/{civil}/pensum/leer-excel"),
    (HttpMethod.Post, "carreras"), (HttpMethod.Put, $"carreras/{carreraId}"), (HttpMethod.Post, "secciones"), (HttpMethod.Put, $"secciones/{seccionId}"),
    (HttpMethod.Put, "formato-seccion"), (HttpMethod.Put, "reglas"), (HttpMethod.Post, "ofertas"), (HttpMethod.Post, "ofertas/lote"),
    (HttpMethod.Post, $"periodos/{periodoId}/copiar-carga"), (HttpMethod.Post, "periodos/abc/cerrar"), (HttpMethod.Post, "periodos/999999999/cerrar"),
    (HttpMethod.Put, $"ofertas/{ofertaCarrera}/docente"), (HttpMethod.Post, $"ofertas/{ofertaCarrera}/retirar"), (HttpMethod.Post, $"ofertas/{ofertaCarrera}/importar"),
    (HttpMethod.Post, $"ofertas/{ofertaCarrera}/confirmar"), (HttpMethod.Post, $"ofertas/{ofertaCarrera}/leer-excel"), (HttpMethod.Get, "ofertas?desde=-5&cantidad=999999"),
    (HttpMethod.Get, "usuarios?buscar=%25%27%22&desde=abc"), (HttpMethod.Get, "pensum?carrera=-1"), (HttpMethod.Get, "ofertas/999999999"), (HttpMethod.Get, "periodos/999999999/cierre"),
    (HttpMethod.Post, $"periodos/{periodoMat}/matricula"), (HttpMethod.Post, $"periodos/{periodoMat}/matricula/leer-excel"), (HttpMethod.Post, "periodos/999999999/matricula"),
    (HttpMethod.Post, $"ofertas/{x1A}/retirar-estudiantes"),
];
var errores5xx = new List<string>();
async Task Golpear(HttpClient quien, HttpMethod metodo, string ruta, string cuerpo)
{
    using var solicitud = new HttpRequestMessage(metodo, ruta);
    if (cuerpo.Length > 0 || metodo != HttpMethod.Get) solicitud.Content = new StringContent(cuerpo, System.Text.Encoding.UTF8, "application/json");
    using var respuesta = await quien.SendAsync(solicitud);
    if ((int)respuesta.StatusCode >= 500) errores5xx.Add($"{metodo} {ruta} con «{(cuerpo.Length > 60 ? cuerpo[..60] + "…" : cuerpo)}»: {(int)respuesta.StatusCode}");
}
foreach (var (metodo, ruta) in rutas)
    foreach (var cuerpo in metodo == HttpMethod.Get || metodo == HttpMethod.Delete ? [""] : cuerpos)
        await Golpear(administrador, metodo, ruta, cuerpo);
// Lo mismo como docente y como estudiante: deben recibir 403, nunca un error del servidor.
foreach (var (metodo, ruta) in rutas.Take(12))
{
    await Golpear(docente, metodo, ruta, cuerpos[7]);
    await Golpear(nuevoAcceso, metodo, ruta, cuerpos[7]);
}
// La contraseña y el acceso, con pocos intentos para no tocar el límite de acceso de las pruebas siguientes.
foreach (var cuerpo in new[] { cuerpos[1], cuerpos[2], cuerpos[7], cuerpos[9] }) await Golpear(docente, HttpMethod.Post, "clave", cuerpo);
foreach (var cuerpo in new[] { cuerpos[2], cuerpos[10] }) { await Golpear(anonimo, HttpMethod.Post, "acceso", cuerpo); await Golpear(anonimo, HttpMethod.Post, "activar", cuerpo); }
// Ráfaga simultánea de escrituras inválidas y válidas sobre la misma asignación.
await Task.WhenAll(Enumerable.Range(0, 40).Select(i => Golpear(administrador, HttpMethod.Post, "ofertas/lote", i % 2 == 0 ? cuerpos[11]
    : JsonSerializer.Serialize(new CargaEnBloque(periodoBloque, seccionId, [new MateriaDocente(materiaPropia, docenteId)]), json))));
using (var enorme = new ByteArrayContent(new byte[3 * 1024 * 1024]))
{
    enorme.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    // Kestrel puede responder 413 o cortar la conexión antes de recibir todo: las dos cosas son rechazar, no caerse.
    // Con un cliente aparte: la conexión que Kestrel corta no debe quedar en el grupo de conexiones de las demás pruebas.
    using var aparte = new HttpClient { BaseAddress = new Uri(direccion), Timeout = TimeSpan.FromSeconds(20) };
    aparte.DefaultRequestHeaders.Authorization = administrador.DefaultRequestHeaders.Authorization;
    try
    {
        using var respuesta = await aparte.PostAsync($"carreras/{civil}/pensum/leer-excel", enorme);
        if ((int)respuesta.StatusCode >= 500) errores5xx.Add($"archivo de 3 MB: {(int)respuesta.StatusCode}");
    }
    catch (HttpRequestException) { }
}
Comprobar(errores5xx.Count == 0, errores5xx.Count == 0 ? $"uso caótico de la API: {rutas.Length} rutas con {cuerpos.Length} cuerpos inválidos, ningún error del servidor"
    : "errores del servidor ante datos basura:\n  " + string.Join("\n  ", errores5xx.Take(15)));
Comprobar((await Enviar<List<Periodo>>(administrador, HttpMethod.Get, "periodos")).Count > 0, "después del uso caótico el servidor sigue respondiendo");
Comprobar((await Enviar<ReglasCalificacion>(administrador, HttpMethod.Get, "reglas")) == fabrica, "la basura no alteró las reglas de calificación");
var abiertas = await bd.Directo(d => d.Escalar<long>("SELECT COUNT(*) FROM information_schema.processlist WHERE USER='registro_local' AND COMMAND='Sleep'"));
Comprobar(abiertas <= 30, "conjunto de conexiones acotado");
Console.WriteLine($"\n{comprobaciones} comprobaciones correctas. Datos sintéticos conservados en la base local de pruebas.");

