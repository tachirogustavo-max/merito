using Entidades;
using Logica;
using MySqlConnector;

namespace Datos;

/// <summary>Operaciones SQL de una transacción. No contiene reglas: solo lee y escribe.</summary>
public sealed partial class ContextoDatos(UnidadDatos d) : IDatos
{
    private static Persona PersonaDesde(MySqlDataReader fila) => new(fila.GetInt64("id"), fila.GetString("cedula"),
        fila.GetString("nombres"), fila.GetString("apellidos"), fila.GetString("rol"), fila.GetBoolean("activo"),
        fila.IsDBNull(fila.GetOrdinal("correo")) ? null : fila.GetString("correo"), !fila.IsDBNull(fila.GetOrdinal("clave_hash")));

    public Task Auditar(long actor, string accion, object referencia) => d.Ejecutar(
        "INSERT INTO auditoria (actor_id,accion,referencia) VALUES (@actor,@accion,@referencia)",
        ("@actor", actor), ("@accion", accion), ("@referencia", referencia.ToString()));

    public Task<long> ContarAdministradores() => d.Escalar<long>("SELECT COUNT(*) FROM usuarios WHERE rol='administrador'");

    public async Task<bool> HayCodigoConfiguracion() => await d.Escalar<long>("SELECT COUNT(*) FROM codigo_configuracion") > 0;

    public Task<int> ConsumirCodigoConfiguracion(string huella) =>
        d.Ejecutar("DELETE FROM codigo_configuracion WHERE huella=@huella", ("@huella", huella));

    public Task<long> InsertarUsuario(string cedula, string nombres, string apellidos, string rol, string? hash) => d.Insertar(
        "INSERT INTO usuarios (cedula,nombres,apellidos,rol,clave_hash) VALUES (@cedula,@nombres,@apellidos,@rol,@hash)",
        ("@cedula", cedula), ("@nombres", nombres), ("@apellidos", apellidos), ("@rol", rol), ("@hash", hash));

    public async Task<CuentaGuardada?> CuentaPorCedula(string cedula) => (await d.Consultar("SELECT * FROM usuarios WHERE cedula=@cedula",
        f => new CuentaGuardada(PersonaDesde(f), f.IsDBNull(f.GetOrdinal("clave_hash")) ? null : f.GetString("clave_hash")),
        ("@cedula", cedula))).FirstOrDefault();

    public Task<string?> BloquearClave(long usuario, bool soloActiva) => d.Escalar<string?>(
        "SELECT clave_hash FROM usuarios WHERE id=@id AND (@todas OR activo=TRUE) FOR UPDATE", ("@id", usuario), ("@todas", !soloActiva));

    public Task EliminarSesionesVencidas() => d.Ejecutar("DELETE FROM sesiones WHERE vence_en < UTC_TIMESTAMP(6)");

    public Task InsertarSesion(string huella, long usuario, DateTime vence) => d.Ejecutar(
        "INSERT INTO sesiones (huella,usuario_id,vence_en) VALUES (@huella,@id,@vence)", ("@huella", huella), ("@id", usuario), ("@vence", vence));

    public async Task<Persona?> PersonaPorSesion(string huella) => (await d.Consultar("""
        SELECT u.* FROM sesiones s JOIN usuarios u ON u.id=s.usuario_id
        WHERE s.huella=@huella AND s.vence_en>UTC_TIMESTAMP(6) AND u.activo=TRUE
        """, PersonaDesde, ("@huella", huella))).FirstOrDefault();

    public Task EliminarSesion(string huella) => d.Ejecutar("DELETE FROM sesiones WHERE huella=@huella", ("@huella", huella));
    public Task ActualizarClave(long usuario, string hash) => d.Ejecutar("UPDATE usuarios SET clave_hash=@hash WHERE id=@id", ("@hash", hash), ("@id", usuario));
    public Task EliminarSesionesDe(long usuario) => d.Ejecutar("DELETE FROM sesiones WHERE usuario_id=@id", ("@id", usuario));
    public Task EliminarCodigosDe(long usuario) =>
            d.Ejecutar("DELETE FROM codigos_acceso WHERE usuario_id=@id", ("@id", usuario));

    private const string FiltroPersonas = "WHERE (@rol IS NULL OR rol=@rol) AND (@buscar='' OR cedula LIKE @patron OR nombres LIKE @patron OR apellidos LIKE @patron OR CONCAT(nombres,' ',apellidos) LIKE @patron OR correo LIKE @patron)";

