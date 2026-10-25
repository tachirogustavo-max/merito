using Entidades;
using Logica;

namespace Datos;

public sealed partial class ContextoDatos
{
    public Task<List<Evaluacion>> Evaluaciones(long oferta) => d.Consultar(
        "SELECT id,nombre,peso,orden FROM evaluaciones WHERE oferta_id=@oferta ORDER BY orden",
        f => new Evaluacion(f.GetInt64(0), f.GetString(1), f.GetDecimal(2), f.GetInt32(3)), ("@oferta", oferta));

    public Task<List<Alumno>> Alumnos(long oferta) => d.Consultar("""
        SELECT i.id,i.estudiante_id,u.cedula,u.nombres,u.apellidos,i.estado FROM inscripciones i
        JOIN usuarios u ON u.id=i.estudiante_id WHERE i.oferta_id=@oferta AND i.estado<>'retirada' ORDER BY u.apellidos,u.nombres
        """, f => new Alumno(f.GetInt64(0), f.GetInt64(1), f.GetString(2), f.GetString(3), f.GetString(4), f.GetString(5)), ("@oferta", oferta));

    public Task<List<Nota>> Notas(long oferta) => d.Consultar("""
        SELECT n.evaluacion_id,i.estudiante_id,n.valor FROM notas n JOIN inscripciones i ON i.id=n.inscripcion_id
        WHERE i.oferta_id=@oferta AND i.estado<>'retirada'
        """, f => new Nota(f.GetInt64(0), f.GetInt64(1), f.GetDecimal(2)), ("@oferta", oferta));

    public Task<decimal> PesoPlanificado(long oferta, long? excepto = null) => d.Escalar<decimal>(
        "SELECT COALESCE(SUM(peso),0) FROM evaluaciones WHERE oferta_id=@oferta AND (@excepto IS NULL OR id<>@excepto)",
        ("@oferta", oferta), ("@excepto", excepto));

    public Task<int> SiguienteOrden(long oferta) => d.Escalar<int>(
        "SELECT COALESCE(MAX(orden),0)+1 FROM evaluaciones WHERE oferta_id=@id", ("@id", oferta));

    public Task<long> InsertarEvaluacion(long oferta, string nombre, decimal peso, int orden) => d.Insertar(
        "INSERT INTO evaluaciones (oferta_id,nombre,peso,orden) VALUES (@oferta,@nombre,@peso,@orden)",
        ("@oferta", oferta), ("@nombre", nombre), ("@peso", peso), ("@orden", orden));

    public async Task<bool> EvaluacionPertenece(long evaluacion, long oferta) => await d.Escalar<long>(
        "SELECT COUNT(*) FROM evaluaciones WHERE id=@evaluacion AND oferta_id=@oferta", ("@evaluacion", evaluacion), ("@oferta", oferta)) > 0;

    public Task ActualizarEvaluacion(long evaluacion, string nombre, decimal peso) => d.Ejecutar(
        "UPDATE evaluaciones SET nombre=@nombre,peso=@peso WHERE id=@evaluacion", ("@nombre", nombre), ("@peso", peso), ("@evaluacion", evaluacion));

    public Task<long> UsosDeEvaluacion(long evaluacion) => d.Escalar<long>(
        "SELECT (SELECT COUNT(*) FROM notas WHERE evaluacion_id=@id)+(SELECT COUNT(*) FROM resultados_publicados WHERE evaluacion_id=@id)",
        ("@id", evaluacion));

    public Task<int> EliminarEvaluacion(long evaluacion, long oferta) => d.Ejecutar(
        "DELETE FROM evaluaciones WHERE id=@id AND oferta_id=@oferta", ("@id", evaluacion), ("@oferta", oferta));

    public async Task<Dictionary<long, long>> InscripcionesConfirmadas(long oferta) => (await d.Consultar(
        "SELECT estudiante_id,id FROM inscripciones WHERE oferta_id=@id AND estado='confirmada'",
        f => (Estudiante: f.GetInt64(0), Inscripcion: f.GetInt64(1)), ("@id", oferta))).ToDictionary(i => i.Estudiante, i => i.Inscripcion);

    public async Task<Dictionary<(long Evaluacion, long Inscripcion), decimal>> NotasGuardadas(long oferta) => (await d.Consultar(
        "SELECT evaluacion_id,inscripcion_id,valor FROM notas WHERE oferta_id=@id",
        f => (Evaluacion: f.GetInt64(0), Inscripcion: f.GetInt64(1), Valor: f.GetDecimal(2)), ("@id", oferta)))
        .ToDictionary(n => (n.Evaluacion, n.Inscripcion), n => n.Valor);

