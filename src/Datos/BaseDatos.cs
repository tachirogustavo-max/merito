using System.Data;
using Logica;
using MySqlConnector;

namespace Datos;

public sealed class BaseDatos : IAlmacen
{
    private readonly string cadena;

    public BaseDatos(string cadenaConexion)
    {
        var opciones = new MySqlConnectionStringBuilder(cadenaConexion)
        {
            Pooling = true,
            MaximumPoolSize = 30,
            ConnectionTimeout = 5,
            DefaultCommandTimeout = 15,
            DateTimeKind = MySqlDateTimeKind.Utc,
            AllowUserVariables = false
        };
        if (string.IsNullOrWhiteSpace(opciones.Database))
            throw new InvalidOperationException("La conexión debe indicar una base de datos exclusiva para esta aplicación.");
        cadena = opciones.ConnectionString;
    }

    public async Task<MySqlConnection> Abrir(CancellationToken cancelacion = default)
    {
        var conexion = new MySqlConnection(cadena);
        try { await conexion.OpenAsync(cancelacion); return conexion; }
        catch { await conexion.DisposeAsync(); throw; }
    }

    public Task<T> Leer<T>(Func<IDatos, Task<T>> operacion, CancellationToken cancelacion) =>
        Ejecutar(operacion, IsolationLevel.RepeatableRead, soloLectura: true, cancelacion);

    public Task<T> Transaccion<T>(Func<IDatos, Task<T>> operacion, CancellationToken cancelacion) =>
        Ejecutar(operacion, IsolationLevel.ReadCommitted, soloLectura: false, cancelacion);

    /// <summary>Acceso directo sin transacción, reservado a pruebas y mantenimiento.</summary>
    public async Task<T> Directo<T>(Func<UnidadDatos, Task<T>> operacion, CancellationToken cancelacion = default)
    {
        await using var conexion = await Abrir(cancelacion);
        return await operacion(new UnidadDatos(conexion, null, cancelacion));
    }

    private async Task<T> Ejecutar<T>(Func<IDatos, Task<T>> operacion, IsolationLevel aislamiento, bool soloLectura, CancellationToken cancelacion)
    {
        try
        {
            await using var conexion = await Abrir(cancelacion);
            // La lectura usa una instantánea: todas sus consultas ven el mismo estado aunque otros guarden a la vez.
            await using var transaccion = await conexion.BeginTransactionAsync(aislamiento, soloLectura, cancelacion);
            var datos = new ContextoDatos(new UnidadDatos(conexion, transaccion, cancelacion));
            try
            {
                var resultado = await operacion(datos);
                await transaccion.CommitAsync(cancelacion);
                return resultado;
            }
            catch
            {
                try { await transaccion.RollbackAsync(CancellationToken.None); }
                catch (MySqlException) { /* La desconexión revierte la transacción en el servidor. */ }
                throw;
            }
        }
        catch (MySqlException error) when (error.Number is 1062 or 1452 or 4025) { throw new ConflictoDatosException(error); }
        catch (MySqlException error) { throw new DatosNoDisponiblesException(error); }
    }
}

public sealed class UnidadDatos(MySqlConnection conexion, MySqlTransaction? transaccion, CancellationToken cancelacion)
{
    private MySqlCommand Comando(string sql, (string Nombre, object? Valor)[] parametros)
    {
        var comando = new MySqlCommand(sql, conexion, transaccion);
        foreach (var (nombre, valor) in parametros) comando.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);
        return comando;
    }

    public async Task<int> Ejecutar(string sql, params (string Nombre, object? Valor)[] parametros)
    {
        await using var comando = Comando(sql, parametros);
        return await comando.ExecuteNonQueryAsync(cancelacion);
    }

    public async Task<T> Escalar<T>(string sql, params (string Nombre, object? Valor)[] parametros)
    {
        await using var comando = Comando(sql, parametros);
        var valor = await comando.ExecuteScalarAsync(cancelacion);
        if (valor is null || valor is DBNull) return default!;
        return (T)Convert.ChangeType(valor, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<List<T>> Consultar<T>(string sql, Func<MySqlDataReader, T> convertir,
        params (string Nombre, object? Valor)[] parametros)
    {
        await using var comando = Comando(sql, parametros);
        await using var lector = await comando.ExecuteReaderAsync(cancelacion);
        var resultado = new List<T>();
        while (await lector.ReadAsync(cancelacion)) resultado.Add(convertir(lector));
        return resultado;
    }

    public async Task<long> Insertar(string sql, params (string Nombre, object? Valor)[] parametros)
    {
        await Ejecutar(sql, parametros);
        return await Escalar<long>("SELECT LAST_INSERT_ID()");
    }
}
