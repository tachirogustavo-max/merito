using Logica;

namespace Datos;

public sealed partial class ContextoDatos
{
    public Task<UsuarioExistente?> BloquearUsuarioPorCedula(string cedula) => UsuarioPorCedula(cedula, " FOR UPDATE");

    public Task<UsuarioExistente?> BuscarUsuarioPorCedula(string cedula) => UsuarioPorCedula(cedula, "");

    private async Task<UsuarioExistente?> UsuarioPorCedula(string cedula, string bloqueo) => (await d.Consultar(
        "SELECT id,nombres,apellidos,rol,correo,clave_hash IS NOT NULL FROM usuarios WHERE cedula=@cedula" + bloqueo,
        f => new UsuarioExistente(f.GetInt64(0), f.GetString(1), f.GetString(2), f.GetString(3), f.IsDBNull(4) ? null : f.GetString(4), f.GetBoolean(5)),
        ("@cedula", cedula))).FirstOrDefault();

    public Task<List<OfertaMatricula>> OfertasParaMatricula(long periodo) => d.Consultar("""
        SELECT o.id,o.seccion_id,o.materia_id,m.codigo,m.nombre,
            (SELECT COUNT(*) FROM inscripciones i WHERE i.oferta_id=o.id AND i.estado<>'retirada')
        FROM ofertas o JOIN materias m ON m.id=o.materia_id WHERE o.periodo_id=@periodo ORDER BY o.id
        """, f => new OfertaMatricula(f.GetInt64(0), f.GetInt64(1), f.GetInt64(2), f.GetString(3), f.GetString(4), f.GetInt32(5)), ("@periodo", periodo));

    public Task<List<InscripcionDelPeriodo>> InscripcionesDelPeriodo(long estudiante, long periodo) => d.Consultar("""
        SELECT i.id,i.oferta_id,i.materia_id,i.estado,s.codigo FROM inscripciones i
        JOIN ofertas o ON o.id=i.oferta_id JOIN secciones s ON s.id=o.seccion_id
        WHERE i.estudiante_id=@estudiante AND i.periodo_id=@periodo
        """, f => new InscripcionDelPeriodo(f.GetInt64(0), f.GetInt64(1), f.GetInt64(2), f.GetString(3), f.GetString(4)),
        ("@estudiante", estudiante), ("@periodo", periodo));

    public async Task BloquearOfertas(IReadOnlyCollection<long> ofertas)
    {
        if (ofertas.Count == 0) return;
        var lista = string.Join(',', ofertas.Select((_, i) => $"@o{i}"));
        (string, object?)[] parametros = [.. ofertas.Select((id, i) => ($"@o{i}", (object?)id))];
        await d.Consultar($"SELECT id FROM ofertas WHERE id IN ({lista}) ORDER BY id FOR UPDATE", f => f.GetInt64(0), parametros);
    }

    // Período y materia salen de la asignación misma; la clave foránea compuesta no admite otros.
    public Task InsertarInscripcion(long oferta, long estudiante, bool confirmada, long proponente, long? aprobador) => d.Ejecutar("""
        INSERT INTO inscripciones (oferta_id,periodo_id,materia_id,estudiante_id,estado,propuesta_por,aprobada_por)
        SELECT o.id,o.periodo_id,o.materia_id,@estudiante,@estado,@proponente,@aprobador FROM ofertas o WHERE o.id=@oferta
        """, ("@oferta", oferta), ("@estudiante", estudiante), ("@estado", confirmada ? "confirmada" : "pendiente"),
        ("@proponente", proponente), ("@aprobador", aprobador));

    public Task<int> ReactivarInscripcion(long inscripcion, long oferta, long aprobador) => d.Ejecutar("""
        UPDATE inscripciones SET estado='confirmada',aprobada_por=@aprobador
        WHERE id=@id AND oferta_id=@oferta AND estado='retirada'
        """, ("@aprobador", aprobador), ("@id", inscripcion), ("@oferta", oferta));

