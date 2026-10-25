using Entidades;

namespace Logica;

public interface IIdentidadDatos
{
    // Cuentas y sesiones
    Task<long> ContarAdministradores();
    Task<bool> HayCodigoConfiguracion();
    /// <summary>Borra el código si existe; devuelve 1 solo a quien lo consumió.</summary>
    Task<int> ConsumirCodigoConfiguracion(string huella);
    Task<long> InsertarUsuario(string cedula, string nombres, string apellidos, string rol, string? hash);
    Task<CuentaGuardada?> CuentaPorCedula(string cedula);
    Task<string?> BloquearClave(long usuario, bool soloActiva);
    Task EliminarSesionesVencidas();
    Task InsertarSesion(string huella, long usuario, DateTime vence);
    Task<Persona?> PersonaPorSesion(string huella);
    Task EliminarSesion(string huella);
    Task ActualizarClave(long usuario, string hash);
    Task EliminarSesionesDe(long usuario);
    Task EliminarCodigosDe(long usuario);
    Task<Pagina<Persona>> Personas(ConsultaPagina consulta, string? rol = null);
    Task<Pagina<FilaDocente>> NominaDocente(ConsultaPagina consulta);
    Task<Pagina<FilaEstudiante>> Estudiantes(ConsultaPagina consulta, long? periodo, long? seccion);
    Task<List<Persona>> DocentesActivos();
    Task<bool> BloquearUsuario(long usuario, bool soloActivo);
    Task<long> BloquearUsuarioActivoPorCedula(string cedula);
    Task InsertarCodigo(string huella, long usuario, DateTime vence);
    Task<bool> CodigoVigente(long usuario, string huella);
    // Correo
    Task<CuentaParaCorreo?> BloquearCuentaParaCorreo(string cedula);
    Task ActualizarCorreo(long usuario, string? correo);
    Task<long> UsuarioConCorreo(string correo);
    Task<Verificacion?> BloquearVerificacion(long usuario);
    Task GuardarVerificacion(long usuario, string huella, DateTime vence);
    Task SumarIntento(long usuario);
    Task EliminarVerificacion(long usuario);
    /// <summary>Estudiantes confirmados en la asignación, activos y sin contraseña todavía; bloqueados.</summary>
    Task<List<EstudianteSinAcceso>> BloquearEstudiantesSinAcceso(long oferta);
    Task<List<long>> BloquearAdministradoresActivos();
    Task ActualizarActivo(long usuario, bool activo);
}
