using Entidades;

namespace Logica;

public sealed class ServicioIdentidad(IAlmacen almacen, IEnviadorCorreo correo)
{
    private static readonly string ClaveFicticia = Seguridad.DerivarClave(Seguridad.NuevoToken());
    private static readonly TimeSpan DuracionSesion = TimeSpan.FromHours(8);
    private static readonly TimeSpan DuracionCodigo = TimeSpan.FromMinutes(30);

    /// <summary>Solo para desarrollo y pruebas: crea la primera cuenta desde variables de entorno.</summary>
    public async Task PrepararAdministrador(string? cedula, string? clave, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cedula) || string.IsNullOrWhiteSpace(clave)) return;
        if (await almacen.Leer(d => d.ContarAdministradores(), ct) > 0) return;
        var normalizada = ReglasAcademicas.Cedula(cedula);
        var hash = Seguridad.DerivarClave(clave);
        await almacen.Transaccion(async d =>
        {
            var id = await d.InsertarUsuario(normalizada, "Administración", "inicial", Roles.Administrador, hash);
            await d.Auditar(id, "administrador_inicial", id);
            return id;
        }, ct);
    }

    public Task<EstadoConfiguracion> EstadoConfiguracion(CancellationToken ct) => almacen.Leer(async d =>
        new EstadoConfiguracion(await d.ContarAdministradores() == 0 && await d.HayCodigoConfiguracion(), correo.Disponible), ct);

    public static string NormalizarCodigo(string? codigo) =>
        new((codigo ?? "").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    /// <summary>
    /// Crea la primera cuenta de administración con el código que dejó la instalación.
    /// Consumir el código y crear la cuenta van juntos: si dos personas lo intentan a la vez, solo una lo logra.
    /// </summary>
    public Task<long> ConfigurarInicial(ConfiguracionInicial solicitud, CancellationToken ct)
    {
        var codigo = NormalizarCodigo(solicitud.Codigo);
        if (codigo.Length != 16) throw new ReglaException("El código de configuración tiene 16 letras y números.");
        var cedula = ReglasAcademicas.Cedula(solicitud.Cedula);
        var nombres = ReglasAcademicas.Texto(solicitud.Nombres, "Nombres", 100);
        var apellidos = ReglasAcademicas.Texto(solicitud.Apellidos, "Apellidos", 100);
        var hash = Seguridad.DerivarClave(solicitud.Clave);
        return almacen.Transaccion(async d =>
        {
            if (await d.ConsumirCodigoConfiguracion(Seguridad.Huella(codigo)) != 1)
                throw new ReglaException("El código de configuración no es válido o ya se usó.");
            if (await d.ContarAdministradores() > 0) throw new ReglaException("Mérito ya tiene una cuenta de administración.", TipoErrorRegla.Conflicto);
            var id = await d.InsertarUsuario(cedula, nombres, apellidos, Roles.Administrador, hash);
            await d.Auditar(id, "configuracion_inicial", id);
            return id;
        }, ct);
    }

    public async Task<Sesion> Iniciar(Acceso acceso, CancellationToken ct)
    {
        var cedula = ReglasAcademicas.Cedula(acceso.Cedula);
        var cuenta = await almacen.Leer(d => d.CuentaPorCedula(cedula), ct);
        // Se verifica siempre una clave para que el tiempo de respuesta no revele si la cuenta existe.
        var valida = Seguridad.VerificarClave(acceso.Clave ?? "", cuenta?.Hash ?? ClaveFicticia);
        if (!valida || cuenta is null || !cuenta.Persona.Activo || cuenta.Hash is null)
            throw new ReglaException("Cédula o contraseña incorrectas, o cuenta sin activar.", TipoErrorRegla.NoAutenticado);
        var token = Seguridad.NuevoToken();
        var vence = DateTime.UtcNow.Add(DuracionSesion);
        await almacen.Transaccion(async d =>
        {
            // Serializar con los cambios de contraseña impide crear una sesión con una clave ya revocada.
            if (await d.BloquearClave(cuenta.Persona.Id, soloActiva: true) != cuenta.Hash)
                throw new ReglaException("El acceso cambió. Vuelve a iniciar sesión.", TipoErrorRegla.NoAutenticado);
            await d.EliminarSesionesVencidas();
            await d.InsertarSesion(Seguridad.Huella(token), cuenta.Persona.Id, vence);
            await d.Auditar(cuenta.Persona.Id, "inicio_sesion", cuenta.Persona.Id);
            return true;
        }, ct);
        return new Sesion(token, cuenta.Persona, vence);
    }

    public Task<Persona?> Resolver(string token, CancellationToken ct) => token.Length != 64
        ? Task.FromResult<Persona?>(null)
        : almacen.Leer(d => d.PersonaPorSesion(Seguridad.Huella(token)), ct);

    public Task Salir(string token, CancellationToken ct) =>
        almacen.Transaccion(async d => { await d.EliminarSesion(Seguridad.Huella(token)); return true; }, ct);

    public async Task Cambiar(Persona actor, CambiarClave cambio, CancellationToken ct)
    {
        var hash = Seguridad.DerivarClave(cambio.Nueva);
        await almacen.Transaccion(async d =>
        {
            var actual = await d.BloquearClave(actor.Id, soloActiva: false);
            if (!Seguridad.VerificarClave(cambio.Actual, actual)) throw new ReglaException("La contraseña actual no coincide.");
            await d.ActualizarClave(actor.Id, hash);
            await d.EliminarSesionesDe(actor.Id);
            await d.EliminarCodigosDe(actor.Id);
            await d.Auditar(actor.Id, "cambio_clave", actor.Id);
            return true;
        }, ct);
    }

    public Task<Pagina<Persona>> Personas(Persona actor, ConsultaPagina consulta, CancellationToken ct, string? rol = null)
    {
        Permisos.SoloAdministrador(actor);
        var cargo = rol is null ? null : Permisos.RolValido(rol);
        return almacen.Leer(d => d.Personas(consulta, cargo), ct);
    }

    public Task<Pagina<FilaDocente>> NominaDocente(Persona actor, ConsultaPagina consulta, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        return almacen.Leer(d => d.NominaDocente(consulta), ct);
    }

    public Task<Pagina<FilaEstudiante>> Estudiantes(Persona actor, ConsultaPagina consulta, long? periodo, long? seccion, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        return almacen.Leer(d => d.Estudiantes(consulta, periodo, seccion), ct);
    }

    // Nómina docente desde Excel: crea las cuentas nuevas y completa el correo de las que todavía no se registraron.

    public const int MaximoNomina = 2000;

    private static FilaNomina NormalizarNomina(FilaNomina fila) => new(ReglasAcademicas.Cedula(fila.Cedula),
        ReglasAcademicas.Texto(fila.Nombres, "Nombres", 100), ReglasAcademicas.Texto(fila.Apellidos, "Apellidos", 100), ReglasAcademicas.Correo(fila.Correo));

    /// <summary>Revisa el archivo sin guardar nada: errores por fila y qué pasará con cada docente.</summary>
    public Task<VistaNomina> RevisarNomina(Persona actor, IReadOnlyList<(int Numero, FilaNomina Fila, bool TieneFormula)> crudas, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        if (crudas.Count is < 1 or > MaximoNomina) throw new ReglaException($"La hoja debe contener entre 1 y {MaximoNomina} docentes.");
        var errores = new List<ErrorFila>();
        var filas = new List<(int Numero, FilaNomina Fila)>();
        var (cedulas, correos) = (new HashSet<string>(), new HashSet<string>());
        foreach (var (numero, cruda, formula) in crudas)
        {
            try
            {
                if (formula) throw new ReglaException("No se admiten fórmulas en la nómina.");
                var fila = NormalizarNomina(cruda);
                if (!cedulas.Add(fila.Cedula)) throw new ReglaException("Cédula repetida en el archivo.");
                if (fila.Correo is { } correo && !correos.Add(correo)) throw new ReglaException($"El correo {correo} está repetido en el archivo.");
                filas.Add((numero, fila));
            }
            catch (ReglaException error) { errores.Add(new ErrorFila(numero, error.Message)); }
        }
        return almacen.Leer(async d =>
        {
            var revisadas = new List<FilaNomina>();
            foreach (var (numero, fila) in filas)
            {
                try { revisadas.Add(fila with { Accion = await PlanNomina(d, fila, aplicar: false) }); }
                catch (ReglaException error) { errores.Add(new ErrorFila(numero, error.Message)); }
            }
            return new VistaNomina(revisadas, [.. errores.OrderBy(e => e.Fila)]);
        }, ct);
    }

    public Task<ResultadoNomina> ImportarNomina(Persona actor, ImportarNomina solicitud, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        var filas = ReglasAcademicas.Lista(solicitud?.Filas, 1, MaximoNomina, $"La nómina admite de 1 a {MaximoNomina} docentes, sin filas vacías.")
            .Select(NormalizarNomina).OrderBy(f => f.Cedula, StringComparer.Ordinal).ToList();
        if (filas.Select(f => f.Cedula).Distinct().Count() != filas.Count) throw new ReglaException("La nómina repite una o más cédulas.");
        if (filas.Where(f => f.Correo is not null).GroupBy(f => f.Correo).Any(g => g.Count() > 1)) throw new ReglaException("La nómina repite uno o más correos.");
        return almacen.Transaccion(async d =>
        {
            var (nuevos, actualizados, iguales) = (0, 0, 0);
            foreach (var fila in filas)
            {
                var accion = await PlanNomina(d, fila, aplicar: true);
                if (accion == "Docente nuevo") nuevos++; else if (accion == "Ya está; se completa el correo") actualizados++; else iguales++;
            }
            await d.Auditar(actor.Id, "nomina_importada", $"{nuevos}+{actualizados}");
            return new ResultadoNomina(nuevos, actualizados, iguales);
        }, ct);
    }

    // Lo que pasa con una fila de la nómina; al aplicar, además se escribe.
    private static async Task<string> PlanNomina(IDatos d, FilaNomina fila, bool aplicar)
    {
        var usuario = aplicar ? await d.BloquearUsuarioPorCedula(fila.Cedula) : await d.BuscarUsuarioPorCedula(fila.Cedula);
        if (usuario is not null && (usuario.Rol != Roles.Docente || !ReglasAcademicas.MismoTexto(usuario.Nombres, fila.Nombres)
            || !ReglasAcademicas.MismoTexto(usuario.Apellidos, fila.Apellidos)))
            throw new ReglaException($"La cédula {Cedulas.Mostrar(fila.Cedula)} ya existe con otro nombre o cargo.", TipoErrorRegla.Conflicto);
        // El correo de una cuenta ya registrada no se reemplaza desde un archivo.
        var correoNuevo = fila.Correo is not null && usuario is not { Registrada: true } && usuario?.Correo != fila.Correo;
        if (correoNuevo) await ExigirCorreoLibre(d, fila.Correo!, usuario?.Id);
        if (aplicar)
        {
            var id = usuario?.Id ?? await d.InsertarUsuario(fila.Cedula, fila.Nombres, fila.Apellidos, Roles.Docente, null);
            if (correoNuevo) await d.ActualizarCorreo(id, fila.Correo);
        }
        return usuario is null ? "Docente nuevo" : correoNuevo ? "Ya está; se completa el correo" : "Ya está, sin cambios";
    }

    public Task<List<Persona>> DocentesActivos(Persona actor, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        return almacen.Leer(d => d.DocentesActivos(), ct);
    }

    public Task<long> Crear(Persona actor, NuevaPersona nueva, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        var rol = Permisos.RolValido(nueva.Rol);
        var cedula = ReglasAcademicas.Cedula(nueva.Cedula);
        var nombres = ReglasAcademicas.Texto(nueva.Nombres, "Nombres", 100);
        var apellidos = ReglasAcademicas.Texto(nueva.Apellidos, "Apellidos", 100);
        var direccion = ReglasAcademicas.Correo(nueva.Correo);
        return almacen.Transaccion(async d =>
        {
            if (direccion is not null) await ExigirCorreoLibre(d, direccion, null);
            var id = await d.InsertarUsuario(cedula, nombres, apellidos, rol, null);
            if (direccion is not null) await d.ActualizarCorreo(id, direccion);
            await d.Auditar(actor.Id, "cuenta_creada", id);
            return id;
        }, ct);
    }

    // La base también lo impide; aquí se explica con palabras.
    internal static async Task ExigirCorreoLibre(IIdentidadDatos d, string direccion, long? excepto)
    {
        if (await d.UsuarioConCorreo(direccion) is var otro and not 0 && otro != excepto)
            throw new ReglaException($"El correo {direccion} ya es de otra cuenta.", TipoErrorRegla.Conflicto);
    }

    public Task<int> CambiarCorreo(Persona actor, long usuario, CambioCorreo cambio, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        var direccion = ReglasAcademicas.Correo(cambio?.Correo);
        return almacen.Transaccion(async d =>
        {
            if (!await d.BloquearUsuario(usuario, soloActivo: false)) throw new ReglaException("La cuenta no existe.", TipoErrorRegla.NoEncontrado);
            if (direccion is not null) await ExigirCorreoLibre(d, direccion, usuario);
            await d.ActualizarCorreo(usuario, direccion);
            // Un código enviado al correo anterior deja de servir.
            await d.EliminarVerificacion(usuario);
            await d.Auditar(actor.Id, "correo_corregido", usuario);
            return 1;
        }, ct);
    }

    // Registro y recuperación por correo

    private static readonly TimeSpan DuracionCodigoCorreo = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan EsperaEntreCorreos = TimeSpan.FromSeconds(60);
    public const int IntentosCodigoCorreo = 5;

    private static string HuellaCorreo(long usuario, string codigo) => Seguridad.Huella($"{usuario}:{codigo}");

    public static string Enmascarar(string direccion)
    {
        var arroba = direccion.IndexOf('@');
        var local = direccion[..arroba];
        return (local.Length <= 2 ? local[..1] : $"{local[0]}{new string('•', Math.Min(local.Length - 2, 6))}{local[^1]}") + direccion[arroba..];
    }

    /// <summary>
    /// Envía un código de seis cifras al correo de la cuenta: sirve para registrarse la primera vez y para recuperar
    /// la contraseña. Se envía dentro de la transacción: si el correo no sale, el código no queda guardado.
    /// </summary>
    public Task<SolicitudEnviada> EnviarCodigoCorreo(SolicitarCodigo solicitud, CancellationToken ct)
    {
        if (!correo.Disponible) throw new ReglaException("El registro por correo todavía no está configurado en este servidor. Pide tu código en administración.");
        var cedula = ReglasAcademicas.Cedula(solicitud?.Cedula);
        var codigo = Seguridad.NuevoCodigoCorreo();
        return almacen.Transaccion(async d =>
        {
            var cuenta = await d.BloquearCuentaParaCorreo(cedula);
            if (cuenta is not { Activo: true, Correo: { } destino })
                throw new ReglaException("No hay un correo registrado para esa cédula. Pide ayuda en administración.", TipoErrorRegla.NoEncontrado);
            if (await d.BloquearVerificacion(cuenta.Id) is { } previa && previa.EnviadoEn > DateTime.UtcNow - EsperaEntreCorreos)
                throw new ReglaException("Ya te enviamos un código hace un momento. Revisa también el correo no deseado; en un minuto puedes pedir otro.", TipoErrorRegla.LimiteIntentos);
            var vence = DateTime.UtcNow.Add(DuracionCodigoCorreo);
            await d.GuardarVerificacion(cuenta.Id, HuellaCorreo(cuenta.Id, codigo), vence);
            await d.Auditar(cuenta.Id, "codigo_correo_enviado", cuenta.Id);
            await correo.EnviarCodigoAcceso(destino, cuenta.Nombres, codigo, cuenta.Registrada, ct);
            return new SolicitudEnviada(Enmascarar(destino), vence, cuenta.Registrada);
        }, ct);
    }

    /// <summary>
    /// Con el código del correo la persona elige su contraseña. Cada código admite pocos intentos; los fallidos se
    /// cuentan aunque la operación termine en error, por eso la transacción devuelve el error en lugar de lanzarlo.
    /// </summary>
    public async Task ConfirmarCodigoCorreo(ConfirmarCorreo confirmacion, CancellationToken ct)
    {
        var cedula = ReglasAcademicas.Cedula(confirmacion?.Cedula);
        var codigo = new string((confirmacion!.Codigo ?? "").Where(char.IsAsciiDigit).ToArray());
        var hash = Seguridad.DerivarClave(confirmacion.Clave);
        var error = await almacen.Transaccion<string?>(async d =>
        {
            var cuenta = await d.BloquearCuentaParaCorreo(cedula);
            var verificacion = cuenta is null ? null : await d.BloquearVerificacion(cuenta.Id);
            if (cuenta is not { Activo: true } || verificacion is null || verificacion.VenceEn <= DateTime.UtcNow)
                return "El código no es válido o venció. Pide uno nuevo.";
            if (codigo.Length != 6 || HuellaCorreo(cuenta.Id, codigo) != verificacion.Huella)
            {
                var quedan = IntentosCodigoCorreo - verificacion.Intentos - 1;
                if (quedan <= 0) { await d.EliminarVerificacion(cuenta.Id); return "Demasiados intentos con ese código. Pide uno nuevo."; }
                await d.SumarIntento(cuenta.Id);
                return quedan == 1 ? "El código no coincide. Te queda un intento." : $"El código no coincide. Te quedan {quedan} intentos.";
            }
            await d.ActualizarClave(cuenta.Id, hash);
            await d.EliminarVerificacion(cuenta.Id);
            await d.EliminarSesionesDe(cuenta.Id);
            await d.EliminarCodigosDe(cuenta.Id);
            await d.Auditar(cuenta.Id, cuenta.Registrada ? "clave_recuperada_por_correo" : "registro_por_correo", cuenta.Id);
            return null;
        }, ct);
        if (error is not null) throw new ReglaException(error);
    }

    public async Task ProbarCorreo(Persona actor, PruebaCorreo prueba, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        if (!correo.Disponible) throw new ReglaException("Este servidor todavía no tiene cuenta de correo. Complétala en correo.json, junto a servidor.json.");
        var direccion = ReglasAcademicas.Correo(prueba?.Para) ?? throw new ReglaException("Escribe a qué correo enviar la prueba.");
        await correo.EnviarPrueba(direccion, actor.NombreCompleto, ct);
        await almacen.Transaccion(async d => { await d.Auditar(actor.Id, "prueba_correo", direccion); return true; }, ct);
    }

    public Task<CodigoAcceso> EmitirCodigo(Persona actor, long usuario, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        var codigo = Seguridad.NuevoCodigoAcceso();
        var vence = DateTime.UtcNow.Add(DuracionCodigo);
        return almacen.Transaccion(async d =>
        {
            if (!await d.BloquearUsuario(usuario, soloActivo: true)) throw new ReglaException("No se encuentra esa cuenta activa.", TipoErrorRegla.NoEncontrado);
            await d.EliminarCodigosDe(usuario);
            await d.InsertarCodigo(Seguridad.Huella(NormalizarCodigo(codigo)), usuario, vence);
            await d.Auditar(actor.Id, "codigo_acceso_emitido", usuario);
            return new CodigoAcceso(codigo, vence);
        }, ct);
    }

    public const int MaximoDiasCodigoSeccion = 30;

    /// <summary>
    /// Códigos para los estudiantes de una asignación que todavía no activaron su cuenta, para entregarlos en clase.
    /// Los emite el docente de la asignación o administración. Solo cuentas que nunca se activaron: una cuenta con
    /// contraseña no se puede tomar por esta vía; recuperar un acceso sigue siendo trámite de administración.
    /// Emitir de nuevo reemplaza los códigos anteriores.
    /// </summary>
    public Task<CodigosDeSeccion> CodigosDeSeccion(Persona actor, long ofertaId, CodigosSeccion solicitud, CancellationToken ct)
    {
        Permisos.NoEstudiante(actor);
        var dias = solicitud?.Dias ?? 0;
        if (dias is < 1 or > MaximoDiasCodigoSeccion) throw new ReglaException($"Los códigos pueden durar de 1 a {MaximoDiasCodigoSeccion} días.");
        var vence = DateTime.UtcNow.AddDays(dias);
        return almacen.Transaccion(async d =>
        {
            var oferta = await ServicioAcademico.PrepararEdicion(d, ofertaId, actor, null, editar: false);
            var pendientes = await d.BloquearEstudiantesSinAcceso(ofertaId);
            var codigos = new List<CodigoEstudiante>();
            foreach (var estudiante in pendientes)
            {
                var codigo = Seguridad.NuevoCodigoAcceso();
                await d.EliminarCodigosDe(estudiante.Id);
                await d.InsertarCodigo(Seguridad.Huella(NormalizarCodigo(codigo)), estudiante.Id, vence);
                codigos.Add(new CodigoEstudiante(estudiante.Cedula, estudiante.Nombres, estudiante.Apellidos, codigo, vence));
            }
            await d.Auditar(actor.Id, "codigos_seccion", $"{ofertaId}:{codigos.Count}");
            return new CodigosDeSeccion(oferta.Descripcion, codigos, oferta.Matriculados - codigos.Count);
        }, ct);
    }

    public async Task Activar(ActivarCuenta solicitud, CancellationToken ct)
    {
        var cedula = ReglasAcademicas.Cedula(solicitud.Cedula);
        // Se acepta como se escribió en la hoja: con o sin guiones, en mayúsculas o minúsculas.
        var codigo = NormalizarCodigo(solicitud.Codigo);
        if (codigo.Length != Seguridad.LargoCodigo) throw new ReglaException("El código no es válido o ha vencido.");
        var hash = Seguridad.DerivarClave(solicitud.Clave);
        await almacen.Transaccion(async d =>
        {
            var usuario = await d.BloquearUsuarioActivoPorCedula(cedula);
            if (usuario == 0 || !await d.CodigoVigente(usuario, Seguridad.Huella(codigo)))
                throw new ReglaException("El código no es válido o ha vencido.");
            await d.ActualizarClave(usuario, hash);
            await d.EliminarCodigosDe(usuario);
            await d.EliminarSesionesDe(usuario);
            await d.Auditar(usuario, "acceso_establecido", usuario);
            return true;
        }, ct);
    }

    public Task<bool> CambiarEstado(Persona actor, long usuario, bool activo, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        if (usuario == actor.Id) throw new ReglaException("No puedes desactivar tu propia cuenta.");
        return almacen.Transaccion(async d =>
        {
            var administradores = await d.BloquearAdministradoresActivos();
            if (!activo && administradores.Count == 1 && administradores.Contains(usuario))
                throw new ReglaException("Debe quedar al menos una cuenta administradora activa.");
            if (!await d.BloquearUsuario(usuario, soloActivo: false)) throw new ReglaException("La cuenta no existe.", TipoErrorRegla.NoEncontrado);
            await d.ActualizarActivo(usuario, activo);
            if (!activo)
            {
                await d.EliminarSesionesDe(usuario);
                await d.EliminarCodigosDe(usuario);
            }
            await d.Auditar(actor.Id, activo ? "cuenta_activada" : "cuenta_desactivada", usuario);
            return activo;
        }, ct);
    }

    public Task<Pagina<EventoAuditoria>> Auditoria(Persona actor, ConsultaPagina consulta, CancellationToken ct)
    {
        Permisos.SoloAdministrador(actor);
        return almacen.Leer(async d =>
        {
            var pagina = await d.Auditoria(consulta);
            return new Pagina<EventoAuditoria>(pagina.Elementos.Select(Historial.Evento).ToList(), pagina.Total, pagina.Desde, pagina.Cantidad);
        }, ct);
    }
}
