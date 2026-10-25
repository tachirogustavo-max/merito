using Entidades;

namespace Logica;

public interface IPensumDatos
{
    // Materias y pensum
    Task<List<Materia>> Materias();
    Task<Materia?> BloquearMateria(long materia);
    /// <summary>Las materias con esos códigos, bloqueadas, con las carreras en cuyo pensum están.</summary>
    Task<List<Materia>> BloquearMateriasPorCodigo(IReadOnlyCollection<string> codigos);
    Task<long> InsertarMateria(NuevaMateria materia);
    Task<int> ActualizarMateria(long materia, NuevaMateria cambio);
    Task<List<MateriaPensum>> Pensum(long? carrera);
    /// <summary>Materia y semestre del pensum de la carrera, bloqueados.</summary>
    Task<Dictionary<long, int>> BloquearPensum(long carrera);
    Task InsertarEnPensum(long carrera, long materia, int semestre);
    Task<int> CambiarSemestre(long carrera, long materia, int semestre);
    Task<int> QuitarDelPensum(long carrera, long materia);
    Task<long> AsignacionesEnPensum(long carrera, long materia);
    Task<bool> EstaEnPensum(long carrera, long materia);
    Task<bool> ExisteOferta(long periodo, long seccion, long materia);
    Task<List<NuevaOferta>> CargaDePeriodo(long periodo);
    Task<List<MateriaAsignada>> CargaDeSeccion(long periodo, long seccion);
    /// <summary>Evaluaciones, inscripciones o publicaciones: con cualquiera de ellas la carga ya no se retira.</summary>
    Task<bool> TieneMovimientos(long oferta);
    Task<int> EliminarOferta(long oferta);
    Task<Pagina<Oferta>> Ofertas(long? docente, ConsultaPagina consulta);
    Task<Periodo?> BloquearPeriodo(long periodo);
    Task<bool> EsDocenteActivo(long usuario);
    /// <summary>La carrera es la de la sección: con ella la base exige que la materia esté en ese pensum.</summary>
    Task<long> InsertarOferta(NuevaOferta oferta, long carrera);
    Task<Oferta?> Oferta(long oferta);
    /// <summary>Bloquea el período y la asignación para serializar escrituras y cierre.</summary>
    Task<Oferta?> BloquearOferta(long oferta);
    Task AsignarDocente(long oferta, long docente);
    Task IncrementarRevision(long oferta);
    Task<List<EstadoPlanilla>> EstadoPlanillasDePeriodo(long periodo);
    /// <summary>Cierra el período y guarda el redondeo que regía, en una sola sentencia: la base exige ambas cosas juntas.</summary>
    Task MarcarPeriodoCerrado(long periodo, string redondeo);
    /// <summary>Guarda en cada asignación del período sus UC y la nota aprobatoria que regía.</summary>
    Task CongelarReglas(long periodo, decimal aprobatoriaGeneral);
    Task<ParametrosOferta> ParametrosOferta(long oferta);
}
