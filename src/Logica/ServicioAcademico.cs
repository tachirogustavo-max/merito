using Entidades;

namespace Logica;

public sealed class ServicioAcademico(IAlmacen almacen)
{
    public static Catalogo LeerCatalogo(string tipo, bool admitePeriodos) => tipo switch
    {
        "carreras" => Catalogo.Carreras,
        "periodos" when admitePeriodos => Catalogo.Periodos,
        _ => throw new ReglaException("Catálogo desconocido.")
    };

    /// <summary>Bloquea la planilla y comprueba acceso, período abierto y revisión esperada.</summary>
    internal static async Task<Oferta> PrepararEdicion(IDatos d, long ofertaId, Persona actor, int? revision, bool editar = true)
    {
        var oferta = await d.BloquearOferta(ofertaId) ?? throw new ReglaException("La asignación no existe.", TipoErrorRegla.NoEncontrado);
        Permisos.PuedeVerOferta(actor, oferta);
        if (editar && oferta.Cerrado) throw new ReglaException("El período está cerrado. No se admiten cambios.", TipoErrorRegla.Conflicto);
        if (revision.HasValue && oferta.Revision != revision.Value)
            throw new ReglaException("Otra operación modificó esta planilla. Recarga la planilla antes de continuar.", TipoErrorRegla.Conflicto);
        return oferta;
    }

    public Task<List<Periodo>> Periodos(Persona actor, CancellationToken ct) {
        Permisos.SoloAdministrador(actor);
        return almacen.Leer(d => d.Periodos(), ct);
    }

    public Task<List<Referencia>> Referencias(Persona actor, string tipo, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        var catalogo = LeerCatalogo(tipo, admitePeriodos: false);
        return almacen.Leer(d => d.Referencias(catalogo), ct);
    }

    public Task<long> CrearReferencia(Persona actor, string tipo, string nombre, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        var catalogo = LeerCatalogo(tipo, admitePeriodos: true);
        var texto = ReglasAcademicas.Texto(nombre, "Nombre", catalogo == Catalogo.Periodos ? 60 : 120);
        return almacen.Transaccion(async d =>
        {
            var id = await d.InsertarReferencia(catalogo, texto);
            await d.Auditar(actor.Id, $"alta_{tipo}", id);
            return id;
        }, ct);
    }

    public Task<List<Seccion>> Secciones(Persona actor, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        return almacen.Leer(d => d.Secciones(), ct);
    }

    // Código de sección. Toda escritura que lo afecta bloquea primero el formato: así dos cambios nunca se mezclan.

    public Task<string> FormatoSeccion(Persona actor, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        return almacen.Leer(d => d.FormatoSeccion(), ct);
    }

    /// <summary>Cambia el formato y rearma el código de todas las secciones en la misma transacción: o cambian todas o ninguna.</summary>
    public Task<ResultadoFormato> CambiarFormatoSeccion(Persona actor, FormatoCodigo cambio, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        var formato = Logica.FormatoSeccion.Validar(cambio.Formato);
        return almacen.Transaccion(async d =>
        {
            var anterior = await d.BloquearFormatoSeccion();
            var secciones = await d.BloquearSecciones(null);
            ExigirDatosDelFormato(formato, secciones);
            await Recodificar(d, formato, secciones);
            await d.GuardarFormatoSeccion(formato);
            await d.Auditar(actor.Id, "formato_seccion", $"{anterior} > {formato}");
            return new ResultadoFormato(formato, secciones.Count);
        }, ct);
    }

