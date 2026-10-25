using System.Threading.RateLimiting;
using Datos;
using Entidades;
using Logica;
using Microsoft.AspNetCore.RateLimiting;
using Servidor;

var configuracion = ConfiguracionServidor.Cargar();
var cadena = configuracion.BaseDatos
    ?? throw new InvalidOperationException($"Falta la conexión a la base de datos: configure ACADEMICO_BD o {ConfiguracionServidor.Ruta}.");

// «Servidor --migrar» prepara o actualiza la base de datos con una cuenta con permisos DDL y termina.
if (args.Contains("--migrar"))
{
    var migracion = new Migrador(new BaseDatos(configuracion.BaseDatosMigracion ?? cadena));
    var aplicadas = await migracion.Aplicar();
    Console.WriteLine(aplicadas.Count == 0 ? $"La base de datos ya está en la versión {Migrador.VersionEsperada}."
        : $"Migraciones aplicadas: {string.Join(", ", aplicadas)}. Versión actual: {Migrador.VersionEsperada}.");
    return;
}

var constructor = WebApplication.CreateBuilder(args);
// Funciona como servicio de Windows o desde una consola.
constructor.Host.UseWindowsService(opciones => opciones.ServiceName = "Merito");
// El origen lo registra la instalación: la cuenta virtual del servicio no puede crearlo.
if (OperatingSystem.IsWindows())
    constructor.Services.Configure<Microsoft.Extensions.Logging.EventLog.EventLogSettings>(opciones =>
    {
        if (OperatingSystem.IsWindows()) opciones.SourceName = "Merito";
    });
constructor.WebHost.ConfigureKestrel(opciones => opciones.Limits.MaxRequestBodySize = 2 * 1024 * 1024);
if (configuracion.Urls is { } urls) constructor.WebHost.UseUrls(urls);
var baseDatos = new BaseDatos(cadena);
constructor.Services.AddSingleton<IAlmacen>(baseDatos);
constructor.Services.AddSingleton<IEnviadorCorreo>(proveedor => Correos.Crear(configuracion.Correo, configuracion.CarpetaCorreoPruebas,
    proveedor.GetRequiredService<ILoggerFactory>()));
constructor.Services.AddSingleton<ServicioIdentidad>();
constructor.Services.AddSingleton<ServicioAcademico>();
constructor.Services.AddSingleton<ServicioCalificaciones>();
constructor.Services.AddSingleton<ServicioMatricula>();
constructor.Services.AddSingleton<ServicioRecord>();
constructor.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = 429;
    opciones.AddPolicy("acceso", contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
constructor.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.MaxDepth = 20);
var app = constructor.Build();
var exigirHttps = configuracion.ExigirHttps;
// Al arrancar queda dicho en el registro si hay correo, para saber si «¿Primera vez?» funcionará. La clave nunca se escribe.
if (configuracion.AvisoCorreo is { } avisoCorreo) app.Logger.LogWarning("{Aviso}", avisoCorreo);
else if (configuracion.Correo is { } cuentaCorreo) app.Logger.LogInformation("Correo configurado: {Usuario} por {Servidor}:{Puerto}.", cuentaCorreo.Usuario, cuentaCorreo.Servidor, cuentaCorreo.Puerto);
else app.Logger.LogInformation("Sin cuenta de correo: completa {Archivo} para el registro por correo.", ConfiguracionServidor.RutaCorreo);