    public async Task<Pagina<FilaDocente>> NominaDocente(ConsultaPagina consulta)
    {
        (string, object?)[] filtro = [("@rol", Roles.Docente), ("@buscar", consulta.Buscar), ("@patron", consulta.Patron)];
        var total = await d.Escalar<int>($"SELECT COUNT(*) FROM usuarios {FiltroPersonas}", filtro);
        // Materias que dicta en períodos abiertos.
        var docentes = await d.Consultar($"""
            SELECT u.*,(SELECT COUNT(*) FROM ofertas o JOIN periodos p ON p.id=o.periodo_id WHERE o.docente_id=u.id AND p.cerrado=FALSE) materias
            FROM usuarios u {FiltroPersonas} ORDER BY apellidos,nombres,id LIMIT @desde,@cantidad
            """, f => new FilaDocente(PersonaDesde(f), f.GetInt32("materias")), [.. filtro, ("@desde", consulta.Desde), ("@cantidad", consulta.Cantidad)]);
        return new Pagina<FilaDocente>(docentes, total, consulta.Desde, consulta.Cantidad);
    }

    // Los estudiantes con sus secciones y materias en el período; con sección, solo los inscritos en ella.
    private const string ConsultaEstudiantes = """
        SELECT u.*,COALESCE(GROUP_CONCAT(DISTINCT s.codigo ORDER BY s.codigo SEPARATOR ', '),'') secciones,COUNT(i.id) materias
        FROM usuarios u
        LEFT JOIN inscripciones i ON i.estudiante_id=u.id AND i.periodo_id=@periodo AND i.estado<>'retirada'
        LEFT JOIN ofertas o ON o.id=i.oferta_id LEFT JOIN secciones s ON s.id=o.seccion_id
        WHERE u.rol='estudiante' AND (@buscar='' OR u.cedula LIKE @patron OR CONCAT(u.nombres,' ',u.apellidos) LIKE @patron OR u.correo LIKE @patron)
        GROUP BY u.id
        HAVING @seccion IS NULL OR SUM(o.seccion_id=@seccion)>0
        """;

    public async Task<Pagina<FilaEstudiante>> Estudiantes(ConsultaPagina consulta, long? periodo, long? seccion)
    {
        (string, object?)[] filtro = [("@periodo", periodo), ("@seccion", seccion), ("@buscar", consulta.Buscar), ("@patron", consulta.Patron)];
        var total = await d.Escalar<int>($"SELECT COUNT(*) FROM ({ConsultaEstudiantes}) e", filtro);
        var estudiantes = await d.Consultar($"{ConsultaEstudiantes} ORDER BY u.apellidos,u.nombres,u.id LIMIT @desde,@cantidad",
            f => new FilaEstudiante(PersonaDesde(f), f.GetString("secciones"), f.GetInt32("materias")),
            [.. filtro, ("@desde", consulta.Desde), ("@cantidad", consulta.Cantidad)]);
        return new Pagina<FilaEstudiante>(estudiantes, total, consulta.Desde, consulta.Cantidad);
    }

    public async Task<Pagina<Persona>> Personas(ConsultaPagina consulta, string? rol = null)
    {
        (string, object?)[] filtro = [("@rol", rol), ("@buscar", consulta.Buscar), ("@patron", consulta.Patron)];
        var total = await d.Escalar<int>($"SELECT COUNT(*) FROM usuarios {FiltroPersonas}", filtro);
        var personas = await d.Consultar($"SELECT * FROM usuarios {FiltroPersonas} ORDER BY rol,apellidos,nombres,id LIMIT @desde,@cantidad",
            PersonaDesde, [.. filtro, ("@desde", consulta.Desde), ("@cantidad", consulta.Cantidad)]);
        return new Pagina<Persona>(personas, total, consulta.Desde, consulta.Cantidad);
    }

    public Task<List<Persona>> DocentesActivos() => d.Consultar(
        "SELECT * FROM usuarios WHERE rol='docente' AND activo=TRUE ORDER BY apellidos,nombres,id", PersonaDesde);

    public async Task<bool> BloquearUsuario(long usuario, bool soloActivo) => await d.Escalar<long>(
        "SELECT id FROM usuarios WHERE id=@id AND (@todos OR activo=TRUE) FOR UPDATE", ("@id", usuario), ("@todos", !soloActivo)) != 0;

    public Task<long> BloquearUsuarioActivoPorCedula(string cedula) => d.Escalar<long>(
        "SELECT id FROM usuarios WHERE cedula=@cedula AND activo=TRUE FOR UPDATE", ("@cedula", cedula));

    public Task InsertarCodigo(string huella, long usuario, DateTime vence) => d.Ejecutar(
        "INSERT INTO codigos_acceso (huella,usuario_id,vence_en) VALUES (@huella,@id,@vence)", ("@huella", huella), ("@id", usuario), ("@vence", vence));

    public async Task<bool> CodigoVigente(long usuario, string huella) => await d.Escalar<long>(
        "SELECT COUNT(*) FROM codigos_acceso WHERE usuario_id=@id AND huella=@huella AND vence_en>UTC_TIMESTAMP(6)",
        ("@id", usuario), ("@huella", huella)) == 1;

    public Task<List<long>> BloquearAdministradoresActivos() => d.Consultar(
        "SELECT id FROM usuarios WHERE rol='administrador' AND activo=TRUE ORDER BY id FOR UPDATE", f => f.GetInt64(0));

