using Entidades;
using Logica;
using MySqlConnector;

namespace Datos;

public sealed partial class ContextoDatos
{
    private const string ConsultaOfertas = """
        SELECT o.id,o.periodo_id,p.nombre periodo,c.nombre carrera,s.codigo seccion,m.nombre materia,
            o.docente_id,CONCAT(u.nombres,' ',u.apellidos) docente,o.revision,p.cerrado,
            (SELECT COUNT(*) FROM inscripciones i WHERE i.oferta_id=o.id AND i.estado='confirmada') matriculados,
            (SELECT COUNT(*) FROM inscripciones i WHERE i.oferta_id=o.id AND i.estado='pendiente') pendientes,
            (SELECT MAX(pub.revision) FROM publicaciones pub WHERE pub.oferta_id=o.id) revision_publicada
        FROM ofertas o JOIN periodos p ON p.id=o.periodo_id JOIN secciones s ON s.id=o.seccion_id
        JOIN carreras c ON c.id=s.carrera_id JOIN materias m ON m.id=o.materia_id JOIN usuarios u ON u.id=o.docente_id
        """;

    private static Oferta OfertaDesde(MySqlDataReader f) => new(f.GetInt64("id"), f.GetInt64("periodo_id"),
        f.GetString("periodo"), f.GetString("carrera"), f.GetString("seccion"), f.GetString("materia"),
        f.GetInt64("docente_id"), f.GetString("docente"), f.GetInt32("revision"), f.GetBoolean("cerrado"),
        f.GetInt32("matriculados"), f.GetInt32("pendientes"), f.IsDBNull(f.GetOrdinal("revision_publicada")) ? null : f.GetInt32("revision_publicada"));

    private static string Tabla(Catalogo catalogo) => catalogo == Catalogo.Carreras ? "carreras" : "periodos";

    public Task<List<Periodo>> Periodos() => d.Consultar("SELECT id,nombre,cerrado FROM periodos ORDER BY id DESC",
        f => new Periodo(f.GetInt64(0), f.GetString(1), f.GetBoolean(2)));

    public Task<List<Referencia>> Referencias(Catalogo catalogo) => d.Consultar(
        $"SELECT id,nombre FROM {Tabla(catalogo)} ORDER BY nombre", f => new Referencia(f.GetInt64(0), f.GetString(1)));

    public Task<long> InsertarReferencia(Catalogo catalogo, string nombre) => d.Insertar(
        $"INSERT INTO {Tabla(catalogo)} (nombre) VALUES (@nombre)", ("@nombre", nombre));

    public Task<List<Seccion>> Secciones() => d.Consultar(ConsultaSecciones + " ORDER BY c.nombre,s.semestre,s.codigo", SeccionDesde);

    private const string FiltroOfertas = """
         WHERE (@docente IS NULL OR o.docente_id=@docente) AND (@buscar='' OR p.nombre LIKE @patron OR m.nombre LIKE @patron
            OR s.codigo LIKE @patron OR c.nombre LIKE @patron OR CONCAT(u.nombres,' ',u.apellidos) LIKE @patron)
        """;

    public async Task<Pagina<Oferta>> Ofertas(long? docente, ConsultaPagina consulta)
    {
        (string, object?)[] filtro = [("@docente", docente), ("@buscar", consulta.Buscar), ("@patron", consulta.Patron)];
        var total = await d.Escalar<int>("""
            SELECT COUNT(*) FROM ofertas o JOIN periodos p ON p.id=o.periodo_id JOIN secciones s ON s.id=o.seccion_id
            JOIN carreras c ON c.id=s.carrera_id JOIN materias m ON m.id=o.materia_id JOIN usuarios u ON u.id=o.docente_id
            """ + FiltroOfertas, filtro);
        var ofertas = await d.Consultar(ConsultaOfertas + FiltroOfertas + " ORDER BY o.periodo_id DESC,m.nombre,s.codigo,o.id LIMIT @desde,@cantidad",
            OfertaDesde, [.. filtro, ("@desde", consulta.Desde), ("@cantidad", consulta.Cantidad)]);
        return new Pagina<Oferta>(ofertas, total, consulta.Desde, consulta.Cantidad);
    }

    public async Task<Periodo?> BloquearPeriodo(long periodo) => (await d.Consultar(
        "SELECT id,nombre,cerrado FROM periodos WHERE id=@id FOR UPDATE", f => new Periodo(f.GetInt64(0), f.GetString(1), f.GetBoolean(2)),
        ("@id", periodo))).FirstOrDefault();

