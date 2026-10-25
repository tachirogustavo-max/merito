namespace Entidades;

public static class Condiciones
{
    public const string Aprobado = "aprobado";
    public const string Aplazado = "aplazado";
}

public static class Roles
{
    public const string Administrador = "administrador";
    public const string Docente = "docente";
    public const string Estudiante = "estudiante";
}

// La cédula se guarda solo con números y se muestra siempre como V-12345678.
public static class Cedulas
{
    public static string Mostrar(string? cedula) =>
        string.IsNullOrEmpty(cedula) ? "" : cedula.StartsWith("V-", StringComparison.Ordinal) ? cedula : "V-" + cedula;
}

// Registrada: ya eligió su contraseña. Sin registrar, puede hacerlo con el código que llega a su correo.
public sealed record Persona(long Id, string Cedula, string Nombres, string Apellidos, string Rol, bool Activo, string? Correo = null, bool Registrada = true)
{
    public string EstadoCuenta => !Activo ? "inactiva" : Registrada ? "activa" : "sin registrar";
    public string NombreCompleto => $"{Nombres} {Apellidos}";
    // Para listas donde puede haber homónimos.
    public string ConCedula => $"{Nombres} {Apellidos} ({Cedulas.Mostrar(Cedula)})";
}
public sealed record FilaDocente(Persona Persona, int Materias)
{
    public string Cedula => Persona.Cedula;
    public string NombreCompleto => Persona.NombreCompleto;
    public string? Correo => Persona.Correo;
    public string EstadoCuenta => Persona.EstadoCuenta;
}
public sealed record FilaEstudiante(Persona Persona, string Secciones, int Materias)
{
    public string Cedula => Persona.Cedula;
    public string NombreCompleto => Persona.NombreCompleto;
    public string? Correo => Persona.Correo;
    public string EstadoCuenta => Persona.EstadoCuenta;
    public string SeccionesTexto => Secciones.Length == 0 ? "Sin inscripción" : Secciones;
}
// Importar la nómina docente: Accion dice qué pasará con cada fila al revisarla.
public sealed record FilaNomina(string Cedula, string Nombres, string Apellidos, string? Correo = null, string Accion = "")
{
    public string CedulaTexto => Cedulas.Mostrar(Cedula);
    public string NombreCompleto => $"{Nombres} {Apellidos}";
}
public sealed record VistaNomina(List<FilaNomina> Filas, List<ErrorFila> Errores);
public sealed record ImportarNomina(List<FilaNomina> Filas);
public sealed record ResultadoNomina(int Nuevos, int Actualizados, int SinCambios);
public sealed record Acceso(string Cedula, string Clave);
public sealed record Sesion(string Token, Persona Persona, DateTime VenceEn);
public sealed record CambiarClave(string Actual, string Nueva);
public sealed record ActivarCuenta(string Cedula, string Codigo, string Clave);
public sealed record EstadoConfiguracion(bool Pendiente, bool CorreoDisponible = false);
public sealed record ConfiguracionInicial(string Codigo, string Cedula, string Nombres, string Apellidos, string Clave);
public sealed record NuevaPersona(string Cedula, string Nombres, string Apellidos, string Rol, string? Correo = null);
// Registro y recuperación por correo: se pide un código y se confirma con él la contraseña nueva.
public sealed record SolicitarCodigo(string Cedula);
public sealed record SolicitudEnviada(string Correo, DateTime VenceEn, bool Registrada);
public sealed record ConfirmarCorreo(string Cedula, string Codigo, string Clave);
public sealed record CambioCorreo(string? Correo);
public sealed record PruebaCorreo(string Para);
public sealed record CodigoAcceso(string Codigo, DateTime VenceEn);
public sealed record CodigosSeccion(int Dias);
public sealed record CodigoEstudiante(string Cedula, string Nombres, string Apellidos, string Codigo, DateTime VenceEn)
{
    public string NombreCompleto => $"{Nombres} {Apellidos}";
}
// ConAcceso: los confirmados que ya tenían cuenta activa y no reciben código.
public sealed record CodigosDeSeccion(string Asignacion, List<CodigoEstudiante> Codigos, int ConAcceso);
public sealed record EstadoCuenta(bool Activo);
public sealed record CambioDocente(long DocenteId, int Revision);
// Detalle: la acción en palabras, con los nombres de lo que se cambió.
public sealed record EventoAuditoria(long Id, string Actor, string Accion, string Referencia, DateTime OcurridoEn, string Detalle = "");
public sealed record Periodo(long Id, string Nombre, bool Cerrado);
public sealed record Referencia(long Id, string Nombre);
// Nombre es el identificador propio de la sección (A, D1…); Codigo es el que se muestra, armado con el formato vigente.
public sealed record Seccion(long Id, long CarreraId, string Nombre, string Carrera, int Semestre = 0, string Codigo = "")
{
    public string SemestreTexto => Semestre == 0 ? "" : Semestre.ToString();
}
public sealed record NuevaSeccion(long CarreraId, string Nombre, int Semestre = 0);
public sealed record CambioSeccion(string Nombre, int Semestre);
public sealed record Carrera(long Id, string Nombre, string? Codigo);
public sealed record NuevaCarrera(string Nombre, string? Codigo);
public sealed record FormatoCodigo(string Formato);
public sealed record ResultadoFormato(string Formato, int Secciones);
public sealed record NuevaOferta(long PeriodoId, long SeccionId, long MateriaId, long DocenteId);
// Una materia existe una vez y la identifica su código. Carreras dice en qué pensums está.
public sealed record Materia(long Id, string Codigo, string Nombre, int Uc, decimal? NotaAprobatoria, string Carreras = "")
{
    public string Descripcion => $"{Codigo} {Nombre}";
}
public sealed record NuevaMateria(string Codigo, string Nombre, int Uc, decimal? NotaAprobatoria = null);
// La materia vista desde el pensum de una carrera. Pensums > 1: la comparte con otras (ciclo básico).
public sealed record MateriaPensum(long MateriaId, string Codigo, string Nombre, int Uc, decimal? NotaAprobatoria, int Semestre, int Pensums, long CarreraId = 0)
{
    public string SemestreTexto => Semestre == 0 ? "Electiva" : Semestre.ToString();
    public string Descripcion => $"{Codigo} {Nombre}";
    public string Compartida => Pensums > 1 ? $"En {Pensums} carreras" : "";
    // General: la nota aprobatoria de las reglas vigentes, para mostrar el número aunque la materia no tenga una propia.
    public decimal General { get; init; }
    public string AprobatoriaTexto => (NotaAprobatoria ?? (General > 0 ? General : null))?.ToString("0.##") ?? "";
}
public sealed record EnPensum(long MateriaId, int Semestre);
public sealed record FilaPensum(string Codigo, string Nombre, int Uc, int Semestre, string Accion = "")
{
    public string SemestreTexto => Semestre == 0 ? "" : Semestre.ToString();
}
public sealed record VistaPensum(List<FilaPensum> Filas, List<ErrorFila> Errores);
public sealed record ImportarPensum(List<FilaPensum> Filas);
public sealed record ResultadoPensum(int Nuevas, int Agregadas, int Cambiadas, int SinCambios);
public static class Redondeos
{
    public const string Media = "media";
    public const string Entero = "entero";
    public const string Ninguno = "ninguno";
}
public static class Indices
{
    public const string Ponderado = "ponderado";
    public const string Simple = "simple";
}
// Las reglas que usa la institución. En una planilla, NotaAprobatoria ya es la de esa materia.
public sealed record ReglasCalificacion(decimal EscalaMinima, decimal EscalaMaxima, decimal NotaAprobatoria,
    string Redondeo, string Indice, bool IndiceConAplazadas);
