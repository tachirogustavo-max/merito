<#
.SYNOPSIS
Retira los servicios de Mérito al desinstalar. Por defecto conserva la base de datos y los respaldos.
#>
param(
    [Parameter(Mandatory)] [string] $Programa,
    [string] $Datos = (Join-Path $env:ProgramData 'Merito'),
    [switch] $BorrarDatos
)
$ErrorActionPreference = 'Continue'
$registro = Join-Path $Datos 'instalacion.log'
function Anotar([string] $texto) { Add-Content -Path $registro -Value "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $texto" -Encoding UTF8 -ErrorAction SilentlyContinue }

if (Get-Service Merito -ErrorAction SilentlyContinue) { Stop-Service Merito -Force; & sc.exe delete Merito | Out-Null }
if (Get-Service MeritoBD -ErrorAction SilentlyContinue) {
    # Apagado ordenado: InnoDB cierra sus archivos antes de retirar el servicio.
    $raiz = Join-Path $Datos 'privado\raiz.cnf'
    $admin = Join-Path $Programa 'mariadb\bin\mariadb-admin.exe'
    if ((Test-Path $raiz) -and (Test-Path $admin)) { & $admin "--defaults-extra-file=$raiz" shutdown 2>$null }
    Stop-Service MeritoBD -Force -ErrorAction SilentlyContinue
    & sc.exe delete MeritoBD | Out-Null
}
Get-NetFirewallRule -DisplayName 'Mérito' -ErrorAction SilentlyContinue | Remove-NetFirewallRule
Get-ScheduledTask -TaskName 'Mérito, respaldo diario*' -ErrorAction SilentlyContinue | Unregister-ScheduledTask -Confirm:$false
Anotar 'Servicios, regla de firewall y respaldo programado retirados.'
if ($BorrarDatos) { Remove-Item $Datos -Recurse -Force -ErrorAction SilentlyContinue }
else { Anotar "Se conservan la base de datos y los respaldos en $Datos." }