app.Use(async (contexto, siguiente) =>
{
    contexto.Response.Headers.CacheControl = "no-store";
    contexto.Response.Headers.XContentTypeOptions = "nosniff";
    var origen = contexto.Connection.RemoteIpAddress;
    // HTTP solo dentro de la red local; desde fuera, o si la institución lo exige, únicamente HTTPS.
    if (!contexto.Request.IsHttps && (exigirHttps || origen is null || !RedLocal.EsPrivada(origen)))
    {
        contexto.Response.StatusCode = 400;
        await contexto.Response.WriteAsJsonAsync(new ErrorApi("Esta conexión requiere HTTPS.", contexto.TraceIdentifier));
        return;
    }
    try { await siguiente(contexto); }
    catch (OperationCanceledException) when (contexto.RequestAborted.IsCancellationRequested) {}
    catch (ReglaException error) { await Responder(contexto, ErroresHttp.Estado(error.Tipo), error.Message); }
    // Un cuerpo que no es JSON válido, campos con el tipo equivocado o un archivo demasiado grande: es un error de quien llama, no del servidor.
    catch (BadHttpRequestException error) when (!contexto.Response.HasStarted)
    {
        await Responder(contexto, error.StatusCode == 413 ? 413 : 400, error.StatusCode == 413 ? "El archivo o los datos enviados son demasiado grandes."
            : "La solicitud no tiene el formato esperado. Actualiza Mérito en este equipo si el problema continúa.");
    }
    catch (ConflictoDatosException) { await Responder(contexto, 409, "Los datos están repetidos, no cumplen una regla o hacen referencia a un registro inexistente."); }
    catch (DatosNoDisponiblesException error)
    {
        app.Logger.LogWarning("Operación de datos fallida {Detalle}; referencia {Referencia}", error.InnerException?.Message, contexto.TraceIdentifier);
        await Responder(contexto, 503, "No se pudo confirmar la operación. Recarga para comprobar el estado antes de reintentar.");
    }
    catch (Exception error)
    {
        app.Logger.LogError(error, "Fallo inesperado; referencia {Referencia}", contexto.TraceIdentifier);
        await Responder(contexto, 500, "No se pudo completar la operación. Conserva la referencia para administración.");
    }
});
app.UseRateLimiter();
app.Use(async (contexto, siguiente) =>
{
    if (contexto.Request.Path is { Value: "/salud" or "/acceso" or "/activar" or "/configuracion" or "/registro/codigo" or "/registro/confirmar" }) { await siguiente(contexto); return; }
    var encabezado = contexto.Request.Headers.Authorization.ToString();
    var token = encabezado.StartsWith("Bearer ", StringComparison.Ordinal) ? encabezado[7..] : "";
    var persona = await contexto.RequestServices.GetRequiredService<ServicioIdentidad>().Resolver(token, contexto.RequestAborted);
    if (persona is null) throw new ReglaException("La sesión venció. Inicia sesión de nuevo.", TipoErrorRegla.NoAutenticado);
    contexto.Items["persona"] = persona;
    contexto.Items["token"] = token;
    await siguiente(contexto);
});



app.MapGet("/salud", () => Results.Ok(new { servicio = "merito", version = Migrador.VersionEsperada }));
app.MapPost("/acceso", (Acceso acceso, ServicioIdentidad s, CancellationToken ct) => s.Iniciar(acceso, ct)).RequireRateLimiting("acceso");
app.MapPost("/activar", async (ActivarCuenta solicitud, ServicioIdentidad s, CancellationToken ct) =>
{ await s.Activar(solicitud, ct); return Results.NoContent(); }).RequireRateLimiting("acceso");
app.MapGet("/configuracion", (ServicioIdentidad s, CancellationToken ct) => s.EstadoConfiguracion(ct));
// Registro la primera vez y recuperación de contraseña con un código que llega al correo de la matrícula.
app.MapPost("/registro/codigo", (SolicitarCodigo solicitud, ServicioIdentidad s, CancellationToken ct) => s.EnviarCodigoCorreo(solicitud, ct)).RequireRateLimiting("acceso");
app.MapPost("/registro/confirmar", async (ConfirmarCorreo confirmacion, ServicioIdentidad s, CancellationToken ct) =>
{ await s.ConfirmarCodigoCorreo(confirmacion, ct); return Results.NoContent(); }).RequireRateLimiting("acceso");
app.MapPost("/configuracion", (ConfiguracionInicial solicitud, ServicioIdentidad s, CancellationToken ct) => s.ConfigurarInicial(solicitud, ct)).RequireRateLimiting("acceso");
app.MapGet("/sesion", (HttpContext c) => Actual(c));
app.MapPost("/salir", async (HttpContext c, ServicioIdentidad s, CancellationToken ct) =>
{ await s.Salir((string)c.Items["token"]!, ct); return Results.NoContent(); });
app.MapPost("/clave", async (CambiarClave cambio, HttpContext c, ServicioIdentidad s, CancellationToken ct) =>
{ await s.Cambiar(Actual(c), cambio, ct); return Results.NoContent(); });

