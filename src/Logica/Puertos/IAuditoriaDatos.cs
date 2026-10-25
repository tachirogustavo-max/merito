using Entidades;

namespace Logica;

public interface IAuditoriaDatos
{
    Task Auditar(long actor, string accion, object referencia);

    /// <summary>Historial de cambios, sin inicios de sesión ni envíos de códigos, con los nombres de lo que se tocó.</summary>
    Task<Pagina<RegistroHistorial>> Auditoria(ConsultaPagina consulta);
}