public sealed record CambioNombre(string Nombre);
public sealed record MateriaDocente(long MateriaId, long DocenteId);
public sealed record CargaEnBloque(long PeriodoId, long SeccionId, List<MateriaDocente> Materias);
public sealed record CopiaDeCarga(long OrigenId);
public sealed record ResultadoCarga(int Creadas, int Existentes, int SinDocente, int FueraDePensum = 0);
public sealed record MateriaAsignada(long MateriaId, long OfertaId, string Docente);
public sealed record Oferta(long Id, long PeriodoId, string Periodo, string Carrera, string Seccion,
    string Materia, long DocenteId, string Docente, int Revision, bool Cerrado, int Matriculados,
    int Pendientes, int? RevisionPublicada)
{
    public string Descripcion => $"{Materia}, sección {Seccion}, {Periodo}";
    public string Estado => Cerrado ? "cerrado" : RevisionPublicada is null ? "sin publicar"
        : RevisionPublicada == Revision ? "publicado" : "cambios sin publicar";
}
public sealed record NuevaEvaluacion(string Nombre, decimal Peso, int Revision);
public sealed record Evaluacion(long Id, string Nombre, decimal Peso, int Orden);
public sealed record Alumno(long InscripcionId, long EstudianteId, string Cedula, string Nombres,
    string Apellidos, string Estado)
{
    public string NombreCompleto => $"{Nombres} {Apellidos}";
}
public sealed record Nota(long EvaluacionId, long EstudianteId, decimal? Valor);
public sealed record GuardarNotas(int Revision, List<Nota> Notas);
public sealed record RevisionEsperada(int Revision);
public sealed record Planilla(Oferta Oferta, List<Evaluacion> Evaluaciones, List<Alumno> Estudiantes, List<Nota> Notas, ReglasCalificacion Reglas);
// Materias: códigos separados por coma. Vacío, todas las de la sección; solo lo usa la matrícula del período.
public sealed record FilaMatricula(string Cedula, string Nombres, string Apellidos, string Carrera, string Seccion, string? Correo = null, string? Materias = null);
public sealed record ErrorFila(int Fila, string Problema);
public sealed record VistaImportacion(List<FilaMatricula> Filas, List<ErrorFila> Errores);
public sealed record ImportarMatricula(int Revision, List<FilaMatricula> Filas);
// Lo que no se inscribió y por qué: ya la aprobó, ya la cursa en otra sección o fue retirado de ella.
public sealed record Omision(string Cedula, string Estudiante, string Materia, string Motivo);
public sealed record ResultadoImportacion(int Nuevos, int Existentes, string Estado, List<Omision>? Omitidas = null);
// Matrícula del período: un solo archivo con todas las secciones. Cada fila inscribe al estudiante en las
// materias de su sección; quien arrastra una materia aparece otra vez con la otra sección y esa materia.
public sealed record PlanFila(int Fila, string Cedula, string Estudiante, string Seccion, int Materias, string Detalle, string Correo = "")
{
    public string CedulaTexto => Cedulas.Mostrar(Cedula);
}
public sealed record VistaMatriculaPeriodo(List<FilaMatricula> Filas, List<PlanFila> Plan, List<ErrorFila> Errores,
    int Estudiantes, int Inscripciones, int YaEstaban, List<Omision> Omitidas);