app.MapGet("/usuarios", (string? buscar, int? desde, int? cantidad, string? rol, HttpContext c, ServicioIdentidad s, CancellationToken ct) =>
    s.Personas(Actual(c), ConsultaPagina.Crear(buscar, desde, cantidad), ct, rol));
app.MapGet("/nomina-docente", (string? buscar, int? desde, int? cantidad, HttpContext c, ServicioIdentidad s, CancellationToken ct) =>
    s.NominaDocente(Actual(c), ConsultaPagina.Crear(buscar, desde, cantidad), ct));
app.MapGet("/estudiantes", (string? buscar, int? desde, int? cantidad, long? periodo, long? seccion, HttpContext c, ServicioIdentidad s, CancellationToken ct) =>
    s.Estudiantes(Actual(c), ConsultaPagina.Crear(buscar, desde, cantidad), periodo, seccion, ct));
app.MapPost("/nomina-docente/leer-excel", async (HttpRequest peticion, HttpContext c, ServicioIdentidad s, CancellationToken ct) =>
{
    Permisos.SoloAdministrador(Actual(c));
    ServicioMatricula.ValidarTamano(peticion.ContentLength);
    using var archivo = new MemoryStream();
    await peticion.Body.CopyToAsync(archivo, ct);
    return await s.RevisarNomina(Actual(c), LectorNomina.Leer(archivo), ct);
});
app.MapPost("/nomina-docente/importar", (ImportarNomina nomina, HttpContext c, ServicioIdentidad s, CancellationToken ct) => s.ImportarNomina(Actual(c), nomina, ct));
app.MapGet("/plantilla-nomina", (HttpContext c) =>
{
    Permisos.SoloAdministrador(Actual(c));
    return Results.File(LectorNomina.Plantilla(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "nomina-docente.xlsx");
});
app.MapGet("/docentes", (HttpContext c, ServicioIdentidad s, CancellationToken ct) => s.DocentesActivos(Actual(c), ct));
app.MapPost("/usuarios", (NuevaPersona nueva, HttpContext c, ServicioIdentidad s, CancellationToken ct) => s.Crear(Actual(c), nueva, ct));
app.MapPost("/ofertas/{id:long}/codigos", (long id, CodigosSeccion solicitud, HttpContext c, ServicioIdentidad s, CancellationToken ct) => s.CodigosDeSeccion(Actual(c), id, solicitud, ct));
app.MapPost("/usuarios/{id:long}/codigo", (long id, HttpContext c, ServicioIdentidad s, CancellationToken ct) => s.EmitirCodigo(Actual(c), id, ct));
app.MapPut("/usuarios/{id:long}/correo", (long id, CambioCorreo cambio, HttpContext c, ServicioIdentidad s, CancellationToken ct) => s.CambiarCorreo(Actual(c), id, cambio, ct));
app.MapPost("/correo/prueba", async (PruebaCorreo prueba, HttpContext c, ServicioIdentidad s, CancellationToken ct) =>
{ await s.ProbarCorreo(Actual(c), prueba, ct); return Results.NoContent(); });
app.MapPut("/usuarios/{id:long}/estado", (long id, EstadoCuenta estado, HttpContext c, ServicioIdentidad s, CancellationToken ct) => s.CambiarEstado(Actual(c), id, estado.Activo, ct));
app.MapGet("/auditoria", (string? buscar, int? desde, int? cantidad, HttpContext c, ServicioIdentidad s, CancellationToken ct) =>
    s.Auditoria(Actual(c), ConsultaPagina.Crear(buscar, desde, cantidad), ct));

app.MapGet("/periodos", (HttpContext c, ServicioAcademico s, CancellationToken ct) => s.Periodos(Actual(c), ct));
app.MapGet("/catalogos/{tipo}", (string tipo, HttpContext c, ServicioAcademico s, CancellationToken ct) => s.Referencias(Actual(c), tipo, ct));
app.MapPost("/catalogos/{tipo}", (string tipo, Referencia nueva, HttpContext c, ServicioAcademico s, CancellationToken ct) => s.CrearReferencia(Actual(c), tipo, nueva.Nombre, ct));
app.MapPut("/catalogos/{tipo}/{id:long}", (string tipo, long id, CambioNombre cambio, HttpContext c, ServicioAcademico s, CancellationToken ct) => s.RenombrarReferencia(Actual(c), tipo, id, cambio, ct));
app.MapGet("/materias", (HttpContext c, ServicioAcademico s, CancellationToken ct) => s.Materias(Actual(c), ct));
app.MapPost("/materias", (NuevaMateria nueva, long? carrera, int? semestre, HttpContext c, ServicioAcademico s, CancellationToken ct) =>
    s.CrearMateria(Actual(c), nueva, carrera, semestre ?? 0, ct));
app.MapGet("/pensum", (long? carrera, HttpContext c, ServicioAcademico s, CancellationToken ct) => s.Pensum(Actual(c), carrera, ct));
app.MapPut("/carreras/{id:long}/pensum", (long id, EnPensum ubicacion, HttpContext c, ServicioAcademico s, CancellationToken ct) => s.UbicarEnPensum(Actual(c), id, ubicacion, ct));
app.MapDelete("/carreras/{id:long}/pensum/{materia:long}", (long id, long materia, HttpContext c, ServicioAcademico s, CancellationToken ct) => s.QuitarDelPensum(Actual(c), id, materia, ct));
app.MapPost("/carreras/{id:long}/pensum/leer-excel", async (long id, HttpRequest peticion, HttpContext c, ServicioAcademico s, CancellationToken ct) =>
{
    Permisos.SoloAdministrador(Actual(c));
    ServicioMatricula.ValidarTamano(peticion.ContentLength);
    using var archivo = new MemoryStream();
    await peticion.Body.CopyToAsync(archivo, ct);
    return await s.RevisarPensum(Actual(c), id, LectorPensum.Leer(archivo), ct);
});
app.MapPost("/carreras/{id:long}/pensum/importar", (long id, ImportarPensum solicitud, HttpContext c, ServicioAcademico s, CancellationToken ct) => s.ImportarPensum(Actual(c), id, solicitud, ct));
app.MapGet("/plantilla-pensum", (HttpContext c) =>
{
    Permisos.SoloAdministrador(Actual(c));
    return Results.File(LectorPensum.Plantilla(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "pensum.xlsx");
});
app.MapGet("/reglas", (HttpContext c, ServicioAcademico s, CancellationToken ct) => s.Reglas(Actual(c), ct));
app.MapPut("/reglas", (ReglasCalificacion cambio, HttpContext c, ServicioAcademico s, CancellationToken ct) => s.CambiarReglas(Actual(c), cambio, ct));
app.MapPut("/materias/{id:long}", (long id, NuevaMateria cambio, HttpContext c, ServicioAcademico s, CancellationToken ct) => s.ActualizarMateria(Actual(c), id, cambio, ct));
app.MapGet("/secciones", (HttpContext c, ServicioAcademico s, CancellationToken ct) => s.Secciones(Actual(c), ct));
app.MapPut("/secciones/{id:long}", (long id, CambioSeccion cambio, HttpContext c, ServicioAcademico s, CancellationToken ct) => s.ActualizarSeccion(Actual(c), id, cambio, ct));
app.MapGet("/carreras", (HttpContext c, ServicioAcademico s, CancellationToken ct) => s.Carreras(Actual(c), ct));
app.MapPost("/carreras", (NuevaCarrera nueva, HttpContext c, ServicioAcademico s, CancellationToken ct) => s.CrearCarrera(Actual(c), nueva, ct));
app.MapPut("/carreras/{id:long}", (long id, NuevaCarrera cambio, HttpContext c, ServicioAcademico s, CancellationToken ct) => s.ActualizarCarrera(Actual(c), id, cambio, ct));
app.MapGet("/formato-seccion", async (HttpContext c, ServicioAcademico s, CancellationToken ct) => new FormatoCodigo(await s.FormatoSeccion(Actual(c), ct)));
app.MapPut("/formato-seccion", (FormatoCodigo cambio, HttpContext c, ServicioAcademico s, CancellationToken ct) => s.CambiarFormatoSeccion(Actual(c), cambio, ct));
app.MapGet("/periodos/{periodo:long}/secciones/{seccion:long}/carga", (long periodo, long seccion, HttpContext c, ServicioAcademico s, CancellationToken ct) =>
    s.CargaDeSeccion(Actual(c), periodo, seccion, ct));
app.MapPost("/periodos/{id:long}/copiar-carga", (long id, CopiaDeCarga copia, HttpContext c, ServicioAcademico s, CancellationToken ct) => s.CopiarCarga(Actual(c), id, copia, ct));
app.MapPost("/ofertas/lote", (CargaEnBloque carga, HttpContext c, ServicioAcademico s, CancellationToken ct) => s.AsignarEnBloque(Actual(c), carga, ct));
app.MapPost("/ofertas/{id:long}/retirar", (long id, RevisionEsperada revision, HttpContext c, ServicioAcademico s, CancellationToken ct) => s.RetirarOferta(Actual(c), id, revision.Revision, ct));
app.MapPost("/secciones", (NuevaSeccion nueva, HttpContext c, ServicioAcademico s, CancellationToken ct) => s.CrearSeccion(Actual(c), nueva, ct));
app.MapGet("/ofertas", (string? buscar, int? desde, int? cantidad, HttpContext c, ServicioAcademico s, CancellationToken ct) =>
    s.Ofertas(Actual(c), ConsultaPagina.Crear(buscar, desde, cantidad), ct));
app.MapPost("/ofertas", (NuevaOferta nueva, HttpContext c, ServicioAcademico s, CancellationToken ct) => s.CrearOferta(Actual(c), nueva, ct));
app.MapPut("/ofertas/{id:long}/docente", (long id, CambioDocente cambio, HttpContext c, ServicioAcademico s, CancellationToken ct) => s.Reasignar(Actual(c), id, cambio, ct));
app.MapGet("/ofertas/{id:long}", (long id, HttpContext c, ServicioAcademico s, CancellationToken ct) => s.Planilla(Actual(c), id, ct));
app.MapGet("/periodos/{id:long}/cierre", (long id, HttpContext c, ServicioAcademico s, CancellationToken ct) => s.RevisarCierre(Actual(c), id, ct));
app.MapPost("/periodos/{id:long}/cerrar", (long id, HttpContext c, ServicioAcademico s, CancellationToken ct) => s.CerrarPeriodo(Actual(c), id, ct));

app.MapPost("/ofertas/{id:long}/evaluaciones", (long id, NuevaEvaluacion nueva, HttpContext c, ServicioCalificaciones s, CancellationToken ct) => s.CrearEvaluacion(Actual(c), id, nueva, ct));
app.MapPut("/ofertas/{id:long}/evaluaciones/{evaluacion:long}", (long id, long evaluacion, NuevaEvaluacion cambio, HttpContext c, ServicioCalificaciones s, CancellationToken ct) => s.EditarEvaluacion(Actual(c), id, evaluacion, cambio, ct));
app.MapPost("/ofertas/{id:long}/evaluaciones/{evaluacion:long}/eliminar", (long id, long evaluacion, RevisionEsperada revision, HttpContext c, ServicioCalificaciones s, CancellationToken ct) => s.EliminarEvaluacion(Actual(c), id, evaluacion, revision.Revision, ct));
app.MapPut("/ofertas/{id:long}/notas", (long id, GuardarNotas notas, HttpContext c, ServicioCalificaciones s, CancellationToken ct) => s.GuardarNotas(Actual(c), id, notas, ct));
app.MapPost("/ofertas/{id:long}/publicar", (long id, RevisionEsperada revision, HttpContext c, ServicioCalificaciones s, CancellationToken ct) => s.Publicar(Actual(c), id, revision.Revision, ct));

app.MapPost("/ofertas/{id:long}/importar", (long id, ImportarMatricula matricula, HttpContext c, ServicioMatricula s, CancellationToken ct) => s.Importar(Actual(c), id, matricula, ct));
app.MapPost("/ofertas/{id:long}/confirmar", (long id, ConfirmarMatricula matricula, HttpContext c, ServicioMatricula s, CancellationToken ct) => s.Confirmar(Actual(c), id, matricula, ct));
app.MapPost("/ofertas/{id:long}/rechazar", (long id, ConfirmarMatricula matricula, HttpContext c, ServicioMatricula s, CancellationToken ct) => s.Rechazar(Actual(c), id, matricula, ct));
app.MapPost("/ofertas/{id:long}/retirar-estudiantes", (long id, ConfirmarMatricula matricula, HttpContext c, ServicioMatricula s, CancellationToken ct) => s.Retirar(Actual(c), id, matricula, ct));
// Matrícula del período: un solo Excel con todas las secciones. Primero se revisa, después se importa lo revisado.
app.MapPost("/periodos/{id:long}/matricula/leer-excel", async (long id, HttpRequest peticion, HttpContext c, ServicioMatricula s, CancellationToken ct) =>
{
    Permisos.SoloAdministrador(Actual(c));
    ServicioMatricula.ValidarTamano(peticion.ContentLength);
    using var archivo = new MemoryStream();
    await peticion.Body.CopyToAsync(archivo, ct);
    return await s.RevisarPeriodo(Actual(c), id, LectorMatricula.Leer(archivo, ServicioMatricula.MaximoFilasPeriodo), ct);
});
app.MapPost("/periodos/{id:long}/matricula", (long id, ImportarMatriculaPeriodo matricula, HttpContext c, ServicioMatricula s, CancellationToken ct) => s.ImportarPeriodo(Actual(c), id, matricula, ct));
app.MapPost("/ofertas/{id:long}/leer-excel", async (long id, HttpRequest peticion, HttpContext c, ServicioAcademico academico, CancellationToken ct) =>
{
    await academico.Planilla(Actual(c), id, ct);
    ServicioMatricula.ValidarTamano(peticion.ContentLength);
    using var archivo = new MemoryStream();
    await peticion.Body.CopyToAsync(archivo, ct);
    return ServicioMatricula.Revisar(LectorMatricula.Leer(archivo));
});
app.MapGet("/plantilla-matricula", (HttpContext c) =>
{
    Permisos.NoEstudiante(Actual(c));
    return Results.File(LectorMatricula.Plantilla(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "matricula.xlsx");
});

app.MapGet("/record", (HttpContext c, ServicioRecord s, CancellationToken ct) => s.Record(Actual(c), ct));
if (configuracion.Migrar)
    await new Migrador(new BaseDatos(configuracion.BaseDatosMigracion ?? cadena)).Aplicar();
var version = await new Migrador(baseDatos).VersionActual();
if (version != Migrador.VersionEsperada)
    throw new InvalidOperationException($"La base de datos está en la versión {version} y este servidor requiere la {Migrador.VersionEsperada}. Ejecute «Servidor --migrar».");
await app.Services.GetRequiredService<ServicioIdentidad>().PrepararAdministrador(configuracion.AdminCedula, configuracion.AdminClave, CancellationToken.None);
await app.RunAsync();

static Persona Actual(HttpContext contexto) => (Persona)contexto.Items["persona"]!;

static Task Responder(HttpContext contexto, int estado, string mensaje)
{
    contexto.Response.StatusCode = estado;
    return contexto.Response.WriteAsJsonAsync(new ErrorApi(mensaje, contexto.TraceIdentifier));
}