    // Antes de aplicar un formato nuevo se dice todo lo que falta de una vez, no la primera sección que falle.
    private static void ExigirDatosDelFormato(string formato, IReadOnlyList<SeccionGuardada> secciones)
    {
        var sinCodigo = Logica.FormatoSeccion.UsaCarrera(formato)
            ? secciones.Where(s => s.CodigoCarrera is null).Select(s => s.Seccion.Carrera).Distinct().Order().ToList() : [];
        var sinSemestre = Logica.FormatoSeccion.UsaSemestre(formato) ? secciones.Count(s => s.Seccion.Semestre == 0) : 0;
        if (sinCodigo.Count == 0 && sinSemestre == 0) return;
        var faltas = new List<string>();
        if (sinCodigo.Count > 0)
            faltas.Add((sinCodigo.Count == 1 ? $"la carrera {sinCodigo[0]} no tiene código" : $"{sinCodigo.Count} carreras no tienen código")
                + (sinCodigo.Count is > 1 and <= 3 ? $" ({string.Join(", ", sinCodigo)})" : ""));
        if (sinSemestre > 0) faltas.Add(sinSemestre == 1 ? "una sección no tiene semestre" : $"{sinSemestre} secciones no tienen semestre");
        throw new ReglaException($"Todavía no se puede usar este formato: {string.Join(" y ", faltas)}. Complétalo en Estructura y vuelve a guardarlo; mientras tanto queda el formato actual.");
    }

    // Arma los códigos nuevos, comprueba que no se repitan dentro de una carrera y solo escribe los que cambian.
    private static async Task Recodificar(IEstructuraAcademicaDatos d, string formato, IReadOnlyList<SeccionGuardada> secciones)
    {
        var nuevos = secciones.Select(s => (s.Seccion, Codigo: Logica.FormatoSeccion.Generar(formato, s.Seccion.Carrera, s.CodigoCarrera, s.Seccion.Semestre, s.Seccion.Nombre))).ToList();
        var repetido = nuevos.GroupBy(n => (n.Seccion.CarreraId, n.Codigo.ToUpperInvariant())).FirstOrDefault(g => g.Count() > 1);
        if (repetido is not null)
            throw new ReglaException($"Con este formato dos secciones de {repetido.First().Seccion.Carrera} quedarían con el código {repetido.First().Codigo}. " +
                "Agrega {semestre} al formato o cambia el identificador de una de ellas.");
        var cambios = nuevos.Where(n => n.Codigo != n.Seccion.Codigo).Select(n => (n.Seccion.Id, n.Codigo)).ToList();
        if (cambios.Count > 0) await d.RecodificarSecciones(cambios);
    }

    public Task<List<Carrera>> Carreras(Persona actor, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        return almacen.Leer(d => d.Carreras(), ct);
    }

    public Task<long> CrearCarrera(Persona actor, NuevaCarrera nueva, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        var nombre = ReglasAcademicas.Texto(nueva.Nombre, "Carrera", 120);
        var codigo = Logica.FormatoSeccion.CodigoCarrera(nueva.Codigo);
        return almacen.Transaccion(async d =>
        {
            var id = await d.InsertarCarrera(nombre, codigo);
            await d.Auditar(actor.Id, "alta_carreras", id);
            return id;
        }, ct);
    }

    /// <summary>Al cambiar el código de la carrera se rearman los códigos de sus secciones.</summary>
    public Task<int> ActualizarCarrera(Persona actor, long carrera, NuevaCarrera cambio, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        var nombre = ReglasAcademicas.Texto(cambio.Nombre, "Carrera", 120);
        var codigo = Logica.FormatoSeccion.CodigoCarrera(cambio.Codigo);
        return almacen.Transaccion(async d =>
        {
            var formato = await d.BloquearFormatoSeccion();
            _ = await d.BloquearCarrera(carrera) ?? throw new ReglaException("La carrera no existe.", TipoErrorRegla.NoEncontrado);
            await d.ActualizarCarrera(carrera, nombre, codigo);
            var secciones = await d.BloquearSecciones(carrera);
            await Recodificar(d, formato, secciones.Select(s => s with { CodigoCarrera = codigo, Seccion = s.Seccion with { Carrera = nombre } }).ToList());
            await d.Auditar(actor.Id, "carrera_corregida", carrera);
            return 1;
        }, ct);
    }

    public Task<long> CrearSeccion(Persona actor, NuevaSeccion solicitud, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        var nombre = Logica.FormatoSeccion.Identificador(solicitud.Nombre);
        var semestre = Logica.FormatoSeccion.Semestre(solicitud.Semestre);
        return almacen.Transaccion(async d =>
        {
            var formato = await d.BloquearFormatoSeccion();
            var carrera = await d.BloquearCarrera(solicitud.CarreraId) ?? throw new ReglaException("Selecciona una carrera.");
            var codigo = Logica.FormatoSeccion.Generar(formato, carrera.Nombre, carrera.Codigo, semestre, nombre);
            await ExigirCodigoLibre(d, carrera.Id, codigo, null);
            var id = await d.InsertarSeccion(carrera.Id, semestre, nombre, codigo);
            await d.Auditar(actor.Id, "alta_seccion", $"{id}:{codigo}");
            return id;
        }, ct);
    }