    public Task<int> RetirarInscripcion(long inscripcion, long oferta) => d.Ejecutar(
        "UPDATE inscripciones SET estado='retirada' WHERE id=@id AND oferta_id=@oferta AND estado='confirmada'", ("@id", inscripcion), ("@oferta", oferta));

    public Task<int> ConfirmarInscripcion(long inscripcion, long oferta, long aprobador) => d.Ejecutar("""
        UPDATE inscripciones SET estado='confirmada',aprobada_por=@aprobador
        WHERE id=@id AND oferta_id=@oferta AND estado='pendiente'
        """, ("@aprobador", aprobador), ("@id", inscripcion), ("@oferta", oferta));

    public Task<int> EliminarInscripcionPendiente(long inscripcion, long oferta) => d.Ejecutar(
        "DELETE FROM inscripciones WHERE id=@id AND oferta_id=@oferta AND estado='pendiente'", ("@id", inscripcion), ("@oferta", oferta));

    public Task<List<EstudianteSinAcceso>> BloquearEstudiantesSinAcceso(long oferta) => d.Consultar("""
        SELECT u.id,u.cedula,u.nombres,u.apellidos FROM inscripciones i JOIN usuarios u ON u.id=i.estudiante_id
        WHERE i.oferta_id=@oferta AND i.estado='confirmada' AND u.rol='estudiante' AND u.activo=TRUE AND u.clave_hash IS NULL
        ORDER BY u.apellidos,u.nombres,u.id FOR UPDATE
        """, f => new EstudianteSinAcceso(f.GetInt64(0), f.GetString(1), f.GetString(2), f.GetString(3)), ("@oferta", oferta));

    // Correo

    public async Task<CuentaParaCorreo?> BloquearCuentaParaCorreo(string cedula) => (await d.Consultar(
        "SELECT id,nombres,correo,activo,clave_hash IS NOT NULL FROM usuarios WHERE cedula=@cedula FOR UPDATE",
        f => new CuentaParaCorreo(f.GetInt64(0), f.GetString(1), f.IsDBNull(2) ? null : f.GetString(2), f.GetBoolean(3), f.GetBoolean(4)),
        ("@cedula", cedula))).FirstOrDefault();

    public Task ActualizarCorreo(long usuario, string? correo) => d.Ejecutar(
        "UPDATE usuarios SET correo=@correo WHERE id=@id", ("@correo", correo), ("@id", usuario));

    public Task<long> UsuarioConCorreo(string correo) => d.Escalar<long>("SELECT id FROM usuarios WHERE correo=@correo", ("@correo", correo));

    public async Task<Verificacion?> BloquearVerificacion(long usuario) => (await d.Consultar(
        "SELECT huella,vence_en,intentos,enviado_en FROM verificaciones_correo WHERE usuario_id=@id FOR UPDATE",
        f => new Verificacion(f.GetString(0), DateTime.SpecifyKind(f.GetDateTime(1), DateTimeKind.Utc), f.GetInt32(2), DateTime.SpecifyKind(f.GetDateTime(3), DateTimeKind.Utc)),
        ("@id", usuario))).FirstOrDefault();

    public async Task GuardarVerificacion(long usuario, string huella, DateTime vence)
    {
        await d.Ejecutar("DELETE FROM verificaciones_correo WHERE usuario_id=@id", ("@id", usuario));
        await d.Ejecutar("INSERT INTO verificaciones_correo (usuario_id,huella,vence_en) VALUES (@id,@huella,@vence)",
            ("@id", usuario), ("@huella", huella), ("@vence", vence));
    }

    public Task SumarIntento(long usuario) => d.Ejecutar(
        "UPDATE verificaciones_correo SET intentos=intentos+1 WHERE usuario_id=@id", ("@id", usuario));

    public Task EliminarVerificacion(long usuario) => d.Ejecutar("DELETE FROM verificaciones_correo WHERE usuario_id=@id", ("@id", usuario));
}