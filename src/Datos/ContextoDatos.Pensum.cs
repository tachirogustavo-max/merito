using Entidades;
using Logica;
using MySqlConnector;

namespace Datos;

public sealed partial class ContextoDatos
{
    // Configuración

    public async Task<Dictionary<string, string>> Configuracion(IReadOnlyCollection<string> claves, Bloqueo bloqueo)
    {
        var nombres = claves.Select((_, i) => $"@c{i}").ToList();
        var sufijo = bloqueo switch { Bloqueo.Exclusivo => " FOR UPDATE", Bloqueo.Compartido => " LOCK IN SHARE MODE", _ => "" };
        var filas = await d.Consultar($"SELECT clave,valor FROM configuracion WHERE clave IN ({string.Join(',', nombres)}) ORDER BY clave{sufijo}",
            f => (Clave: f.GetString(0), Valor: f.GetString(1)), [.. claves.Select((c, i) => ($"@c{i}", (object?)c))]);
        var valores = filas.ToDictionary(f => f.Clave, f => f.Valor);
        var faltan = claves.Where(c => !valores.ContainsKey(c)).ToList();
        if (faltan.Count > 0) throw new InvalidOperationException($"Faltan claves de configuración: {string.Join(", ", faltan)}. Revise las migraciones.");
        return valores;
    }

    public async Task GuardarConfiguracion(string clave, string valor)
    {
        if (await d.Ejecutar("UPDATE configuracion SET valor=@valor,actualizado_en=UTC_TIMESTAMP(6) WHERE clave=@clave", ("@valor", valor), ("@clave", clave)) != 1)
            throw new InvalidOperationException($"Falta la clave de configuración {clave}.");
    }

    public async Task<bool> HayNotas() => await d.Escalar<long>("""
        SELECT EXISTS(SELECT 1 FROM notas) OR EXISTS(SELECT 1 FROM cambios_notas) OR EXISTS(SELECT 1 FROM resultados_publicados WHERE valor IS NOT NULL)
        """) != 0;

    public Task<long> MateriasConAprobatoriaFuera(decimal minima, decimal maxima) => d.Escalar<long>(
        "SELECT COUNT(*) FROM materias WHERE nota_aprobatoria IS NOT NULL AND (nota_aprobatoria<=@minima OR nota_aprobatoria>@maxima)",
        ("@minima", minima), ("@maxima", maxima));

    // Materias y pensum

    private const string ConsultaMaterias = """
        SELECT m.id,m.codigo,m.nombre,m.uc,m.nota_aprobatoria,
            COALESCE((SELECT GROUP_CONCAT(c.nombre ORDER BY c.nombre SEPARATOR ', ') FROM pensum p JOIN carreras c ON c.id=p.carrera_id WHERE p.materia_id=m.id),'')
        FROM materias m
        """;

    private static Materia MateriaDesde(MySqlDataReader f) => new(f.GetInt64(0), f.GetString(1), f.GetString(2), f.GetInt32(3),
        f.IsDBNull(4) ? null : f.GetDecimal(4), f.GetString(5));

    public Task<List<Materia>> Materias() => d.Consultar(ConsultaMaterias + " ORDER BY m.codigo", MateriaDesde);

    // Solo se bloquea la fila de la materia; las carreras se leen aparte para no tomar sus bloqueos fuera de orden.
    public async Task<Materia?> BloquearMateria(long materia)
    {
        if (await d.Escalar<long>("SELECT id FROM materias WHERE id=@id FOR UPDATE", ("@id", materia)) == 0) return null;
        return (await d.Consultar(ConsultaMaterias + " WHERE m.id=@id", MateriaDesde, ("@id", materia))).FirstOrDefault();
    }

    public async Task<List<Materia>> BloquearMateriasPorCodigo(IReadOnlyCollection<string> codigos)
    {
        if (codigos.Count == 0) return [];
        var lista = string.Join(',', codigos.Select((_, i) => $"@m{i}"));
        (string, object?)[] parametros = [.. codigos.Select((c, i) => ($"@m{i}", (object?)c))];
        await d.Consultar($"SELECT id FROM materias WHERE codigo IN ({lista}) ORDER BY id FOR UPDATE", f => f.GetInt64(0), parametros);
        return await d.Consultar(ConsultaMaterias + $" WHERE m.codigo IN ({lista}) ORDER BY m.id", MateriaDesde, parametros);
    }

