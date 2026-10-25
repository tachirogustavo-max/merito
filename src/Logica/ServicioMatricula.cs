using Entidades;

namespace Logica;

public sealed record FilaCruda(int Numero, string Cedula, string Nombres, string Apellidos, string Carrera, string Seccion, bool TieneFormula,
    string? Correo = null, string? Materias = null);

public sealed class ServicioMatricula(IAlmacen almacen)
{
    public const long TamanoMaximoArchivo = 1024 * 1024;
    // Un archivo para todo el período: varias carreras y secciones, una fila por estudiante y sección.
    public const int MaximoFilasPeriodo = 20000;
    private static readonly char[] SeparadoresMaterias = [',', ';', '/', ' ', '\t', '\n', '\r'];

    /// <summary>Valida cada fila por separado para informar todos los problemas de una vez.</summary>
    public static VistaImportacion Revisar(IReadOnlyList<FilaCruda> crudas)
    {
        if (crudas.Count is < 1 or > ReglasAcademicas.MaximoInscritos)
            throw new ReglaException($"La hoja debe contener entre 1 y {ReglasAcademicas.MaximoInscritos} estudiantes.");
        var filas = new List<FilaMatricula>();
        var errores = new List<ErrorFila>();
        var cedulas = new HashSet<string>();
        var correos = new HashSet<string>();
        foreach (var cruda in crudas)
        {
            try
            {
                var fila = Normalizar(Fila(cruda));
                if (!cedulas.Add(fila.Cedula)) throw new ReglaException("Cédula repetida en el archivo.");
                if (fila.Correo is { } correo && !correos.Add(correo)) throw new ReglaException($"El correo {correo} está repetido en el archivo.");
                filas.Add(fila);
            }
            catch (ReglaException error) { errores.Add(new ErrorFila(cruda.Numero, error.Message)); }
        }
        return new VistaImportacion(filas, errores);
    }

    private static FilaMatricula Fila(FilaCruda cruda) => cruda.TieneFormula
        ? throw new ReglaException("No se admiten fórmulas en la matrícula.")
        : new FilaMatricula(cruda.Cedula, cruda.Nombres, cruda.Apellidos, cruda.Carrera, cruda.Seccion, cruda.Correo, cruda.Materias);

    private static FilaMatricula Normalizar(FilaMatricula fila)
    {
        var codigos = Codigos(fila.Materias);
        if (codigos.Count > 30 || codigos.Any(c => c.Length > 20)) throw new ReglaException("Materias: escribe los códigos separados por coma, por ejemplo MAT-101, FIS-101.");
        return new(ReglasAcademicas.Cedula(fila.Cedula),
            ReglasAcademicas.Texto(fila.Nombres, "Nombres", 100), ReglasAcademicas.Texto(fila.Apellidos, "Apellidos", 100),
            ReglasAcademicas.Texto(fila.Carrera, "Carrera"), ReglasAcademicas.Texto(fila.Seccion, "Sección", 60), ReglasAcademicas.Correo(fila.Correo),
            codigos.Count == 0 ? null : string.Join(", ", codigos));
    }

