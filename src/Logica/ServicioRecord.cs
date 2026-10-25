using Entidades;

namespace Logica;

public sealed class ServicioRecord(IAlmacen almacen)
{
    public async Task<RecordAcademico> Record(Persona actor, CancellationToken ct)
    {
        Permisos.SoloEstudiante(actor);
        var (filas, reglas) = await almacen.Leer(async d =>
            (await d.ResultadosPublicados(actor.Id), Calificacion.Leer(await d.Configuracion(Calificacion.Claves, Bloqueo.Ninguno))), ct);
        return Construir(actor, filas, reglas);
    }

    /// <summary>
    /// Arma el récord con la última publicación de cada materia. El índice usa solo períodos cerrados, con las UC,
    /// la nota aprobatoria y el redondeo que regían al cerrar; los abiertos se muestran con las reglas vigentes.
    /// </summary>
    public static RecordAcademico Construir(Persona estudiante, IEnumerable<ResultadoPublicado> filas, ReglasCalificacion reglas)
    {
        var materias = Materias(filas, reglas);
        var cerradas = materias.Where(m => m.PeriodoCerrado && m.NotaFinal.HasValue).ToList();
        var indice = Calificacion.Indice(cerradas.Select(m => (m.NotaFinal!.Value, m.Uc, m.Condicion == Condiciones.Aprobado)), reglas);
        var ucAprobadas = cerradas.Where(m => m.Condicion == Condiciones.Aprobado).Sum(m => m.Uc);
        return new RecordAcademico(estudiante, materias, indice, reglas.Indice, reglas.EscalaMaxima, ucAprobadas);
    }

    /// <summary>Código de cada materia aprobada en un período cerrado y el período en que se aprobó.</summary>
    public static Dictionary<string, string> Aprobadas(IEnumerable<ResultadoPublicado> filas, ReglasCalificacion reglas) =>
        Materias(filas, reglas).Where(m => m.PeriodoCerrado && m.Condicion == Condiciones.Aprobado)
            .GroupBy(m => m.Codigo, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Periodo, StringComparer.OrdinalIgnoreCase);

    private static List<MateriaRecord> Materias(IEnumerable<ResultadoPublicado> filas, ReglasCalificacion reglas) =>
        filas.GroupBy(f => f.OfertaId).Select(grupo =>
        {
            var primera = grupo.First();
            var aprobatoria = primera.AprobatoriaCierre ?? primera.AprobatoriaMateria ?? reglas.NotaAprobatoria;
            var (acumulado, final) = Calificacion.Calcular(grupo.Select(f => (f.Peso, f.Nota)).ToList(), primera.RedondeoCierre ?? reglas.Redondeo);
            var detalles = grupo.Select(f => new DetallePublicado(f.Evaluacion, f.Peso, f.Nota,
                f.Nota.HasValue ? Calificacion.PorcentajeObtenido(f.Nota.Value, f.Peso, reglas.EscalaMaxima) : null,
                f.Nota.HasValue ? Calificacion.Aporte(f.Nota.Value, f.Peso) : null)).ToList();
            return new MateriaRecord(grupo.Key, primera.Periodo, primera.Carrera, primera.Seccion, primera.Materia,
                primera.PublicadoEn, primera.PeriodoCerrado, detalles, acumulado,
                grupo.Where(f => f.Nota.HasValue).Sum(f => f.Peso), grupo.Sum(f => f.Peso),
                final.HasValue, final, final is { } nota ? Calificacion.Condicion(nota, aprobatoria) : null,
                primera.Codigo, primera.Uc, reglas.EscalaMaxima);
        }).ToList();
}