    public Task ActualizarActivo(long usuario, bool activo) => d.Ejecutar("UPDATE usuarios SET activo=@activo WHERE id=@id", ("@activo", activo), ("@id", usuario));

    // La referencia guarda ids («oferta», «oferta:docente», «carrera:materia»…): aquí se cambian por nombres.
    // Lo que no cambia datos (inicios de sesión, códigos enviados, correos de prueba) queda guardado pero no se lista.
    private const string ConsultaHistorial = """
        SELECT a.id,CONCAT(u.nombres,' ',u.apellidos) actor,a.accion,a.referencia,a.ocurrido_en,
            CONCAT(m.nombre,', sección ',s.codigo,', ',po.nombre) oferta,CONCAT(pu.nombres,' ',pu.apellidos) persona,
            pe.nombre periodo,ca.nombre carrera,CONCAT(ma.codigo,' ',ma.nombre) materia,se.codigo seccion
        FROM (SELECT a.*,CAST(SUBSTRING_INDEX(a.referencia,':',1) AS SIGNED) r1,
                CAST(SUBSTRING_INDEX(SUBSTRING_INDEX(a.referencia,':',2),':',-1) AS SIGNED) r2 FROM auditoria a
              WHERE a.accion NOT IN ('inicio_sesion','codigo_correo_enviado','prueba_correo','acceso_establecido','cambio_clave',
                'registro_por_correo','clave_recuperada_por_correo')) a
        JOIN usuarios u ON u.id=a.actor_id
        LEFT JOIN ofertas o ON o.id=CASE
            WHEN a.accion IN ('notas_guardadas','matricula_importada','matricula_confirmada','matricula_propuesta_rechazada','matricula_retirada',
                'codigos_seccion','asignacion_creada','docente_reasignado') THEN a.r1
            WHEN a.accion='notas_publicadas' THEN (SELECT oferta_id FROM publicaciones WHERE id=a.r1)
            WHEN a.accion IN ('evaluacion_creada','evaluacion_editada') THEN (SELECT oferta_id FROM evaluaciones WHERE id=a.r1) END
        LEFT JOIN materias m ON m.id=o.materia_id LEFT JOIN secciones s ON s.id=o.seccion_id LEFT JOIN periodos po ON po.id=o.periodo_id
        LEFT JOIN usuarios pu ON pu.id=CASE WHEN a.accion IN ('cuenta_creada','correo_corregido','codigo_acceso_emitido','cuenta_activada','cuenta_desactivada') THEN a.r1 END
        LEFT JOIN periodos pe ON pe.id=CASE WHEN a.accion IN ('periodo_cerrado','matricula_periodo_importada','alta_periodos','renombre_periodos') THEN a.r1
            WHEN a.accion='carga_copiada' THEN a.r2 END
        LEFT JOIN carreras ca ON ca.id=CASE WHEN a.accion IN ('alta_carreras','carrera_corregida','renombre_carreras','pensum_actualizado','pensum_quitada','pensum_importado') THEN a.r1 END
        LEFT JOIN materias ma ON ma.id=CASE WHEN a.accion IN ('alta_materia','materia_corregida') THEN a.r1 WHEN a.accion IN ('pensum_actualizado','pensum_quitada') THEN a.r2 END
        LEFT JOIN secciones se ON se.id=CASE WHEN a.accion IN ('alta_seccion','seccion_corregida') THEN a.r1 END
        """;

    private const string FiltroHistorial = """
         WHERE (@buscar='' OR h.actor LIKE @patron OR h.accion LIKE @patron OR h.oferta LIKE @patron OR h.persona LIKE @patron
            OR h.periodo LIKE @patron OR h.carrera LIKE @patron OR h.materia LIKE @patron OR h.seccion LIKE @patron)
        """;

    public async Task<Pagina<RegistroHistorial>> Auditoria(ConsultaPagina consulta)
    {
        (string, object?)[] filtro = [("@buscar", consulta.Buscar), ("@patron", consulta.Patron)];
        var total = await d.Escalar<int>($"SELECT COUNT(*) FROM ({ConsultaHistorial}) h {FiltroHistorial}", filtro);
        string? Texto(MySqlConnector.MySqlDataReader f, int i) => f.IsDBNull(i) ? null : f.GetString(i);
        var eventos = await d.Consultar($"SELECT h.* FROM ({ConsultaHistorial}) h {FiltroHistorial} ORDER BY h.id DESC LIMIT @desde,@cantidad",
            f => new RegistroHistorial(f.GetInt64(0), f.GetString(1), f.GetString(2), f.GetString(3), f.GetDateTime(4),
                Texto(f, 5), Texto(f, 6), Texto(f, 7), Texto(f, 8), Texto(f, 9), Texto(f, 10)),
            [.. filtro, ("@desde", consulta.Desde), ("@cantidad", consulta.Cantidad)]);
        return new Pagina<RegistroHistorial>(eventos, total, consulta.Desde, consulta.Cantidad);
    }
}
