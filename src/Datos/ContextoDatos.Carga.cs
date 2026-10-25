using Entidades;
using Logica;

namespace Datos;

public sealed partial class ContextoDatos
{
    internal const string ConsultaSecciones = "SELECT s.id,s.carrera_id,s.nombre,c.nombre,s.semestre,s.codigo,c.codigo FROM secciones s JOIN carreras c ON c.id=s.carrera_id";

    internal static Seccion SeccionDesde(MySqlConnector.MySqlDataReader f) =>
        new(f.GetInt64(0), f.GetInt64(1), f.GetString(2), f.GetString(3), f.GetInt32(4), f.GetString(5));

    public async Task<Seccion?> Seccion(long seccion) =>
        (await d.Consultar(ConsultaSecciones + " WHERE s.id=@id", SeccionDesde, ("@id", seccion))).FirstOrDefault();

    public Task<List<SeccionGuardada>> BloquearSecciones(long? carrera) => d.Consultar(
        ConsultaSecciones + " WHERE (@carrera IS NULL OR s.carrera_id=@carrera) ORDER BY s.id FOR UPDATE",
        f => new SeccionGuardada(SeccionDesde(f), f.IsDBNull(6) ? null : f.GetString(6)), ("@carrera", carrera));

    public Task<long> InsertarSeccion(long carrera, int semestre, string nombre, string codigo) => d.Insertar(
        "INSERT INTO secciones (carrera_id,semestre,nombre,codigo) VALUES (@carrera,@semestre,@nombre,@codigo)",
        ("@carrera", carrera), ("@semestre", semestre), ("@nombre", nombre), ("@codigo", codigo));

    public Task<int> ActualizarSeccion(long seccion, int semestre, string nombre, string codigo) => d.Ejecutar(
        "UPDATE secciones SET semestre=@semestre,nombre=@nombre,codigo=@codigo WHERE id=@id",
        ("@semestre", semestre), ("@nombre", nombre), ("@codigo", codigo), ("@id", seccion));

    public async Task RecodificarSecciones(IReadOnlyList<(long Seccion, string Codigo)> cambios)
    {
        // Primero un código provisional único por fila y después el definitivo: dos secciones pueden intercambiar códigos.
        foreach (var (seccion, _) in cambios)
            await d.Ejecutar("UPDATE secciones SET codigo=CONCAT('~',id) WHERE id=@id", ("@id", seccion));
        foreach (var (seccion, codigo) in cambios)
            await d.Ejecutar("UPDATE secciones SET codigo=@codigo WHERE id=@id", ("@codigo", codigo), ("@id", seccion));
    }

    public Task<string> FormatoSeccion() => d.Escalar<string>("SELECT valor FROM configuracion WHERE clave='formato_seccion'");
    public Task<string> BloquearFormatoSeccion() => d.Escalar<string>("SELECT valor FROM configuracion WHERE clave='formato_seccion' FOR UPDATE");

    public async Task GuardarFormatoSeccion(string formato)
    {
        if (await d.Ejecutar("UPDATE configuracion SET valor=@valor,actualizado_en=UTC_TIMESTAMP(6) WHERE clave='formato_seccion'", ("@valor", formato)) != 1)
            throw new InvalidOperationException("Falta la fila formato_seccion; la migración 0004 no se aplicó.");
    }

    public Task<List<Carrera>> Carreras() => d.Consultar("SELECT id,nombre,codigo FROM carreras ORDER BY nombre",
        f => new Carrera(f.GetInt64(0), f.GetString(1), f.IsDBNull(2) ? null : f.GetString(2)));

    public async Task<Carrera?> BloquearCarrera(long carrera) => (await d.Consultar("SELECT id,nombre,codigo FROM carreras WHERE id=@id FOR UPDATE",
        f => new Carrera(f.GetInt64(0), f.GetString(1), f.IsDBNull(2) ? null : f.GetString(2)), ("@id", carrera))).FirstOrDefault();

    public Task<long> InsertarCarrera(string nombre, string? codigo) => d.Insertar(
        "INSERT INTO carreras (nombre,codigo) VALUES (@nombre,@codigo)", ("@nombre", nombre), ("@codigo", codigo));

    public Task<int> ActualizarCarrera(long carrera, string nombre, string? codigo) => d.Ejecutar(
        "UPDATE carreras SET nombre=@nombre,codigo=@codigo WHERE id=@id", ("@nombre", nombre), ("@codigo", codigo), ("@id", carrera));

    public Task<int> RenombrarReferencia(Catalogo catalogo, long id, string nombre) =>
        d.Ejecutar($"UPDATE {Tabla(catalogo)} SET nombre=@nombre WHERE id=@id", ("@nombre", nombre), ("@id", id));

    public async Task<bool> ExisteOferta(long periodo, long seccion, long materia) => await d.Escalar<long>(
        "SELECT COUNT(*) FROM ofertas WHERE periodo_id=@periodo AND seccion_id=@seccion AND materia_id=@materia",
        ("@periodo", periodo), ("@seccion", seccion), ("@materia", materia)) > 0;

    public Task<List<NuevaOferta>> CargaDePeriodo(long periodo) => d.Consultar(
        "SELECT periodo_id,seccion_id,materia_id,docente_id FROM ofertas WHERE periodo_id=@periodo ORDER BY id",
        f => new NuevaOferta(f.GetInt64(0), f.GetInt64(1), f.GetInt64(2), f.GetInt64(3)), ("@periodo", periodo));

    public Task<List<MateriaAsignada>> CargaDeSeccion(long periodo, long seccion) => d.Consultar("""
        SELECT o.materia_id,o.id,CONCAT(u.nombres,' ',u.apellidos) FROM ofertas o JOIN usuarios u ON u.id=o.docente_id
        WHERE o.periodo_id=@periodo AND o.seccion_id=@seccion
        """, f => new MateriaAsignada(f.GetInt64(0), f.GetInt64(1), f.GetString(2)), ("@periodo", periodo), ("@seccion", seccion));

    public async Task<bool> TieneMovimientos(long oferta) => await d.Escalar<long>("""
        SELECT (SELECT COUNT(*) FROM evaluaciones WHERE oferta_id=@id) + (SELECT COUNT(*) FROM inscripciones WHERE oferta_id=@id)
             + (SELECT COUNT(*) FROM publicaciones WHERE oferta_id=@id)
        """, ("@id", oferta)) > 0;

    public Task<int> EliminarOferta(long oferta) => d.Ejecutar("DELETE FROM ofertas WHERE id=@id", ("@id", oferta));
}
