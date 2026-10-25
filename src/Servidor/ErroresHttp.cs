using Logica;

namespace Servidor;

public static class ErroresHttp
{
    public static int Estado(TipoErrorRegla tipo) => tipo switch
    {
        TipoErrorRegla.DatosInvalidos => StatusCodes.Status400BadRequest,
        TipoErrorRegla.NoAutenticado => StatusCodes.Status401Unauthorized,
        TipoErrorRegla.AccesoDenegado => StatusCodes.Status403Forbidden,
        TipoErrorRegla.NoEncontrado => StatusCodes.Status404NotFound,
        TipoErrorRegla.Conflicto => StatusCodes.Status409Conflict,
        TipoErrorRegla.LimiteIntentos => StatusCodes.Status429TooManyRequests,
        TipoErrorRegla.ServicioExternoNoDisponible => StatusCodes.Status502BadGateway,
        _ => StatusCodes.Status500InternalServerError
    };
}
