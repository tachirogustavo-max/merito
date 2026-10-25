using Entidades;

namespace Logica;

public enum Catalogo { Carreras, Periodos }
public enum Bloqueo { Ninguno, Compartido, Exclusivo }

public sealed record CuentaGuardada(Persona Persona, string? Hash);
public sealed record UsuarioExistente(long Id, string Nombres, string Apellidos, string Rol, string? Correo = null, bool Registrada = false);
public sealed record CuentaParaCorreo(long Id, string Nombres, string? Correo, bool Activo, bool Registrada);
public sealed record Verificacion(string Huella, DateTime VenceEn, int Intentos, DateTime EnviadoEn);

/// <summary>Envío de correos. Si el servidor no tiene cuenta de correo configurada, Disponible es falso.</summary>
public interface IEnviadorCorreo
{
    bool Disponible { get; }
    Task EnviarCodigoAcceso(string para, string nombres, string codigo, bool registrada, CancellationToken ct);
    Task EnviarPrueba(string para, string quien, CancellationToken ct);
}
public sealed record SeccionGuardada(Seccion Seccion, string? CodigoCarrera);
public sealed record EstudianteSinAcceso(long Id, string Cedula, string Nombres, string Apellidos);
public sealed record EstadoPlanilla(Oferta Oferta, decimal PesoTotal, long CalificacionesFaltantes);
// Uc ya es la de cierre si el período cerró; las demás de cierre son null mientras el período siga abierto.
public sealed record ResultadoPublicado(long OfertaId, string Periodo, string Carrera, string Seccion, string Materia,
    DateTime PublicadoEn, bool PeriodoCerrado, string Evaluacion, decimal Peso, decimal? Nota,
    string Codigo, int Uc, decimal? AprobatoriaMateria, decimal? AprobatoriaCierre, string? RedondeoCierre);
// Vigentes: inscripciones pendientes o confirmadas, sin las retiradas.
public sealed record OfertaMatricula(long Id, long SeccionId, long MateriaId, string Codigo, string Materia, int Vigentes);
public sealed record InscripcionDelPeriodo(long Id, long OfertaId, long MateriaId, string Estado, string Seccion);
// Un registro de auditoría con los nombres de lo que tocó; null si ya no existe (una evaluación eliminada, una carga retirada).
public sealed record RegistroHistorial(long Id, string Actor, string Accion, string Referencia, DateTime OcurridoEn,
    string? Oferta, string? Persona, string? Periodo, string? Carrera, string? Materia, string? Seccion);
public sealed record ParametrosOferta(decimal? AprobatoriaMateria, decimal? AprobatoriaCierre, string? RedondeoCierre);

/// <summary>
/// Acceso transaccional a los datos. Los servicios de esta capa deciden qué se puede hacer;
/// la implementación solo lee y escribe.
/// </summary>
public interface IAlmacen
{
    Task<T> Leer<T>(Func<IDatos, Task<T>> operacion, CancellationToken ct);
    Task<T> Transaccion<T>(Func<IDatos, Task<T>> operacion, CancellationToken ct);
}

/// <summary>Operaciones disponibles sobre una misma conexión y transacción.</summary>
public interface IDatos : IAuditoriaDatos, IIdentidadDatos, IEstructuraAcademicaDatos,
    IPensumDatos, ICalificacionesDatos, IMatriculaDatos
{
}
