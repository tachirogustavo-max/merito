using Entidades;

namespace Logica;

public sealed class ServicioCalificaciones(IAlmacen almacen)
{
    public Task<long> CrearEvaluacion(Persona actor, long ofertaId, NuevaEvaluacion nueva, CancellationToken ct)
    {
        Permisos.SoloDocente(actor);
        var nombre = ReglasAcademicas.Texto(nueva.Nombre, "Evaluación", 100);
        ReglasAcademicas.ValidarPeso(nueva.Peso);
        return almacen.Transaccion(async d =>
        {
            await ServicioAcademico.PrepararEdicion(d, ofertaId, actor, nueva.Revision);
            if ((await d.Evaluaciones(ofertaId)).Count >= ReglasAcademicas.MaximoEvaluaciones)
                throw new ReglaException($"Esta versión admite hasta {ReglasAcademicas.MaximoEvaluaciones} evaluaciones por asignación.");
            ReglasAcademicas.ValidarPlan(await d.PesoPlanificado(ofertaId), nueva.Peso);
            var id = await d.InsertarEvaluacion(ofertaId, nombre, nueva.Peso, await d.SiguienteOrden(ofertaId));
            await d.IncrementarRevision(ofertaId);
            await d.Auditar(actor.Id, "evaluacion_creada", id);
            return id;
        }, ct);
    }

    public Task<int> EditarEvaluacion(Persona actor, long ofertaId, long evaluacionId, NuevaEvaluacion cambio, CancellationToken ct)
    {
        Permisos.SoloDocente(actor);
        var nombre = ReglasAcademicas.Texto(cambio.Nombre, "Evaluación", 100);
        ReglasAcademicas.ValidarPeso(cambio.Peso);
        return almacen.Transaccion(async d =>
        {
            await ServicioAcademico.PrepararEdicion(d, ofertaId, actor, cambio.Revision);
            if (!await d.EvaluacionPertenece(evaluacionId, ofertaId)) throw new ReglaException("La evaluación no pertenece a esta materia.", TipoErrorRegla.NoEncontrado);
            ReglasAcademicas.ValidarPlan(await d.PesoPlanificado(ofertaId, evaluacionId), cambio.Peso);
            await d.ActualizarEvaluacion(evaluacionId, nombre, cambio.Peso);
            await d.IncrementarRevision(ofertaId);
            await d.Auditar(actor.Id, "evaluacion_editada", evaluacionId);
            return cambio.Revision + 1;
        }, ct);
    }

    public Task<int> EliminarEvaluacion(Persona actor, long ofertaId, long evaluacionId, int revision, CancellationToken ct)
    {
        Permisos.SoloDocente(actor);
        return almacen.Transaccion(async d =>
        {
            await ServicioAcademico.PrepararEdicion(d, ofertaId, actor, revision);
            if (await d.UsosDeEvaluacion(evaluacionId) > 0)
                throw new ReglaException("La evaluación ya tiene notas o publicaciones. Se puede corregir, pero no eliminar.", TipoErrorRegla.Conflicto);
            if (await d.EliminarEvaluacion(evaluacionId, ofertaId) != 1) throw new ReglaException("La evaluación no existe en esta asignación.", TipoErrorRegla.NoEncontrado);
            await d.IncrementarRevision(ofertaId);
            await d.Auditar(actor.Id, "evaluacion_eliminada", evaluacionId);
            return revision + 1;
        }, ct);
    }

    public Task<int> GuardarNotas(Persona actor, long ofertaId, GuardarNotas solicitud, CancellationToken ct)
    {
        Permisos.SoloDocente(actor);
        var lote = ReglasAcademicas.Lista(solicitud?.Notas, 1, 10000, "El lote debe contener entre 1 y 10000 notas, sin elementos vacíos.");
        if (lote.Select(n => (n.EvaluacionId, n.EstudianteId)).Distinct().Count() != lote.Count)
            throw new ReglaException("El lote contiene notas repetidas.");
        return almacen.Transaccion(async d =>
        {
            var oferta = await ServicioAcademico.PrepararEdicion(d, ofertaId, actor, solicitud!.Revision);
            // La escala se lee con bloqueo compartido: no puede cambiar mientras se guardan estas notas.
            var reglas = await ServicioAcademico.ReglasDeOferta(d, ofertaId, Bloqueo.Compartido);
            foreach (var nota in solicitud.Notas) Calificacion.ValidarNota(nota.Valor, reglas);
            var evaluaciones = (await d.Evaluaciones(ofertaId)).Select(e => e.Id).ToHashSet();
            var inscripciones = await d.InscripcionesConfirmadas(ofertaId);
            var anteriores = await d.NotasGuardadas(ofertaId);
            var revision = oferta.Revision + 1;
            foreach (var nota in solicitud.Notas)
            {
                if (!evaluaciones.Contains(nota.EvaluacionId) || !inscripciones.TryGetValue(nota.EstudianteId, out var inscripcion))
                    throw new ReglaException("Una evaluación o estudiante no pertenece a esta planilla confirmada.", TipoErrorRegla.AccesoDenegado);
                decimal? anterior = anteriores.TryGetValue((nota.EvaluacionId, inscripcion), out var valor) ? valor : null;
                if (anterior == nota.Valor) continue;
                await d.RegistrarCambioNota(ofertaId, nota.EvaluacionId, inscripcion, actor.Id, revision, anterior, nota.Valor);
                if (nota.Valor is { } nueva) await d.GuardarNota(ofertaId, nota.EvaluacionId, inscripcion, nueva);
                else await d.EliminarNota(nota.EvaluacionId, inscripcion);
            }
            await d.IncrementarRevision(ofertaId);
            await d.Auditar(actor.Id, "notas_guardadas", ofertaId);
            return revision;
        }, ct);
    }

    /// <summary>Publica una copia inmutable de la revisión indicada; repetir la solicitud no duplica versiones.</summary>
    public Task<long> Publicar(Persona actor, long ofertaId, int revision, CancellationToken ct)
    {
        Permisos.SoloDocente(actor);
        return almacen.Transaccion(async d =>
        {
            var oferta = await ServicioAcademico.PrepararEdicion(d, ofertaId, actor, revision);
            var existente = await d.PublicacionDeRevision(ofertaId, revision);
            if (existente != 0) return existente;
            if ((await d.Evaluaciones(ofertaId)).Count == 0 || oferta.Matriculados == 0)
                throw new ReglaException("Para publicar debe haber evaluaciones y matrícula confirmada.");
            var publicacion = await d.InsertarPublicacion(ofertaId, revision, actor.Id);
            await d.CopiarResultadosPublicados(publicacion, ofertaId);
            await d.Auditar(actor.Id, "notas_publicadas", publicacion);
            return publicacion;
        }, ct);
    }
}
