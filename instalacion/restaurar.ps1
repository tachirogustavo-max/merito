<#
.SYNOPSIS
Restaura un respaldo de Mérito en una base de datos.

.DESCRIPTION
Comprueba la huella .sha256 antes de tocar nada. Lo recomendado es restaurar en una base nueva,
verificarla y luego apuntar ACADEMICO_BD a ella. Para escribir sobre una base que ya tiene tablas
hay que indicar -Reemplazar de forma expresa; detén antes el servicio de Mérito.

.EXAMPLE
.\restaurar.ps1 -Mariadb "C:\MariaDB\bin\mariadb.exe" -Opciones "C:\Merito\privado\administracion.cnf" -Archivo "D:\Respaldos\Merito\merito_merito_20261017_220000.sql.gz" -BaseDestino merito_restaurada
#>
param(
    [Parameter(Mandatory)] [string] $Mariadb,
    [Parameter(Mandatory)] [string] $Opciones,
    [Parameter(Mandatory)] [string] $Archivo,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9_]+$')] [string] $BaseDestino,
    [switch] $Reemplazar
)
$ErrorActionPreference = 'Stop'
function Consultar([string] $sql) {
    $resultado = & $Mariadb "--defaults-extra-file=$Opciones" --batch --skip-column-names -e $sql
    if ($LASTEXITCODE -ne 0) { throw "MariaDB rechazó la consulta (código $LASTEXITCODE)." }
    $resultado
}

if (-not (Test-Path $Archivo)) { throw "No existe el respaldo $Archivo." }
$huellaArchivo = "$Archivo.sha256"
if (Test-Path $huellaArchivo) {
    $esperada = (Get-Content $huellaArchivo -Raw).Split(' ')[0].Trim()
    if ((Get-FileHash $Archivo -Algorithm SHA256).Hash -ne $esperada) { throw 'La huella no coincide: el respaldo está dañado o fue modificado.' }
} else { Write-Warning 'El respaldo no tiene archivo .sha256; no se pudo comprobar su integridad.' }

$tablas = [int](Consultar "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='$BaseDestino'")
if ($tablas -gt 0 -and -not $Reemplazar) { throw "La base $BaseDestino ya tiene $tablas tablas. Restaura en una base nueva o usa -Reemplazar." }
Consultar "CREATE DATABASE IF NOT EXISTS ``$BaseDestino`` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci" | Out-Null

$temporal = [IO.Path]::Combine([IO.Path]::GetTempPath(), "merito_restaurar_$([guid]::NewGuid().ToString('N')).sql")
try {
    $entrada = [IO.File]::OpenRead($Archivo)
    $salida = [IO.File]::Create($temporal)
    try { $zip = New-Object IO.Compression.GZipStream($entrada, [IO.Compression.CompressionMode]::Decompress); $zip.CopyTo($salida); $zip.Dispose() }
    finally { $entrada.Dispose(); $salida.Dispose() }
    $proceso = Start-Process -FilePath $Mariadb -NoNewWindow -Wait -PassThru -RedirectStandardInput $temporal -RedirectStandardError "$temporal.err" `
        -ArgumentList @("--defaults-extra-file=`"$Opciones`"", '--default-character-set=utf8mb4', $BaseDestino)
    if ($proceso.ExitCode -ne 0) { throw "La restauración falló (código $($proceso.ExitCode)): $(Get-Content "$temporal.err" -Raw)" }
}
finally { Remove-Item $temporal, "$temporal.err" -ErrorAction SilentlyContinue }

$version = Consultar "SELECT COALESCE(MAX(version),0) FROM ``$BaseDestino``.versiones_esquema"
$usuarios = Consultar "SELECT COUNT(*) FROM ``$BaseDestino``.usuarios"
Write-Output "Restaurado en ${BaseDestino}: versión de esquema $version, $usuarios cuentas."
