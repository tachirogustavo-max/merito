<#
.SYNOPSIS
Aplica los permisos mínimos de permisos.sql a la cuenta del servicio y a la de respaldo.

.DESCRIPTION
Debe ejecutarlo quien administre MariaDB, con un archivo de opciones que contenga su usuario
y contraseña (nunca en la línea de órdenes). Las cuentas deben existir: este script no crea
contraseñas ni las muestra.

.EXAMPLE
.\aplicar-permisos.ps1 -Mariadb "C:\MariaDB\bin\mariadb.exe" -Opciones "C:\Merito\privado\administracion.cnf" -BaseDatos merito -Servicio merito_servicio -Respaldo merito_respaldo
#>
param(
    [Parameter(Mandatory)] [string] $Mariadb,
    [Parameter(Mandatory)] [string] $Opciones,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9_]+$')] [string] $BaseDatos,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9_]+$')] [string] $Servicio,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9_]+$')] [string] $Respaldo,
    [ValidatePattern('^[A-Za-z0-9_.%:-]+$')] [string] $Equipo = 'localhost'
)
$ErrorActionPreference = 'Stop'
$plantilla = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'permisos.sql')
$sql = $plantilla.Replace('{BD}', $BaseDatos).Replace('{SERVICIO}', $Servicio).Replace('{RESPALDO}', $Respaldo).Replace('{EQUIPO}', $Equipo)
$sql | & $Mariadb "--defaults-extra-file=$Opciones" --batch
if ($LASTEXITCODE -ne 0) { throw "MariaDB rechazó los permisos (código $LASTEXITCODE)." }
Write-Output "Permisos aplicados a $Servicio y $Respaldo sobre $BaseDatos."