    public Task<int> ActualizarSeccion(Persona actor, long seccion, CambioSeccion cambio, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        var nombre = Logica.FormatoSeccion.Identificador(cambio.Nombre);
        var semestre = Logica.FormatoSeccion.Semestre(cambio.Semestre);
        return almacen.Transaccion(async d =>
        {
            var formato = await d.BloquearFormatoSeccion();
            var actual = await d.Seccion(seccion) ?? throw new ReglaException("La sección no existe.", TipoErrorRegla.NoEncontrado);
            var carrera = await d.BloquearCarrera(actual.CarreraId) ?? throw new ReglaException("La carrera no existe.", TipoErrorRegla.NoEncontrado);
            var codigo = Logica.FormatoSeccion.Generar(formato, carrera.Nombre, carrera.Codigo, semestre, nombre);
            await ExigirCodigoLibre(d, carrera.Id, codigo, seccion);
            if (await d.ActualizarSeccion(seccion, semestre, nombre, codigo) == 0) throw new ReglaException("La sección no existe.", TipoErrorRegla.NoEncontrado);
            await d.Auditar(actor.Id, "seccion_corregida", $"{seccion}:{codigo}");
            return 1;
        }, ct);
    }

    // La base también lo impide; aquí se explica con palabras antes de llegar al error de clave repetida.
    private static async Task ExigirCodigoLibre(IEstructuraAcademicaDatos d, long carrera, string codigo, long? excepto)
    {
        var otras = await d.BloquearSecciones(carrera);
        if (otras.Any(o => o.Seccion.Id != excepto && string.Equals(o.Seccion.Codigo, codigo, StringComparison.OrdinalIgnoreCase)))
            throw new ReglaException($"Ya existe la sección {codigo} en esa carrera.", TipoErrorRegla.Conflicto);
    }

    public Task<int> RenombrarReferencia(Persona actor, string tipo, long id, CambioNombre cambio, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        var catalogo = LeerCatalogo(tipo, admitePeriodos: true);
        var nombre = ReglasAcademicas.Texto(cambio.Nombre, "Nombre", catalogo == Catalogo.Periodos ? 60 : 120);
        return almacen.Transaccion(async d =>
        {
            if (catalogo == Catalogo.Periodos)
            {
                var periodo = await d.BloquearPeriodo(id) ?? throw new ReglaException("El período no existe.", TipoErrorRegla.NoEncontrado);
                if (periodo.Cerrado) throw new ReglaException("Un período cerrado conserva su nombre: ya figura en los récords.", TipoErrorRegla.Conflicto);
            }
            if (await d.RenombrarReferencia(catalogo, id, nombre) == 0) throw new ReglaException("El registro no existe.", TipoErrorRegla.NoEncontrado);
            await d.Auditar(actor.Id, $"renombre_{tipo}", id);
            return 1;
        }, ct);
    }

    // Materias y pensum

    public Task<List<Materia>> Materias(Persona actor, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        return almacen.Leer(d => d.Materias(), ct);
    }

    public Task<List<MateriaPensum>> Pensum(Persona actor, long? carrera, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        return almacen.Leer(d => d.Pensum(carrera), ct);
    }

    private static async Task<NuevaMateria> ValidarMateria(IEstructuraAcademicaDatos d, NuevaMateria materia)
    {
        var limpia = Logica.Pensum.Normalizar(materia);
        if (limpia.NotaAprobatoria is { } propia)
            Calificacion.ValidarAprobatoria(propia, Calificacion.Leer(await d.Configuracion(Calificacion.Claves, Bloqueo.Compartido)));
        return limpia;
    }

    private static async Task ExigirCodigoMateriaLibre(IPensumDatos d, string codigo, long? excepto)
    {
        var existentes = await d.BloquearMateriasPorCodigo([codigo]);
        if (existentes.FirstOrDefault(m => m.Id != excepto) is { } otra)
            throw new ReglaException($"El código {codigo} ya es de «{otra.Nombre}». Si es la misma materia, agrégala al pensum en lugar de crearla.", TipoErrorRegla.Conflicto);
    }