    private static List<string> Codigos(string? materias) => (materias ?? "")
        .Split(SeparadoresMaterias, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(c => c.ToUpperInvariant()).Distinct().ToList();

    public static void ValidarTamano(long? bytes)
    {
        if (bytes is null or <= 0 or > TamanoMaximoArchivo) throw new ReglaException("El archivo debe pesar como máximo 1 MB.");
    }

    // Qué hacer con una materia para un estudiante, según lo que ya tiene en el período y lo que ya aprobó.
    private enum Paso { Nueva, Confirmar, Reactivar, YaEstaba, Omitir }

    private static (Paso Paso, string? Motivo, InscripcionDelPeriodo? Propia) Decidir(OfertaMatricula oferta, IReadOnlyList<InscripcionDelPeriodo> actuales,
        IReadOnlyDictionary<string, string> aprobadas, bool confirmarPendientes, bool reactivar)
    {
        var propia = actuales.FirstOrDefault(i => i.OfertaId == oferta.Id);
        if (propia?.Estado == "confirmada") return (Paso.YaEstaba, null, propia);
        if (propia?.Estado == "pendiente") return (confirmarPendientes ? Paso.Confirmar : Paso.YaEstaba, null, propia);
        if (actuales.FirstOrDefault(i => i.MateriaId == oferta.MateriaId && i.OfertaId != oferta.Id && i.Estado != "retirada") is { } otra)
            return (Paso.Omitir, $"Ya la cursa en la sección {otra.Seccion}.", propia);
        if (aprobadas.TryGetValue(oferta.Codigo, out var periodo)) return (Paso.Omitir, $"Ya la aprobó en {periodo}.", propia);
        if (propia is not null)
            return reactivar ? (Paso.Reactivar, null, propia)
                : (Paso.Omitir, "Lo retiraron de esta materia. Si debe volver, impórtalo desde la planilla de la materia.", propia);
        return (Paso.Nueva, null, null);
    }

    private static async Task<Dictionary<string, string>> Aprobadas(IDatos d, long estudiante) =>
        ServicioRecord.Aprobadas(await d.ResultadosPublicados(estudiante), Calificacion.Leer(await d.Configuracion(Calificacion.Claves, Bloqueo.Ninguno)));

    private static bool MismaIdentidad(UsuarioExistente usuario, FilaMatricula fila) => usuario.Rol == Roles.Estudiante
        && ReglasAcademicas.MismoTexto(usuario.Nombres, fila.Nombres) && ReglasAcademicas.MismoTexto(usuario.Apellidos, fila.Apellidos);

    // El correo decide quién puede registrarse con esa cuenta: solo lo guarda la matrícula de administración,
    // y nunca reemplaza el de una cuenta que ya se registró.
    private static bool GuardaCorreo(UsuarioExistente? usuario, FilaMatricula fila) =>
        fila.Correo is not null && usuario is not { Registrada: true } && usuario?.Correo != fila.Correo;

    public Task<ResultadoImportacion> Importar(Persona actor, long ofertaId, ImportarMatricula solicitud, CancellationToken ct)
    {
        Permisos.NoEstudiante(actor);
        var filas = ReglasAcademicas.Lista(solicitud?.Filas, 1, ReglasAcademicas.MaximoInscritos,
                $"Cada importación admite de 1 a {ReglasAcademicas.MaximoInscritos} estudiantes, sin filas vacías.").Select(Normalizar).OrderBy(f => f.Cedula, StringComparer.Ordinal).ToList();
        if (filas.Select(f => f.Cedula).Distinct().Count() != filas.Count) throw new ReglaException("El listado repite una o más cédulas.");
        if (filas.Where(f => f.Correo is not null).GroupBy(f => f.Correo).Any(g => g.Count() > 1)) throw new ReglaException("El listado repite uno o más correos.");
        var confirmada = actor.Rol == Roles.Administrador;
        return almacen.Transaccion(async d =>
        {
            var oferta = await ServicioAcademico.PrepararEdicion(d, ofertaId, actor, solicitud!.Revision);
            if (filas.Any(f => !ReglasAcademicas.MismoTexto(f.Carrera, oferta.Carrera) || !ReglasAcademicas.MismoTexto(f.Seccion, oferta.Seccion)))
                throw new ReglaException($"La carrera y sección del archivo deben coincidir con la asignación seleccionada: {oferta.Carrera}, sección {oferta.Seccion}.");
            var materia = (await d.OfertasParaMatricula(oferta.PeriodoId)).Single(o => o.Id == ofertaId);
            var nuevos = 0;
            var existentes = 0;
            var omitidas = new List<Omision>();
            foreach (var fila in filas)
            {
                var usuario = await d.BloquearUsuarioPorCedula(fila.Cedula);
                if (usuario is not null && !MismaIdentidad(usuario, fila))
                    throw new ReglaException($"La cédula {fila.Cedula} ya existe con otra identidad o cargo. Revisa el archivo.", TipoErrorRegla.Conflicto);
                var estudiante = usuario?.Id ?? await d.InsertarUsuario(fila.Cedula, fila.Nombres, fila.Apellidos, Roles.Estudiante, null);
                if (confirmada && GuardaCorreo(usuario, fila))
                {
                    await ServicioIdentidad.ExigirCorreoLibre(d, fila.Correo!, estudiante);
                    await d.ActualizarCorreo(estudiante, fila.Correo);
                }
                (Paso Paso, string? Motivo, InscripcionDelPeriodo? Propia) decision = (Paso.Nueva, null, null);
                if (usuario is not null)
                    decision = Decidir(materia, await d.InscripcionesDelPeriodo(estudiante, oferta.PeriodoId), await Aprobadas(d, estudiante), false, confirmada);
                var (paso, motivo, propia) = decision;
                if (paso == Paso.YaEstaba) { existentes++; continue; }
                if (paso == Paso.Omitir) { omitidas.Add(new Omision(fila.Cedula, $"{fila.Nombres} {fila.Apellidos}", materia.Materia, motivo!)); continue; }
                if (materia.Vigentes + nuevos >= ReglasAcademicas.MaximoInscritos)
                    throw new ReglaException($"Esta versión admite hasta {ReglasAcademicas.MaximoInscritos} estudiantes por asignación.");
                if (paso == Paso.Reactivar) await d.ReactivarInscripcion(propia!.Id, ofertaId, actor.Id);
                else await d.InsertarInscripcion(ofertaId, estudiante, confirmada, actor.Id, confirmada ? actor.Id : null);
                nuevos++;
            }
            if (nuevos > 0) await d.IncrementarRevision(ofertaId);
            await d.Auditar(actor.Id, "matricula_importada", ofertaId);
            return new ResultadoImportacion(nuevos, existentes, confirmada ? "confirmada" : "pendiente", omitidas);
        }, ct);
    }

    /// <summary>Revisa la matrícula del período sin guardar nada: qué se inscribe, qué ya estaba y qué se omite.</summary>
    public Task<VistaMatriculaPeriodo> RevisarPeriodo(Persona actor, long periodoId, IReadOnlyList<FilaCruda> crudas, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        if (crudas.Count is < 1 or > MaximoFilasPeriodo)
            throw new ReglaException($"La hoja debe contener entre 1 y {MaximoFilasPeriodo} filas.");
        var errores = new List<ErrorFila>();
        var filas = new List<(int, FilaMatricula)>();
        foreach (var cruda in crudas)
        {
            try { filas.Add((cruda.Numero, Normalizar(Fila(cruda)))); }
            catch (ReglaException error) { errores.Add(new ErrorFila(cruda.Numero, error.Message)); }
        }
        return almacen.Leer(d => Planear(d, actor, periodoId, filas, errores, aplicar: false), ct);
    }

    public Task<ResultadoMatriculaPeriodo> ImportarPeriodo(Persona actor, long periodoId, ImportarMatriculaPeriodo solicitud, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        var filas = ReglasAcademicas.Lista(solicitud?.Filas, 1, MaximoFilasPeriodo, $"La matrícula del período admite de 1 a {MaximoFilasPeriodo} filas, sin filas vacías.")
            .Select((fila, i) => (i + 1, Normalizar(fila))).ToList();
        return almacen.Transaccion(async d =>
        {
            var plan = await Planear(d, actor, periodoId, filas, [], aplicar: true);
            return new ResultadoMatriculaPeriodo(plan.Estudiantes, plan.Inscripciones, plan.YaEstaban, plan.Omitidas);
        }, ct);
    }

    /// <summary>
    /// Cada fila inscribe al estudiante en todas las materias de su sección, o solo en las de la columna materias.
    /// Al revisar se juntan todos los problemas; al aplicar, el primero cancela todo.
    /// Bloqueos en el orden de siempre: período, asignaciones y luego cuentas.
    /// </summary>
    private static async Task<VistaMatriculaPeriodo> Planear(IDatos d, Persona actor, long periodoId, List<(int Numero, FilaMatricula Fila)> filas,
        List<ErrorFila> errores, bool aplicar)
    {
        void Fallar(int numero, string problema)
        {
            if (aplicar) throw new ReglaException($"Fila {numero}: {problema}");
            errores.Add(new ErrorFila(numero, problema));
        }
        var periodo = (aplicar ? await d.BloquearPeriodo(periodoId) : (await d.Periodos()).FirstOrDefault(p => p.Id == periodoId))
            ?? throw new ReglaException("El período no existe.", TipoErrorRegla.NoEncontrado);
        if (periodo.Cerrado) throw new ReglaException("El período está cerrado. No se admiten cambios.", TipoErrorRegla.Conflicto);
        var secciones = await d.Secciones();
        var ofertas = await d.OfertasParaMatricula(periodoId);
        var porSeccion = ofertas.ToLookup(o => o.SeccionId);

        var destinos = new List<(int Numero, FilaMatricula Fila, Seccion Seccion, List<OfertaMatricula> Ofertas)>();
        foreach (var (numero, fila) in filas)
        {
            var candidatas = secciones.Where(s => ReglasAcademicas.MismoTexto(s.Carrera, fila.Carrera)
                && (ReglasAcademicas.MismoTexto(s.Codigo, fila.Seccion) || ReglasAcademicas.MismoTexto(s.Nombre, fila.Seccion))).ToList();
            if (candidatas.Count != 1)
            {
                // Casi siempre la carrera viene escrita distinto, sin tilde por ejemplo: se dice eso y no que falta la sección.
                var conCarrera = secciones.Any(s => ReglasAcademicas.MismoTexto(s.Carrera, fila.Carrera));
                Fallar(numero, candidatas.Count > 1 ? $"Hay varias secciones {fila.Seccion} en {fila.Carrera}: usa el código completo."
                    : conCarrera ? $"No existe la sección {fila.Seccion} en {fila.Carrera}."
                    : $"No hay ninguna carrera «{fila.Carrera}» con secciones. Escríbela igual que en Mérito, con sus tildes.");
                continue;
            }
            var seccion = candidatas[0];
            var propias = porSeccion[seccion.Id].ToList();
            if (propias.Count == 0) { Fallar(numero, $"La sección {seccion.Codigo} no tiene materias asignadas en {periodo.Nombre}."); continue; }
            var codigos = Codigos(fila.Materias);
            var faltante = codigos.FirstOrDefault(c => !propias.Any(o => string.Equals(o.Codigo, c, StringComparison.OrdinalIgnoreCase)));
            if (faltante is not null) { Fallar(numero, $"La sección {seccion.Codigo} no tiene la materia {faltante} en {periodo.Nombre}."); continue; }
            destinos.Add((numero, fila, seccion, codigos.Count == 0 ? propias
                : propias.Where(o => codigos.Contains(o.Codigo, StringComparer.OrdinalIgnoreCase)).ToList()));
        }

        // Dentro del archivo: la misma persona siempre igual y cada materia una sola vez.
        var porCedula = destinos.GroupBy(x => x.Fila.Cedula).OrderBy(g => g.Key, StringComparer.Ordinal).ToList();
        foreach (var grupo in porCedula)
        {
            var primera = grupo.First();
            foreach (var otra in grupo.Skip(1).Where(x => !ReglasAcademicas.MismoTexto(x.Fila.Nombres, primera.Fila.Nombres)
                || !ReglasAcademicas.MismoTexto(x.Fila.Apellidos, primera.Fila.Apellidos) || (x.Fila.Correo ?? primera.Fila.Correo) != (primera.Fila.Correo ?? x.Fila.Correo)))
                Fallar(otra.Numero, $"La cédula {Cedulas.Mostrar(grupo.Key)} está en la fila {primera.Numero} con otro nombre o correo.");
            foreach (var repetida in grupo.SelectMany(x => x.Ofertas.Select(o => (x.Numero, Oferta: o))).GroupBy(t => t.Oferta.MateriaId).Where(g => g.Count() > 1))
                Fallar(repetida.Last().Numero, $"{repetida.First().Oferta.Codigo} aparece dos veces para la cédula {Cedulas.Mostrar(grupo.Key)} (filas {string.Join(" y ", repetida.Select(t => t.Numero))}).");
        }
        foreach (var compartido in destinos.Where(x => x.Fila.Correo is not null).GroupBy(x => x.Fila.Correo).Where(g => g.Select(x => x.Fila.Cedula).Distinct().Count() > 1))
            Fallar(compartido.Last().Numero, $"El correo {compartido.Key} está en varias cédulas del archivo.");
        // Al revisar, quien ya tiene un error en el archivo no se compara con la base: su fila se corrige primero.
        var conError = errores.Select(e => e.Fila).ToHashSet();
        porCedula = porCedula.Where(g => !g.Any(x => conError.Contains(x.Numero))).ToList();

        if (aplicar) await d.BloquearOfertas(destinos.SelectMany(x => x.Ofertas.Select(o => o.Id)).Distinct().Order().ToList());
        var vigentes = ofertas.ToDictionary(o => o.Id, o => o.Vigentes);
        var tocadas = new SortedSet<long>();
        var plan = new List<PlanFila>();
        var omitidas = new List<Omision>();
        var (inscripciones, yaEstaban) = (0, 0);
        foreach (var grupo in porCedula)
        {
            var fila = grupo.First().Fila;
            var nombre = $"{fila.Nombres} {fila.Apellidos}";
            var correo = grupo.Select(x => x.Fila.Correo).FirstOrDefault(c => c is not null);
            var usuario = aplicar ? await d.BloquearUsuarioPorCedula(grupo.Key) : await d.BuscarUsuarioPorCedula(grupo.Key);
            if (usuario is not null && !MismaIdentidad(usuario, fila))
            {
                Fallar(grupo.First().Numero, $"La cédula {Cedulas.Mostrar(grupo.Key)} ya existe con otra identidad o cargo.");
                continue;
            }
            if (correo is not null && GuardaCorreo(usuario, fila with { Correo = correo }))
            {
                try { await ServicioIdentidad.ExigirCorreoLibre(d, correo, usuario?.Id); }
                catch (ReglaException error) { Fallar(grupo.First().Numero, error.Message); continue; }
            }
            long? estudiante = usuario?.Id;
            if (aplicar)
            {
                estudiante ??= await d.InsertarUsuario(fila.Cedula, fila.Nombres, fila.Apellidos, Roles.Estudiante, null);
                if (correo is not null && GuardaCorreo(usuario, fila with { Correo = correo })) await d.ActualizarCorreo(estudiante.Value, correo);
            }
            var actuales = usuario is null ? new List<InscripcionDelPeriodo>() : await d.InscripcionesDelPeriodo(usuario.Id, periodoId);
            var aprobadas = usuario is null ? new Dictionary<string, string>() : await Aprobadas(d, usuario.Id);
            foreach (var destino in grupo)
            {
                var (nuevas, ya) = (0, 0);
                var notas = new List<string>();
                foreach (var oferta in destino.Ofertas)
                {
                    var (paso, motivo, propia) = Decidir(oferta, actuales, aprobadas, confirmarPendientes: true, reactivar: false);
                    if (paso == Paso.YaEstaba) { ya++; continue; }
                    if (paso == Paso.Omitir)
                    {
                        omitidas.Add(new Omision(fila.Cedula, nombre, $"{oferta.Codigo} {oferta.Materia}", motivo!));
                        notas.Add($"{oferta.Codigo}: {motivo}");
                        continue;
                    }
                    if (paso == Paso.Nueva && vigentes[oferta.Id]++ >= ReglasAcademicas.MaximoInscritos)
                    {
                        Fallar(destino.Numero, $"{oferta.Codigo} en {destino.Seccion.Codigo} llegó al máximo de {ReglasAcademicas.MaximoInscritos} estudiantes.");
                        continue;
                    }
                    if (aplicar)
                    {
                        if (paso == Paso.Confirmar) await d.ConfirmarInscripcion(propia!.Id, oferta.Id, actor.Id);
                        else await d.InsertarInscripcion(oferta.Id, estudiante!.Value, true, actor.Id, actor.Id);
                    }
                    tocadas.Add(oferta.Id);
                    nuevas++;
                }
                (inscripciones, yaEstaban) = (inscripciones + nuevas, yaEstaban + ya);
                var resumen = nuevas > 0 ? $"Queda en {Cantidad(nuevas, "materia", "materias")}" : ya > 0 ? "Ya estaba inscrito" : "No se inscribe";
                if (nuevas > 0 && ya > 0) resumen += $"; ya tenía {ya}";
                plan.Add(new PlanFila(destino.Numero, fila.Cedula, nombre, destino.Seccion.Codigo, nuevas,
                    notas.Count == 0 ? resumen + "." : $"{resumen}. {string.Join(" ", notas)}", correo ?? ""));
            }
        }
        if (aplicar)
        {
            foreach (var id in tocadas) await d.IncrementarRevision(id);
            await d.Auditar(actor.Id, "matricula_periodo_importada", $"{periodoId}:{inscripciones}");
        }
        return new VistaMatriculaPeriodo(filas.Select(f => f.Fila).ToList(), [.. plan.OrderBy(p => p.Fila)], [.. errores.OrderBy(e => e.Fila)],
            porCedula.Count, inscripciones, yaEstaban, omitidas);
    }

    private static string Cantidad(int n, string uno, string varios) => $"{n} {(n == 1 ? uno : varios)}";

    public Task<int> Confirmar(Persona actor, long ofertaId, ConfirmarMatricula solicitud, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        ValidarSeleccion(solicitud, "Selecciona las inscripciones pendientes que deseas confirmar.");
        if (solicitud.Inscripciones.Distinct().Count() != solicitud.Inscripciones.Count)
            throw new ReglaException("Selecciona las inscripciones pendientes que deseas confirmar.");
        return almacen.Transaccion(async d =>
        {
            await ServicioAcademico.PrepararEdicion(d, ofertaId, actor, solicitud.Revision);
            foreach (var id in solicitud.Inscripciones)
                if (await d.ConfirmarInscripcion(id, ofertaId, actor.Id) != 1)
                    throw new ReglaException("Una inscripción ya fue revisada o no pertenece a esta asignación.", TipoErrorRegla.Conflicto);
            await d.IncrementarRevision(ofertaId);
            await d.Auditar(actor.Id, "matricula_confirmada", ofertaId);
            return solicitud.Inscripciones.Count;
        }, ct);
    }

    public Task<int> Rechazar(Persona actor, long ofertaId, ConfirmarMatricula solicitud, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        ValidarSeleccion(solicitud, "Selecciona inscripciones pendientes.");
        return almacen.Transaccion(async d =>
        {
            await ServicioAcademico.PrepararEdicion(d, ofertaId, actor, solicitud.Revision);
            foreach (var id in solicitud.Inscripciones.Distinct())
                if (await d.EliminarInscripcionPendiente(id, ofertaId) != 1)
                    throw new ReglaException("La inscripción ya fue revisada o no pertenece a esta asignación.", TipoErrorRegla.Conflicto);
            await d.IncrementarRevision(ofertaId);
            await d.Auditar(actor.Id, "matricula_propuesta_rechazada", ofertaId);
            return solicitud.Inscripciones.Count;
        }, ct);
    }

    /// <summary>
    /// Saca de la materia a estudiantes confirmados que no la cursan. No borra nada: sus notas y cambios quedan
    /// en la historia, dejan de verla y pueden inscribirse en otra sección. Solo con el período abierto.
    /// </summary>
    public Task<int> Retirar(Persona actor, long ofertaId, ConfirmarMatricula solicitud, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        ValidarSeleccion(solicitud, "Selecciona los estudiantes confirmados que quieres retirar de la materia.");
        return almacen.Transaccion(async d =>
        {
            await ServicioAcademico.PrepararEdicion(d, ofertaId, actor, solicitud.Revision);
            foreach (var id in solicitud.Inscripciones.Distinct())
                if (await d.RetirarInscripcion(id, ofertaId) != 1)
                    throw new ReglaException("Solo se retiran inscripciones confirmadas de esta materia; las pendientes se rechazan.", TipoErrorRegla.Conflicto);
            await d.IncrementarRevision(ofertaId);
            var retiradas = solicitud.Inscripciones.Distinct().Count();
            await d.Auditar(actor.Id, "matricula_retirada", $"{ofertaId}:{retiradas}");
            return retiradas;
        }, ct);
    }

    private static void ValidarSeleccion(ConfirmarMatricula solicitud, string mensaje)
    {
        if (solicitud.Inscripciones is null || solicitud.Inscripciones.Count is < 1 or > ReglasAcademicas.MaximoInscritos)
            throw new ReglaException(mensaje);
    }
}
