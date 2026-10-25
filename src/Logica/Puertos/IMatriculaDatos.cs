using Entidades;

namespace Logica;

public interface IMatriculaDatos
{
    // Matrícula
    Task<UsuarioExistente?> BloquearUsuarioPorCedula(string cedula);
    Task<UsuarioExistente?> BuscarUsuarioPorCedula(string cedula);
    Task<List<OfertaMatricula>> OfertasParaMatricula(long periodo);
    /// <summary>Todas las inscripciones del estudiante en el período, también las retiradas.</summary>
    Task<List<InscripcionDelPeriodo>> InscripcionesDelPeriodo(long estudiante, long periodo);
    /// <summary>Bloquea varias asignaciones en orden de id; el período se bloquea antes.</summary>
    Task BloquearOfertas(IReadOnlyCollection<long> ofertas);
    Task InsertarInscripcion(long oferta, long estudiante, bool confirmada, long proponente, long? aprobador);
    Task<int> ConfirmarInscripcion(long inscripcion, long oferta, long aprobador);
    Task<int> ReactivarInscripcion(long inscripcion, long oferta, long aprobador);
    Task<int> RetirarInscripcion(long inscripcion, long oferta);
    Task<int> EliminarInscripcionPendiente(long inscripcion, long oferta);
}
