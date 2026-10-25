namespace Logica;

public enum TipoErrorRegla
{
    DatosInvalidos,
    NoAutenticado,
    AccesoDenegado,
    NoEncontrado,
    Conflicto,
    LimiteIntentos,
    ServicioExternoNoDisponible
}

public sealed class ReglaException(string mensaje, TipoErrorRegla tipo = TipoErrorRegla.DatosInvalidos) : Exception(mensaje)
{
    public TipoErrorRegla Tipo { get; } = tipo;
}

public sealed class ConflictoDatosException(Exception causa)
    : Exception("Los datos están repetidos o hacen referencia a un registro inexistente.", causa);

public sealed class DatosNoDisponiblesException(Exception causa)
    : Exception("No se pudo confirmar la operación con la base de datos.", causa);