    public Task RegistrarCambioNota(long oferta, long evaluacion, long inscripcion, long actor, int revision, decimal? anterior, decimal? nueva) => d.Ejecutar("""
        INSERT INTO cambios_notas (oferta_id,evaluacion_id,inscripcion_id,actor_id,revision,anterior,nueva)
        VALUES (@oferta,@evaluacion,@inscripcion,@actor,@revision,@anterior,@nueva)
        """, ("@oferta", oferta), ("@evaluacion", evaluacion), ("@inscripcion", inscripcion),
        ("@actor", actor), ("@revision", revision), ("@anterior", anterior), ("@nueva", nueva));

    // Actualizar y, si no existía, insertar: la cuenta del servicio solo puede modificar el valor, nunca a qué planilla pertenece.
    public async Task GuardarNota(long oferta, long evaluacion, long inscripcion, decimal valor)
    {
        var cambiadas = await d.Ejecutar("UPDATE notas SET valor=@valor,actualizado_en=UTC_TIMESTAMP(6) WHERE evaluacion_id=@evaluacion AND inscripcion_id=@inscripcion",
            ("@valor", valor), ("@evaluacion", evaluacion), ("@inscripcion", inscripcion));
        if (cambiadas == 0)
            await d.Ejecutar("INSERT INTO notas (oferta_id,evaluacion_id,inscripcion_id,valor) VALUES (@oferta,@evaluacion,@inscripcion,@valor)",
                ("@oferta", oferta), ("@evaluacion", evaluacion), ("@inscripcion", inscripcion), ("@valor", valor));
    }

    public Task EliminarNota(long evaluacion, long inscripcion) => d.Ejecutar(
        "DELETE FROM notas WHERE evaluacion_id=@evaluacion AND inscripcion_id=@inscripcion", ("@evaluacion", evaluacion), ("@inscripcion", inscripcion));

    public Task<long> PublicacionDeRevision(long oferta, int revision) => d.Escalar<long>(
        "SELECT id FROM publicaciones WHERE oferta_id=@oferta AND revision=@revision", ("@oferta", oferta), ("@revision", revision));

    public Task<long> InsertarPublicacion(long oferta, int revision, long actor) => d.Insertar(
        "INSERT INTO publicaciones (oferta_id,revision,publicada_por) VALUES (@oferta,@revision,@actor)",
        ("@oferta", oferta), ("@revision", revision), ("@actor", actor));

    public Task CopiarResultadosPublicados(long publicacion, long oferta) => d.Ejecutar("""
        INSERT INTO resultados_publicados (publicacion_id,estudiante_id,evaluacion_id,nombre_evaluacion,peso,valor,orden)
        SELECT @publicacion,i.estudiante_id,e.id,e.nombre,e.peso,n.valor,e.orden
        FROM inscripciones i JOIN evaluaciones e ON e.oferta_id=i.oferta_id
        LEFT JOIN notas n ON n.inscripcion_id=i.id AND n.evaluacion_id=e.id
        WHERE i.oferta_id=@oferta AND i.estado='confirmada'
        """, ("@publicacion", publicacion), ("@oferta", oferta));

    public Task<List<ResultadoPublicado>> ResultadosPublicados(long estudiante) => d.Consultar("""
        SELECT o.id,p.nombre periodo,c.nombre carrera,s.codigo seccion,m.nombre materia,pub.publicada_en,p.cerrado,
            r.nombre_evaluacion,r.peso,r.valor,m.codigo,COALESCE(o.uc_cierre,m.uc),m.nota_aprobatoria,o.aprobatoria_cierre,p.redondeo_cierre
        FROM resultados_publicados r JOIN publicaciones pub ON pub.id=r.publicacion_id
        JOIN ofertas o ON o.id=pub.oferta_id JOIN periodos p ON p.id=o.periodo_id
        JOIN secciones s ON s.id=o.seccion_id JOIN carreras c ON c.id=s.carrera_id JOIN materias m ON m.id=o.materia_id
        WHERE r.estudiante_id=@estudiante AND pub.id=(SELECT MAX(ultima.id) FROM publicaciones ultima WHERE ultima.oferta_id=o.id)
            -- A quien retiraron de la materia ya no la ve, aunque una publicación anterior lo incluyera.
            AND NOT EXISTS (SELECT 1 FROM inscripciones ir WHERE ir.oferta_id=o.id AND ir.estudiante_id=r.estudiante_id AND ir.estado='retirada')
        ORDER BY p.id DESC,m.nombre,r.orden
        """, f => new ResultadoPublicado(f.GetInt64(0), f.GetString(1), f.GetString(2), f.GetString(3), f.GetString(4),
            f.GetDateTime(5), f.GetBoolean(6), f.GetString(7), f.GetDecimal(8), f.IsDBNull(9) ? null : f.GetDecimal(9),
            f.GetString(10), f.GetInt32(11), f.IsDBNull(12) ? null : f.GetDecimal(12), f.IsDBNull(13) ? null : f.GetDecimal(13),
            f.IsDBNull(14) ? null : f.GetString(14)), ("@estudiante", estudiante));
}
