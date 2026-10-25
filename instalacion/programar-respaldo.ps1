<#
.SYNOPSIS
Registra en el Programador de tareas un respaldo diario de Mérito.

.DESCRIPTION
Se ejecuta como administrador del servidor una sola vez. La tarea corre aunque nadie haya iniciado sesión,
con la cuenta indicada (por defecto SYSTEM, que debe poder leer el archivo de opciones y escribir en la carpeta).

.EXAMPLE
.\programar-respaldo.ps1 -MariadbDump "C:\MariaDB\bin\mariadb-dump.exe" -Opciones "C:\Merito\privado\respaldo.cnf" -BaseDatos merito -Carpeta "D:\Respaldos\Merito" -Hora "22:00"
#>
param(
    [Parameter(Mandatory)] [string] $MariadbDump,
    [Parameter(Mandatory)] [string] $Opciones,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9_]+$')] [string] $BaseDatos,
    [Parameter(Mandatory)] [string] $Carpeta,
    [ValidatePattern('^\d{2}:\d{2}$')] [string] $Hora = '22:00',
    [int] $DiasRetencion = 30,
    [string] $Cuenta = 'SYSTEM'
)
$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'respaldar.ps1'
$argumentos = "-NoProfile -ExecutionPolicy Bypass -File `"$script`" -MariadbDump `"$MariadbDump`" -Opciones `"$Opciones`" -BaseDatos $BaseDatos -Carpeta `"$Carpeta`" -DiasRetencion $DiasRetencion"
$accion = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $argumentos
$disparador = New-ScheduledTaskTrigger -Daily -At $Hora
# No se llama $opciones: PowerShell no distingue mayúsculas y pisaría el parámetro $Opciones, que es texto.
$ajustes = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 2)
Register-ScheduledTask -TaskName "Mérito, respaldo diario ($BaseDatos)" -Action $accion -Trigger $disparador -Settings $ajustes -User $Cuenta -RunLevel Highest -Force | Out-Null
Write-Output "Respaldo diario programado a las $Hora en $Carpeta."