    public async Task<bool> EsDocenteActivo(long usuario) => await d.Escalar<long>(
        "SELECT id FROM usuarios WHERE id=@id AND rol='docente' AND activo=TRUE", ("@id", usuario)) != 0;

    public Task<long> InsertarOferta(NuevaOferta oferta, long carrera) => d.Insertar(
        "INSERT INTO ofertas (periodo_id,seccion_id,carrera_id,materia_id,docente_id) VALUES (@periodo,@seccion,@carrera,@materia,@docente)",
        ("@periodo", oferta.PeriodoId), ("@seccion", oferta.SeccionId), ("@carrera", carrera), ("@materia", oferta.MateriaId), ("@docente", oferta.DocenteId));

    public async Task<Oferta?> Oferta(long oferta) =>
        (await d.Consultar(ConsultaOfertas + " WHERE o.id=@id", OfertaDesde, ("@id", oferta))).FirstOrDefault();

    public async Task<Oferta?> BloquearOferta(long oferta)
    {
        var periodo = await d.Escalar<long>("SELECT periodo_id FROM ofertas WHERE id=@id", ("@id", oferta));
        if (periodo == 0) return null;
        // Siempre el período primero y luego la asignación: el mismo orden evita bloqueos cruzados con el cierre.
        await d.Escalar<long>("SELECT id FROM periodos WHERE id=@id FOR UPDATE", ("@id", periodo));
        await d.Escalar<long>("SELECT id FROM ofertas WHERE id=@id FOR UPDATE", ("@id", oferta));
        return await Oferta(oferta);
    }

    public Task AsignarDocente(long oferta, long docente) => d.Ejecutar(
        "UPDATE ofertas SET docente_id=@docente,revision=revision+1 WHERE id=@id", ("@docente", docente), ("@id", oferta));

    public Task IncrementarRevision(long oferta) => d.Ejecutar("UPDATE ofertas SET revision=revision+1 WHERE id=@id", ("@id", oferta));

    public async Task<List<EstadoPlanilla>> EstadoPlanillasDePeriodo(long periodo)
    {
        var ofertas = await d.Consultar(ConsultaOfertas + " WHERE o.periodo_id=@id ORDER BY o.id", OfertaDesde, ("@id", periodo));
        // Ponderación y notas faltantes de todo el período en una sola consulta, no una por planilla.
        var resumen = (await d.Consultar("""
            SELECT o.id,
                (SELECT COALESCE(SUM(e.peso),0) FROM evaluaciones e WHERE e.oferta_id=o.id),
                (SELECT COUNT(*) FROM inscripciones i JOIN evaluaciones e ON e.oferta_id=i.oferta_id
                    LEFT JOIN notas n ON n.inscripcion_id=i.id AND n.evaluacion_id=e.id
                    WHERE i.oferta_id=o.id AND i.estado='confirmada' AND n.valor IS NULL)
            FROM ofertas o WHERE o.periodo_id=@id
            """, f => (Id: f.GetInt64(0), Peso: f.GetDecimal(1), Faltantes: f.GetInt64(2)), ("@id", periodo))).ToDictionary(r => r.Id);
        return ofertas.Select(o => new EstadoPlanilla(o, resumen[o.Id].Peso, resumen[o.Id].Faltantes)).ToList();
    }

    public Task MarcarPeriodoCerrado(long periodo, string redondeo) => d.Ejecutar(
        "UPDATE periodos SET cerrado=TRUE,cerrado_en=UTC_TIMESTAMP(6),redondeo_cierre=@redondeo WHERE id=@id", ("@redondeo", redondeo), ("@id", periodo));

    public Task CongelarReglas(long periodo, decimal aprobatoriaGeneral) => d.Ejecutar("""
        UPDATE ofertas o JOIN materias m ON m.id=o.materia_id
        SET o.uc_cierre=m.uc, o.aprobatoria_cierre=COALESCE(m.nota_aprobatoria,@general) WHERE o.periodo_id=@periodo
        """, ("@general", aprobatoriaGeneral), ("@periodo", periodo));

    public async Task<ParametrosOferta> ParametrosOferta(long oferta) => (await d.Consultar("""
        SELECT m.nota_aprobatoria,o.aprobatoria_cierre,p.redondeo_cierre FROM ofertas o
        JOIN materias m ON m.id=o.materia_id JOIN periodos p ON p.id=o.periodo_id WHERE o.id=@id
        """, f => new ParametrosOferta(f.IsDBNull(0) ? null : f.GetDecimal(0), f.IsDBNull(1) ? null : f.GetDecimal(1), f.IsDBNull(2) ? null : f.GetString(2)),
        ("@id", oferta))).Single();
}
