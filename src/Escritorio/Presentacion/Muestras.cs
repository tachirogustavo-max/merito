using Entidades;

namespace Escritorio.Presentacion;

/// <summary>
/// Datos ficticios para la vista previa del diseñador de Visual Studio. Los XAML los usan con el prefijo d:
/// (por ejemplo d:ItemsSource="{x:Static p:Muestras.Personas}"), que el programa ignora al ejecutarse.
/// </summary>
public static class Muestras
{
    public static List<Persona> Personas { get; } =
    [
        new(1, "12345678", "Ana", "Pérez", Roles.Docente, true),
        new(2, "23456789", "Luis", "Gómez", Roles.Docente, true),
        new(3, "30111222", "María", "Rodríguez", Roles.Estudiante, true),
        new(4, "9876543", "Carlos", "Medina", Roles.Administrador, false),
    ];

    public static List<Oferta> Ofertas { get; } =
    [
        new(1, 1, "2-2026", "Informática", "A", "Matemática II", 1, "Ana Pérez", 3, false, 32, 0, 3),
        new(2, 1, "2-2026", "Informática", "A", "Programación II", 2, "Luis Gómez", 2, false, 30, 2, null),
        new(3, 1, "2-2026", "Informática", "B", "Inglés técnico", 1, "Ana Pérez", 4, false, 28, 0, 3),
    ];

    public static List<Evaluacion> Evaluaciones { get; } = [new(1, "Primer parcial", 30m, 1), new(2, "Proyecto", 30m, 2), new(3, "Final", 40m, 3)];

    public static List<Alumno> Alumnos { get; } =
    [
        new(1, 3, "30111222", "María", "Rodríguez", "confirmada"),
        new(2, 5, "30222333", "José", "Hernández", "pendiente"),
    ];

    public static List<VistaRecord.GrupoPeriodo> Periodos { get; } =
    [
        new("Período 1-2026", true,
        [
            new("Matemática I", "Informática, sección A", "Acumulado: 15 puntos, con el 100 % de la materia evaluado", 100, "15", true, false,
                Condiciones.Aprobado, "Aprobado", [new("Primer parcial", 30m, 14m, 21m, 4.2m), new("Segundo parcial", 30m, 16m, 24m, 4.8m), new("Final", 40m, 15m, 30m, 6m)],
                "Última publicación: 10/07/2026 15:00"),
        ]),
        new("Período 2-2026", false,
        [
            new("Matemática II", "Informática, sección A", "Acumulado: 4,05 puntos, con el 30 % de la materia evaluado", 30, "Pendiente", false, false,
                null, "", [new("Primer parcial", 30m, 13.5m, 20.25m, 4.05m), new("Final", 70m, null, null, null)], "Última publicación: 18/10/2026 09:30"),
        ]),
    ];

    public static List<VistaCargaSeccion.Fila> FilasCarga { get; } =
    [
        new(new MateriaPensum(1, "MAT-21215", "Cálculo II", 4, null, 2, 3), null, 2) { Docente = Personas[0] },
        new(new MateriaPensum(2, "FIS-21224", "Física II", 4, null, 2, 3), null, 2),
        new(new MateriaPensum(3, "SIS-22113", "Programación II", 3, null, 2, 1), null, 2),
        new(new MateriaPensum(4, "ELE-20013", "Oratoria", 2, null, 0, 1), null, 2),
        new(new MateriaPensum(5, "ING-21122", "Inglés II", 2, null, 2, 3), "Luis Gómez", 2),
    ];

    public static List<MateriaPensum> Pensum { get; } =
    [
        new(1, "MAT-21114", "Cálculo I", 4, null, 1, 3),
        new(2, "QUI-21113", "Química general", 3, null, 1, 3),
        new(3, "DIN-21113", "Defensa integral de la nación I", 2, null, 1, 5),
        new(4, "MAT-21215", "Cálculo II", 4, null, 2, 3),
        new(5, "SIS-22113", "Programación II", 3, null, 2, 1),
        new(6, "PRO-28510", "Proyecto de grado", 6, 16m, 8, 1),
        new(7, "ELE-20013", "Oratoria", 2, null, 0, 1),
    ];

    public static List<VentanaCierre.Renglon> RenglonesCierre { get; } =
    [
        new("Matemática II, sección A", "Lista para el cierre.", true),
        new("Programación II, sección A", "Faltan 4 calificaciones.\nEl docente debe publicar la última versión.", false),
    ];
}
