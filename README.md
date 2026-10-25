# Mérito

Sistema de notas para universidades e institutos que trabajan en red local. Los docentes cargan sus evaluaciones y publican las notas, administración lleva la matrícula y cierra los períodos, y cada estudiante ve su récord.

Está hecho en C# con .NET 10. El servidor es ASP.NET Core con MariaDB y el programa de escritorio es WPF, para Windows.

## Carpetas

- `src/Entidades`, `src/Logica` y `src/Datos`: modelos, reglas académicas y acceso a la base de datos.
- `src/Servidor`: la API. También lee los Excel y envía los correos.
- `src/Escritorio`: el programa que usan administración, docentes y estudiantes.
- `pruebas`: pruebas de la API y de las pantallas.
- `instalacion`: el instalador y los scripts del servidor (preparación, respaldos y permisos).
- `documentacion`: manual, documentación técnica y diagrama de la base de datos.

## Compilar

Hace falta el SDK de .NET 10.

```powershell
dotnet build RegistroAcademico.sln
```

## Pruebas

Se corren contra un servidor local y una base de MariaDB cuyo nombre termine en `_pruebas`. Antes hay que definir estas variables:

- `ACADEMICO_PRUEBAS=1`
- `ACADEMICO_BD` y `ACADEMICO_BD_MANTENIMIENTO`: cadenas de conexión a la base de pruebas.
- `ACADEMICO_URL_SERVIDOR`: dirección del servidor de pruebas.
- `ACADEMICO_ADMIN_CEDULA` y `ACADEMICO_ADMIN_CLAVE`: cuenta de administración de pruebas.
- `ACADEMICO_CORREO_CARPETA`: carpeta donde se guardan los correos en vez de enviarlos.

Con el servidor en marcha:

```powershell
dotnet run --project pruebas/Pruebas
dotnet run --project pruebas/PruebasEscritorio
```

## Instalador

```powershell
.\instalacion\construir-instalador.ps1 -Version 1.0.1
```

Deja el instalador en `salida`. Ya trae .NET y MariaDB, así que en los equipos no hay que instalar nada más.

La última versión compilada está en la sección de descargas del repositorio.