    /// <summary>Crea la materia y, si se indica la carrera, la agrega a su pensum en la misma operación.</summary>
    public Task<long> CrearMateria(Persona actor, NuevaMateria nueva, long? carrera, int semestre, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        Logica.Pensum.Semestre(semestre);
        return almacen.Transaccion(async d =>
        {
            if (carrera is { } c) _ = await d.BloquearCarrera(c) ?? throw new ReglaException("La carrera no existe.", TipoErrorRegla.NoEncontrado);
            var materia = await ValidarMateria(d, nueva);
            await ExigirCodigoMateriaLibre(d, materia.Codigo, null);
            var id = await d.InsertarMateria(materia);
            if (carrera is { } enCarrera) await d.InsertarEnPensum(enCarrera, id, semestre);
            await d.Auditar(actor.Id, "alta_materia", $"{id}:{materia.Codigo}");
            return id;
        }, ct);
    }

    /// <summary>
    /// Corrige código, nombre, UC o nota aprobatoria propia. Afecta a todas las carreras que la tienen en su pensum;
    /// los períodos cerrados no cambian porque guardaron sus UC y su nota aprobatoria al cerrar.
    /// </summary>
    public Task<int> ActualizarMateria(Persona actor, long materia, NuevaMateria cambio, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        return almacen.Transaccion(async d =>
        {
            _ = await d.BloquearMateria(materia) ?? throw new ReglaException("La materia no existe.", TipoErrorRegla.NoEncontrado);
            var limpia = await ValidarMateria(d, cambio);
            await ExigirCodigoMateriaLibre(d, limpia.Codigo, materia);
            await d.ActualizarMateria(materia, limpia);
            await d.Auditar(actor.Id, "materia_corregida", $"{materia}:{limpia.Codigo}");
            return 1;
        }, ct);
    }

    /// <summary>Agrega al pensum una materia que ya existe (el ciclo básico de otra carrera) o le cambia el semestre.</summary>
    public Task<int> UbicarEnPensum(Persona actor, long carrera, EnPensum ubicacion, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        var semestre = Logica.Pensum.Semestre(ubicacion.Semestre);
        return almacen.Transaccion(async d =>
        {
            _ = await d.BloquearCarrera(carrera) ?? throw new ReglaException("La carrera no existe.", TipoErrorRegla.NoEncontrado);
            var pensum = await d.BloquearPensum(carrera);
            _ = await d.BloquearMateria(ubicacion.MateriaId) ?? throw new ReglaException("La materia no existe.", TipoErrorRegla.NoEncontrado);
            if (pensum.TryGetValue(ubicacion.MateriaId, out var actual))
            {
                if (actual != semestre) await d.CambiarSemestre(carrera, ubicacion.MateriaId, semestre);
            }
            else await d.InsertarEnPensum(carrera, ubicacion.MateriaId, semestre);
            await d.Auditar(actor.Id, "pensum_actualizado", $"{carrera}:{ubicacion.MateriaId}:{semestre}");
            return 1;
        }, ct);
    }

    public Task<int> QuitarDelPensum(Persona actor, long carrera, long materia, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        return almacen.Transaccion(async d =>
        {
            _ = await d.BloquearCarrera(carrera) ?? throw new ReglaException("La carrera no existe.", TipoErrorRegla.NoEncontrado);
            var pensum = await d.BloquearPensum(carrera);
            if (!pensum.ContainsKey(materia)) throw new ReglaException("Esa materia no está en el pensum de la carrera.", TipoErrorRegla.NoEncontrado);
            // La base también lo impide; aquí se explica con palabras.
            if (await d.AsignacionesEnPensum(carrera, materia) is var usos and > 0)
                throw new ReglaException($"La materia ya se asignó {usos} {(usos == 1 ? "vez" : "veces")} en esta carrera y forma parte de récords. No se puede quitar del pensum.", TipoErrorRegla.Conflicto);
            await d.QuitarDelPensum(carrera, materia);
            await d.Auditar(actor.Id, "pensum_quitada", $"{carrera}:{materia}");
            return 1;
        }, ct);
    }

