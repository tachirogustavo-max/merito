using System.Security.Cryptography;
using System.Text;
using MySqlConnector;

namespace Datos;

/// <summary>
/// Aplica en orden los scripts de Migraciones. Cada versión se registra con su huella:
/// un script ya aplicado no puede cambiar, los cambios de esquema van en un archivo nuevo.
/// </summary>
public sealed class Migrador(BaseDatos bd)
{
    public sealed record Migracion(int Version, string Nombre, string Sql, string Huella);

    // Huellas de estos scripts antes de reescribir sus comentarios; las sentencias no cambiaron.
    private static readonly Dictionary<int, string> HuellasAnteriores = new()
    {
        [3] = "4E5C4EC65E5D5298FD6DED42F46EEB5DA8A3C81C22E9D3C47F82D092EF52CCEA",
        [4] = "7394BCED261CDB267880D44CD4CFFBC054E3553F97151E67BAFDBCAD7CDA61BD",
        [5] = "33D2414C80FFB1D034D43BABDE40333DF1356721CB5B2C845ED7098DDF42640A",
        [6] = "CC3213F018F0672224ABE1F90A7792F55621F1D0E0A8E824BF02CC960A8247D6",
        [7] = "3BA3090277A3AB4C0D44E44A480AB20803EED7D14FDA24E0128DEFA30D2E4ADB",
        [8] = "0D8D4C86DDC707F4637233AF26834DE81C2728C52D37DE8FFE24AE369F331640",
    };

    public static IReadOnlyList<Migracion> Disponibles()
    {
        var ensamblado = typeof(Migrador).Assembly;
        return ensamblado.GetManifestResourceNames()
            .Where(n => n.StartsWith("Migraciones.", StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.Ordinal))
            .Select(n =>
            {
                using var lector = new StreamReader(ensamblado.GetManifestResourceStream(n)!);
                var sql = lector.ReadToEnd().Replace("\r\n", "\n");
                var nombre = n["Migraciones.".Length..^".sql".Length];
                return new Migracion(int.Parse(nombre[..4]), nombre, sql, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))));
            })
            .OrderBy(m => m.Version).ToList();
    }

    public static int VersionEsperada => Disponibles().Max(m => m.Version);

    public Task<int> VersionActual(CancellationToken ct = default) => bd.Directo(async d =>
    {
        var existe = await d.Escalar<long>("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='versiones_esquema'");
        return existe == 0 ? 0 : await d.Escalar<int>("SELECT COALESCE(MAX(version),0) FROM versiones_esquema");
    }, ct);

    public Task<List<int>> Aplicar(CancellationToken ct = default) => bd.Directo(async d =>
    {
        if (await d.Escalar<long>("SELECT GET_LOCK('merito_migraciones', 15)") != 1)
            throw new InvalidOperationException("Otra instancia está preparando la base de datos.");
        try
        {
            await d.Ejecutar("""
                CREATE TABLE IF NOT EXISTS versiones_esquema (
                    version INT NOT NULL PRIMARY KEY,
                    aplicada_en DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6)
                ) ENGINE=InnoDB
                """);
            await d.Ejecutar("ALTER TABLE versiones_esquema ADD COLUMN IF NOT EXISTS nombre VARCHAR(120) NULL, ADD COLUMN IF NOT EXISTS huella CHAR(64) NULL");
            var aplicadas = (await d.Consultar("SELECT version,huella FROM versiones_esquema", f => (Version: f.GetInt32(0), Huella: f.IsDBNull(1) ? null : f.GetString(1))))
                .ToDictionary(v => v.Version, v => v.Huella);
            var nuevas = new List<int>();
            foreach (var migracion in Disponibles())
            {
                if (aplicadas.TryGetValue(migracion.Version, out var huella))
                {
                    if (huella is null || (HuellasAnteriores.TryGetValue(migracion.Version, out var anterior) && huella == anterior))
                        await d.Ejecutar("UPDATE versiones_esquema SET nombre=@nombre,huella=@huella WHERE version=@version",
                            ("@nombre", migracion.Nombre), ("@huella", migracion.Huella), ("@version", migracion.Version));
                    else if (huella != migracion.Huella)
                        throw new InvalidOperationException($"La migración {migracion.Nombre} cambió después de aplicarse. Crea una migración nueva en lugar de editarla.");
                    continue;
                }
                // Las sentencias DDL de MariaDB se confirman solas; cada script debe poder repetirse sin daño.
                foreach (var sentencia in Sentencias(migracion.Sql)) await d.Ejecutar(sentencia);
                await d.Ejecutar("INSERT INTO versiones_esquema (version,nombre,huella) VALUES (@version,@nombre,@huella)",
                    ("@version", migracion.Version), ("@nombre", migracion.Nombre), ("@huella", migracion.Huella));
                nuevas.Add(migracion.Version);
            }
            return nuevas;
        }
        finally { await d.Escalar<long>("SELECT RELEASE_LOCK('merito_migraciones')"); }
    }, ct);

    private static IEnumerable<string> Sentencias(string sql)
    {
        var sinComentarios = string.Join('\n', sql.Split('\n').Where(l => !l.TrimStart().StartsWith("--", StringComparison.Ordinal)));
        return sinComentarios.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