public sealed record ImportarMatriculaPeriodo(List<FilaMatricula> Filas);
public sealed record ResultadoMatriculaPeriodo(int Estudiantes, int Inscripciones, int YaEstaban, List<Omision> Omitidas);
public sealed record ConfirmarMatricula(int Revision, List<long> Inscripciones);
public sealed record DetallePublicado(string Evaluacion, decimal Peso, decimal? Nota,
    decimal? AportePorcentual, decimal? AporteSobreVeinte);
public sealed record MateriaRecord(long OfertaId, string Periodo, string Carrera, string Seccion,
    string Materia, DateTime PublicadoEn, bool PeriodoCerrado, List<DetallePublicado> Evaluaciones,
    decimal Acumulado, decimal PesoCalificado, decimal PesoPlanificado, bool Completa, decimal? NotaFinal, string? Condicion,
    string Codigo = "", int Uc = 0, decimal EscalaMaxima = 20m);
// Promedio es el índice del récord: ponderado por UC o simple, según las reglas vigentes.
public sealed record RecordAcademico(Persona Estudiante, List<MateriaRecord> Materias, decimal? Promedio,
    string Indice = Indices.Simple, decimal EscalaMaxima = 20m, int UcAprobadas = 0);
public sealed record ComprobacionCierre(long OfertaId, string Materia, string Seccion, List<string> Pendientes);
public sealed record ResumenCierre(Periodo Periodo, List<ComprobacionCierre> Ofertas)
{
    public bool PuedeCerrar => !Periodo.Cerrado && Ofertas.Count > 0 && Ofertas.All(o => o.Pendientes.Count == 0);
}
public sealed record ErrorApi(string Mensaje, string Referencia);
public sealed record Pagina<T>(List<T> Elementos, int Total, int Desde, int Cantidad);