    /// <summary>Lo que haría importar el Excel en el pensum de la carrera, sin guardar nada.</summary>
    public Task<VistaPensum> RevisarPensum(Persona actor, long carrera, IReadOnlyList<FilaCrudaPensum> crudas, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        var (filas, errores) = Logica.Pensum.Revisar(crudas);
        // Transacción y no solo lectura: compara con lo guardado bajo los mismos bloqueos que la importación, sin escribir nada.
        return almacen.Transaccion(async d =>
        {
            var (plan, conflictos) = await Planear(d, carrera, filas);
            return new VistaPensum(plan, [.. errores, .. conflictos]);
        }, ct);
    }

    private static async Task<(List<FilaPensum>, List<ErrorFila>)> Planear(IDatos d, long carrera, IReadOnlyList<(int Numero, FilaPensum Fila)> filas)
    {
        _ = await d.BloquearCarrera(carrera) ?? throw new ReglaException("La carrera no existe.", TipoErrorRegla.NoEncontrado);
        var pensum = await d.BloquearPensum(carrera);
        var existentes = (await d.BloquearMateriasPorCodigo(filas.Select(f => f.Fila.Codigo).ToList())).ToDictionary(m => m.Codigo, StringComparer.Ordinal);
        return Logica.Pensum.Planear(filas, existentes, pensum);
    }

    /// <summary>Importa el pensum de una carrera: todo o nada. Una materia que ya existe no se duplica, solo se agrega al pensum.</summary>
    public Task<ResultadoPensum> ImportarPensum(Persona actor, long carrera, ImportarPensum solicitud, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        var filas = ReglasAcademicas.Lista(solicitud?.Filas, 1, Logica.Pensum.MaximoFilas,
            $"Cada importación admite de 1 a {Logica.Pensum.MaximoFilas} materias, sin filas vacías.").Select((f, i) => (i + 1, Logica.Pensum.Normalizar(f))).ToList();
        if (filas.Select(f => f.Item2.Codigo).Distinct().Count() != filas.Count) throw new ReglaException("El listado repite uno o más códigos.");
        return almacen.Transaccion(async d =>
        {
            var (plan, conflictos) = await Planear(d, carrera, filas);
            if (conflictos.Count > 0)
                throw new ReglaException($"No se importó nada: {conflictos[0].Problema}" + (conflictos.Count > 1 ? $" Y {conflictos.Count - 1} problema(s) más." : ""), TipoErrorRegla.Conflicto);
            var pensum = await d.BloquearPensum(carrera);
            var existentes = (await d.BloquearMateriasPorCodigo(plan.Select(f => f.Codigo).ToList())).ToDictionary(m => m.Codigo, StringComparer.Ordinal);
            int nuevas = 0, agregadas = 0, cambiadas = 0, iguales = 0;
            foreach (var fila in plan)
            {
                if (!existentes.TryGetValue(fila.Codigo, out var materia))
                {
                    var id = await d.InsertarMateria(new NuevaMateria(fila.Codigo, fila.Nombre, fila.Uc));
                    await d.InsertarEnPensum(carrera, id, fila.Semestre);
                    nuevas++;
                }
                else if (!pensum.TryGetValue(materia.Id, out var semestre)) { await d.InsertarEnPensum(carrera, materia.Id, fila.Semestre); agregadas++; }
                else if (semestre != fila.Semestre) { await d.CambiarSemestre(carrera, materia.Id, fila.Semestre); cambiadas++; }
                else iguales++;
            }
            await d.Auditar(actor.Id, "pensum_importado", $"{carrera}:{nuevas}+{agregadas}~{cambiadas}");
            return new ResultadoPensum(nuevas, agregadas, cambiadas, iguales);
        }, ct);
    }

    // La materia debe estar en el pensum de la carrera de la sección. La base lo exige con una clave foránea.
    static async Task ValidarMateriaEnSeccion(IPensumDatos d, long materiaId, Seccion seccion)
    {
        if (!await d.EstaEnPensum(seccion.CarreraId, materiaId))
            throw new ReglaException($"Esa materia no está en el pensum de {seccion.Carrera}. Agrégala en Estructura › Pensum.");
    }

