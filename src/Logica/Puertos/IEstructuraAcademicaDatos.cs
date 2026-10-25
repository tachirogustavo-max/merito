using Entidades;

namespace Logica;

public interface IEstructuraAcademicaDatos
{
    // Estructura académica
    Task<List<Periodo>> Periodos();
    Task<List<Referencia>> Referencias(Catalogo catalogo);
    Task<long> InsertarReferencia(Catalogo catalogo, string nombre);
    Task<List<Seccion>> Secciones();
    Task<Seccion?> Seccion(long seccion);
    // Código de sección. El orden de bloqueo es siempre formato, carrera y secciones, para no cruzarse con otra operación.
    Task<string> FormatoSeccion();
    Task<string> BloquearFormatoSeccion();
    Task GuardarFormatoSeccion(string formato);
    Task<List<Carrera>> Carreras();
    Task<Carrera?> BloquearCarrera(long carrera);
    Task<long> InsertarCarrera(string nombre, string? codigo);
    Task<int> ActualizarCarrera(long carrera, string nombre, string? codigo);
    /// <summary>Secciones con el código de su carrera, bloqueadas; todas o las de una carrera.</summary>
    Task<List<SeccionGuardada>> BloquearSecciones(long? carrera);
    Task<long> InsertarSeccion(long carrera, int semestre, string nombre, string codigo);
    Task<int> ActualizarSeccion(long seccion, int semestre, string nombre, string codigo);
    /// <summary>Cambia códigos de varias secciones sin chocar con el índice único a mitad del cambio.</summary>
    Task RecodificarSecciones(IReadOnlyList<(long Seccion, string Codigo)> cambios);
    Task<int> RenombrarReferencia(Catalogo catalogo, long id, string nombre);
    // Configuración. Las reglas de calificación se leen con bloqueo compartido al guardar notas o cerrar,
    // y con bloqueo exclusivo al cambiarlas: un cambio de escala nunca se cruza con notas a medio guardar.
    Task<Dictionary<string, string>> Configuracion(IReadOnlyCollection<string> claves, Bloqueo bloqueo);
    Task GuardarConfiguracion(string clave, string valor);
    Task<bool> HayNotas();
    Task<long> MateriasConAprobatoriaFuera(decimal minima, decimal maxima);
}
