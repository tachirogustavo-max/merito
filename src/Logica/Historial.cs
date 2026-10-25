using Entidades;

namespace Logica;

/// <summary>Cuenta en español cada registro de auditoría, con los nombres de lo que se cambió.</summary>
public static class Historial
{
    public static EventoAuditoria Evento(RegistroHistorial r) => new(r.Id, r.Actor, r.Accion, r.Referencia, r.OcurridoEn, Describir(r));

    public static string Describir(RegistroHistorial r)
    {
        // Lo que ya no existe (una evaluación eliminada, una carga retirada) se nombra de forma general.
        var oferta = r.Oferta ?? "una materia";
        var persona = r.Persona ?? "una cuenta";
        var periodo = r.Periodo ?? "un período";
        var carrera = r.Carrera ?? "una carrera";
        var materia = r.Materia ?? "una materia";
        var partes = r.Referencia.Split(':');
        string Parte(int i) => i < partes.Length ? partes[i] : "";
        int Numero(int i) => int.TryParse(Parte(i), out var n) ? n : 0;
        string Cantidad(int n, string uno, string varios) => $"{n} {(n == 1 ? uno : varios)}";
        return r.Accion switch
        {
            "notas_guardadas" => $"Guardó notas en borrador en {oferta}",
            "notas_publicadas" => $"Publicó las notas de {oferta}",
            "evaluacion_creada" => $"Agregó una evaluación en {oferta}",
            "evaluacion_editada" => $"Corrigió una evaluación de {oferta}",
            "evaluacion_eliminada" => "Eliminó una evaluación que no tenía notas",
            "matricula_importada" => $"Importó la matrícula de {oferta}",
            "matricula_periodo_importada" => $"Importó la matrícula del período {periodo}: {Cantidad(Numero(1), "inscripción nueva", "inscripciones nuevas")}",
            "matricula_confirmada" => $"Confirmó la matrícula propuesta en {oferta}",
            "matricula_propuesta_rechazada" => $"Rechazó una matrícula propuesta en {oferta}",
            "matricula_retirada" => $"Retiró {Cantidad(Numero(1), "estudiante", "estudiantes")} de {oferta}",
            "codigos_seccion" => $"Generó {Cantidad(Numero(1), "código de acceso", "códigos de acceso")} para {oferta}",
            "asignacion_creada" => $"Asignó {oferta}",
            "asignacion_retirada" => "Retiró de la carga académica una materia sin estudiantes",
            "docente_reasignado" => $"Cambió el docente de {oferta}",
            "carga_copiada" => $"Copió la carga académica al período {periodo}: {Cantidad(Numero(2), "materia", "materias")}",
            "periodo_cerrado" => $"Cerró el período {periodo}",
            "alta_periodos" => $"Creó el período {periodo}",
            "renombre_periodos" => $"Corrigió el nombre del período {periodo}",
            "alta_carreras" => $"Creó la carrera {carrera}",
            "carrera_corregida" or "renombre_carreras" => $"Corrigió la carrera {carrera}",
            "alta_seccion" => $"Creó la sección {r.Seccion ?? Parte(1)}",
            "seccion_corregida" => $"Corrigió la sección {r.Seccion ?? Parte(1)}",
            "formato_seccion" => "Cambió el formato del código de sección",
            "alta_materia" => $"Creó la materia {materia}",
            "materia_corregida" => $"Corrigió la materia {materia}",
            "pensum_actualizado" => $"Ubicó {materia} en el pensum de {carrera}",
            "pensum_quitada" => $"Quitó {materia} del pensum de {carrera}",
            "pensum_importado" => $"Importó el pensum de {carrera}",
            "reglas_calificacion" => "Cambió las reglas de calificación",
            "cuenta_creada" => $"Creó la cuenta de {persona}",
            "correo_corregido" => $"Corrigió el correo de {persona}",
            "codigo_acceso_emitido" => $"Emitió un código de acceso para {persona}",
            "cuenta_activada" => $"Activó la cuenta de {persona}",
            "cuenta_desactivada" => $"Desactivó la cuenta de {persona}",
            "administrador_inicial" or "configuracion_inicial" => "Configuró Mérito y creó la cuenta de administración",
            _ => char.ToUpperInvariant(r.Accion[0]) + r.Accion[1..].Replace('_', ' '),
        };
    }
}