    // Reglas de calificación

    public Task<ReglasCalificacion> Reglas(Persona actor, CancellationToken ct)
    {
        Permisos.NoEstudiante(actor);
        return almacen.Leer(async d => Calificacion.Leer(await d.Configuracion(Calificacion.Claves, Bloqueo.Ninguno)), ct);
    }

    /// <summary>
    /// La escala solo cambia antes de la primera nota: después cambiaría el significado de lo ya cargado.
    /// La nota aprobatoria, el redondeo y el índice se pueden ajustar; los períodos cerrados conservan lo que regía.
    /// </summary>
    public Task<ReglasCalificacion> CambiarReglas(Persona actor, ReglasCalificacion cambio, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        var nuevas = Calificacion.Validar(cambio);
        return almacen.Transaccion(async d =>
        {
            var actuales = Calificacion.Leer(await d.Configuracion(Calificacion.Claves, Bloqueo.Exclusivo));
            if ((nuevas.EscalaMinima != actuales.EscalaMinima || nuevas.EscalaMaxima != actuales.EscalaMaxima) && await d.HayNotas())
                throw new ReglaException("La escala ya no se puede cambiar: hay notas cargadas con la escala actual. La nota aprobatoria, el redondeo y el índice sí se pueden ajustar.", TipoErrorRegla.Conflicto);
            if (await d.MateriasConAprobatoriaFuera(nuevas.EscalaMinima, nuevas.EscalaMaxima) is var fuera and > 0)
                throw new ReglaException($"{fuera} {(fuera == 1 ? "materia tiene" : "materias tienen")} una nota aprobatoria propia fuera de esa escala. Corrígelas primero en el pensum.", TipoErrorRegla.Conflicto);
            var anteriores = Calificacion.Escribir(actuales);
            foreach (var (clave, valor) in Calificacion.Escribir(nuevas))
                if (anteriores[clave] != valor) await d.GuardarConfiguracion(clave, valor);
            await d.Auditar(actor.Id, "reglas_calificacion", string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"escala {nuevas.EscalaMinima:0.##}-{nuevas.EscalaMaxima:0.##}, aprueba {nuevas.NotaAprobatoria:0.##}, {nuevas.Redondeo}, {nuevas.Indice}"));
            return nuevas;
        }, ct);
    }

    /// <summary>Reglas con las que se calcula una planilla: las congeladas si el período cerró, las vigentes si no.</summary>
    internal static async Task<ReglasCalificacion> ReglasDeOferta(IDatos d, long oferta, Bloqueo bloqueo)
    {
        var generales = Calificacion.Leer(await d.Configuracion(Calificacion.Claves, bloqueo));
        var propias = await d.ParametrosOferta(oferta);
        return generales with
        {
            NotaAprobatoria = propias.AprobatoriaCierre ?? propias.AprobatoriaMateria ?? generales.NotaAprobatoria,
            Redondeo = propias.RedondeoCierre ?? generales.Redondeo
        };
    }
    public Task<List<MateriaAsignada>> CargaDeSeccion(Persona actor, long periodo, long seccion, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        return almacen.Leer(d => d.CargaDeSeccion(periodo, seccion), ct);
    }

    public const int MaximoPorBloque = 60;

    /// <summary>Asigna varias materias de una sección en una sola operación; las que ya estaban asignadas se dejan como están.</summary>
    public Task<ResultadoCarga> AsignarEnBloque(Persona actor, CargaEnBloque carga, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        if (carga?.Materias is not { Count: > 0 }) throw new ReglaException("Elige el docente de al menos una materia.");
        ReglasAcademicas.Lista(carga.Materias, 1, MaximoPorBloque, $"Se pueden asignar de 1 a {MaximoPorBloque} materias a la vez.");
        if (carga.Materias.Select(m => m.MateriaId).Distinct().Count() != carga.Materias.Count) throw new ReglaException("Una materia aparece dos veces.");
        return almacen.Transaccion(async d =>
        {
            var periodo = await d.BloquearPeriodo(carga.PeriodoId);
            if (periodo is null || periodo.Cerrado) throw new ReglaException("Selecciona un período abierto.");
            var seccion = await d.Seccion(carga.SeccionId) ?? throw new ReglaException("La sección no existe.", TipoErrorRegla.NoEncontrado);
            int creadas = 0, existentes = 0;
            foreach (var item in carga.Materias)
            {
                await ValidarMateriaEnSeccion(d, item.MateriaId, seccion);
                if (await d.ExisteOferta(periodo.Id, seccion.Id, item.MateriaId)) { existentes++; continue; }
                if (!await d.EsDocenteActivo(item.DocenteId)) throw new ReglaException("Uno de los docentes elegidos no está activo.");
                var id = await d.InsertarOferta(new NuevaOferta(periodo.Id, seccion.Id, item.MateriaId, item.DocenteId), seccion.CarreraId);
                await d.Auditar(actor.Id, "asignacion_creada", id);
                creadas++;
            }
            return new ResultadoCarga(creadas, existentes, 0);
        }, ct);
    }

    /// <summary>
    /// Repite en un período abierto la carga de otro: misma sección, materia y docente.
    /// No toca lo que ya existe y omite a los docentes desactivados, que hay que asignar a mano.
    /// </summary>
    public Task<ResultadoCarga> CopiarCarga(Persona actor, long destino, CopiaDeCarga copia, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        if (copia.OrigenId == destino) throw new ReglaException("Elige un período distinto como origen.");
        return almacen.Transaccion(async d =>
        {
            var periodo = await d.BloquearPeriodo(destino);
            if (periodo is null || periodo.Cerrado) throw new ReglaException("La carga solo se copia a un período abierto.");
            if (await d.BloquearPeriodo(copia.OrigenId) is null) throw new ReglaException("El período de origen no existe.", TipoErrorRegla.NoEncontrado);
            int creadas = 0, existentes = 0, sinDocente = 0, fuera = 0;
            foreach (var anterior in await d.CargaDePeriodo(copia.OrigenId))
            {
                if (await d.ExisteOferta(destino, anterior.SeccionId, anterior.MateriaId)) { existentes++; continue; }
                if (!await d.EsDocenteActivo(anterior.DocenteId)) { sinDocente++; continue; }
                var seccion = await d.Seccion(anterior.SeccionId);
                // Si desde entonces la materia salió del pensum, no se copia: se avisa.
                if (seccion is null || !await d.EstaEnPensum(seccion.CarreraId, anterior.MateriaId)) { fuera++; continue; }
                await d.InsertarOferta(anterior with { PeriodoId = destino }, seccion.CarreraId);
                creadas++;
            }
            await d.Auditar(actor.Id, "carga_copiada", $"{copia.OrigenId}:{destino}:{creadas}");
            return new ResultadoCarga(creadas, existentes, sinDocente, fuera);
        }, ct);
    }

    public Task<int> RetirarOferta(Persona actor, long ofertaId, int revision, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        return almacen.Transaccion(async d =>
        {
            await PrepararEdicion(d, ofertaId, actor, revision);
            if (await d.TieneMovimientos(ofertaId))
                throw new ReglaException("Esta materia ya tiene evaluaciones, estudiantes o publicaciones y no se puede retirar. Si el problema es el docente, usa «Cambiar docente».", TipoErrorRegla.Conflicto);
            await d.EliminarOferta(ofertaId);
            await d.Auditar(actor.Id, "asignacion_retirada", ofertaId);
            return 1;
        }, ct);
    }

    public Task<Pagina<Oferta>> Ofertas(Persona actor, ConsultaPagina consulta, CancellationToken ct)
    {
        Permisos.NoEstudiante(actor);
        long? docente = actor.Rol == Roles.Administrador ? null : actor.Id;
        return almacen.Leer(d => d.Ofertas(docente, consulta), ct);
    }

    public Task<long> CrearOferta(Persona actor, NuevaOferta solicitud, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        return almacen.Transaccion(async d =>
        {
            var periodo = await d.BloquearPeriodo(solicitud.PeriodoId);
            if (periodo is null || periodo.Cerrado) throw new ReglaException("Selecciona un período abierto.");
            if (!await d.EsDocenteActivo(solicitud.DocenteId)) throw new ReglaException("Selecciona una cuenta docente activa.");
            var seccion = await d.Seccion(solicitud.SeccionId) ?? throw new ReglaException("La sección no existe.", TipoErrorRegla.NoEncontrado);
            await ValidarMateriaEnSeccion(d, solicitud.MateriaId, seccion);
            var id = await d.InsertarOferta(solicitud, seccion.CarreraId);
            await d.Auditar(actor.Id, "asignacion_creada", id);
            return id;
        }, ct);
    }

    public Task<Planilla> Planilla(Persona actor, long ofertaId, CancellationToken ct) => almacen.Leer(async d =>
    {
        var oferta = await d.Oferta(ofertaId) ?? throw new ReglaException("La asignación no existe.", TipoErrorRegla.NoEncontrado);
        Permisos.PuedeVerOferta(actor, oferta);
        return new Planilla(oferta, await d.Evaluaciones(ofertaId), await d.Alumnos(ofertaId), await d.Notas(ofertaId),
            await ReglasDeOferta(d, ofertaId, Bloqueo.Ninguno));
    }, ct);

    public Task<int> Reasignar(Persona actor, long ofertaId, CambioDocente cambio, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        return almacen.Transaccion(async d =>
        {
            await PrepararEdicion(d, ofertaId, actor, cambio.Revision);
            if (!await d.EsDocenteActivo(cambio.DocenteId)) throw new ReglaException("Selecciona una cuenta docente activa.");
            await d.AsignarDocente(ofertaId, cambio.DocenteId);
            await d.Auditar(actor.Id, "docente_reasignado", $"{ofertaId}:{cambio.DocenteId}");
            return cambio.Revision + 1;
        }, ct);
    }

    /// <summary>Un período se cierra solo cuando cada planilla está completa y publicada en su última versión.</summary>
    public static List<string> PendientesDeCierre(EstadoPlanilla estado)
    {
        var pendientes = new List<string>();
        if (estado.Oferta.Matriculados == 0)   pendientes.Add("Sin matrícula confirmada.");
        if (estado.Oferta.Pendientes > 0)      pendientes.Add("Hay inscripciones por revisar.");
        if (estado.PesoTotal != 100m)          pendientes.Add($"La ponderación suma {estado.PesoTotal:0.##} %, debe llegar a 100 %.");
        if (estado.CalificacionesFaltantes > 0) pendientes.Add($"Faltan {estado.CalificacionesFaltantes} calificaciones.");
        if (estado.Oferta.RevisionPublicada != estado.Oferta.Revision)
            pendientes.Add("El docente debe publicar la última versión.");
        return pendientes;
    }

    private static async Task<ResumenCierre> ComprobarCierre(IDatos d, long periodoId)
    {
        var periodo = await d.BloquearPeriodo(periodoId) ?? throw new ReglaException("El período no existe.", TipoErrorRegla.NoEncontrado);
        var planillas = await d.EstadoPlanillasDePeriodo(periodoId);
        return new ResumenCierre(periodo, planillas.Select(p =>
            new ComprobacionCierre(p.Oferta.Id, p.Oferta.Materia, p.Oferta.Seccion, PendientesDeCierre(p))).ToList());
    }

    public Task<ResumenCierre> RevisarCierre(Persona actor, long periodoId, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        return almacen.Transaccion(d => ComprobarCierre(d, periodoId), ct);
    }

    public Task<ResumenCierre> CerrarPeriodo(Persona actor, long periodoId, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        return almacen.Transaccion(async d =>
        {
            var comprobacion = await ComprobarCierre(d, periodoId);
            if (!comprobacion.PuedeCerrar) throw new ReglaException("No se puede cerrar: revisa matrícula, ponderación, notas y publicaciones.", TipoErrorRegla.Conflicto);
            // Lo que rige hoy queda guardado en el período: un cambio posterior de reglas no altera el récord cerrado.
            var reglas = Calificacion.Leer(await d.Configuracion(Calificacion.Claves, Bloqueo.Compartido));
            await d.CongelarReglas(periodoId, reglas.NotaAprobatoria);
            await d.MarcarPeriodoCerrado(periodoId, reglas.Redondeo);
            await d.Auditar(actor.Id, "periodo_cerrado", periodoId);
            return comprobacion with { Periodo = comprobacion.Periodo with { Cerrado = true } };
        }, ct);
    }
}
