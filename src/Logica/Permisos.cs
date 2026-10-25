using Entidades;

namespace Logica;

public static class Permisos
{
    public static void SoloAdministrador(Persona actor)
    {
        if (actor.Rol != Roles.Administrador) throw new ReglaException("Esta operación requiere administración.", TipoErrorRegla.AccesoDenegado);
    }

    public static void SoloDocente(Persona actor)
    {
        if (actor.Rol != Roles.Docente) throw new ReglaException("La carga y publicación corresponden al docente asignado.", TipoErrorRegla.AccesoDenegado);
    }

    public static void SoloEstudiante(Persona actor)
    {
        if (actor.Rol != Roles.Estudiante) throw new ReglaException("El récord personal corresponde a una cuenta de estudiante.", TipoErrorRegla.AccesoDenegado);
    }

    public static void NoEstudiante(Persona actor)
    {
        if (actor.Rol == Roles.Estudiante) throw new ReglaException("Consulta tu récord académico.", TipoErrorRegla.AccesoDenegado);
    }

    public static void PuedeVerOferta(Persona actor, Oferta oferta)
    {
        if (actor.Rol != Roles.Administrador && (actor.Rol != Roles.Docente || oferta.DocenteId != actor.Id))
            throw new ReglaException("No tienes acceso a esta materia y sección.", TipoErrorRegla.AccesoDenegado);
    }

    public static string RolValido(string? rol) => rol is Roles.Docente or Roles.Estudiante or Roles.Administrador
        ? rol : throw new ReglaException("Selecciona un cargo válido.");
}
