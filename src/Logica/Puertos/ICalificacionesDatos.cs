using Entidades;

namespace Logica;

public interface ICalificacionesDatos
{
    // Evaluaciones y notas
    Task<List<Evaluacion>> Evaluaciones(long oferta);
    Task<List<Alumno>> Alumnos(long oferta);
    Task<List<Nota>> Notas(long oferta);
    Task<decimal> PesoPlanificado(long oferta, long? excepto = null);
    Task<int> SiguienteOrden(long oferta);
    Task<long> InsertarEvaluacion(long oferta, string nombre, decimal peso, int orden);
    Task<bool> EvaluacionPertenece(long evaluacion, long oferta);
    Task ActualizarEvaluacion(long evaluacion, string nombre, decimal peso);
    Task<long> UsosDeEvaluacion(long evaluacion);
    Task<int> EliminarEvaluacion(long evaluacion, long oferta);
    Task<Dictionary<long, long>> InscripcionesConfirmadas(long oferta);
    Task<Dictionary<(long Evaluacion, long Inscripcion), decimal>> NotasGuardadas(long oferta);
    Task RegistrarCambioNota(long oferta, long evaluacion, long inscripcion, long actor, int revision, decimal? anterior, decimal? nueva);
    Task GuardarNota(long oferta, long evaluacion, long inscripcion, decimal valor);
    Task EliminarNota(long evaluacion, long inscripcion);
    Task<long> PublicacionDeRevision(long oferta, int revision);
    Task<long> InsertarPublicacion(long oferta, int revision, long actor);
    Task CopiarResultadosPublicados(long publicacion, long oferta);
    Task<List<ResultadoPublicado>> ResultadosPublicados(long estudiante);
}