    public Task<long> InsertarMateria(NuevaMateria materia) => d.Insertar(
        "INSERT INTO materias (codigo,nombre,uc,nota_aprobatoria) VALUES (@codigo,@nombre,@uc,@aprobatoria)",
        ("@codigo", materia.Codigo), ("@nombre", materia.Nombre), ("@uc", materia.Uc), ("@aprobatoria", materia.NotaAprobatoria));

    public Task<int> ActualizarMateria(long materia, NuevaMateria cambio) => d.Ejecutar(
        "UPDATE materias SET codigo=@codigo,nombre=@nombre,uc=@uc,nota_aprobatoria=@aprobatoria WHERE id=@id",
        ("@codigo", cambio.Codigo), ("@nombre", cambio.Nombre), ("@uc", cambio.Uc), ("@aprobatoria", cambio.NotaAprobatoria), ("@id", materia));

    public Task<List<MateriaPensum>> Pensum(long? carrera) => d.Consultar("""
        SELECT m.id,m.codigo,m.nombre,m.uc,m.nota_aprobatoria,p.semestre,
            (SELECT COUNT(*) FROM pensum otro WHERE otro.materia_id=m.id),p.carrera_id
        FROM pensum p JOIN materias m ON m.id=p.materia_id
        WHERE @carrera IS NULL OR p.carrera_id=@carrera
        ORDER BY p.carrera_id,p.semestre=0,p.semestre,m.codigo
        """, f => new MateriaPensum(f.GetInt64(0), f.GetString(1), f.GetString(2), f.GetInt32(3), f.IsDBNull(4) ? null : f.GetDecimal(4),
            f.GetInt32(5), f.GetInt32(6), f.GetInt64(7)), ("@carrera", carrera));

    public async Task<Dictionary<long, int>> BloquearPensum(long carrera) => (await d.Consultar(
        "SELECT materia_id,semestre FROM pensum WHERE carrera_id=@carrera ORDER BY materia_id FOR UPDATE",
        f => (Materia: f.GetInt64(0), Semestre: f.GetInt32(1)), ("@carrera", carrera))).ToDictionary(p => p.Materia, p => p.Semestre);

    public Task InsertarEnPensum(long carrera, long materia, int semestre) => d.Ejecutar(
        "INSERT INTO pensum (carrera_id,materia_id,semestre) VALUES (@carrera,@materia,@semestre)",
        ("@carrera", carrera), ("@materia", materia), ("@semestre", semestre));

    public Task<int> CambiarSemestre(long carrera, long materia, int semestre) => d.Ejecutar(
        "UPDATE pensum SET semestre=@semestre WHERE carrera_id=@carrera AND materia_id=@materia",
        ("@semestre", semestre), ("@carrera", carrera), ("@materia", materia));

    public Task<int> QuitarDelPensum(long carrera, long materia) => d.Ejecutar(
        "DELETE FROM pensum WHERE carrera_id=@carrera AND materia_id=@materia", ("@carrera", carrera), ("@materia", materia));

    public Task<long> AsignacionesEnPensum(long carrera, long materia) => d.Escalar<long>(
        "SELECT COUNT(*) FROM ofertas WHERE carrera_id=@carrera AND materia_id=@materia", ("@carrera", carrera), ("@materia", materia));

    // Con bloqueo compartido: quitar la materia del pensum espera a que termine la asignación, y la clave foránea hace el resto.
    public async Task<bool> EstaEnPensum(long carrera, long materia) => await d.Escalar<long>(
        "SELECT COUNT(*) FROM pensum WHERE carrera_id=@carrera AND materia_id=@materia LOCK IN SHARE MODE",
        ("@carrera", carrera), ("@materia", materia)) > 0;
}
