<#
.SYNOPSIS
Crea un respaldo comprimido y verificado de la base de datos de Mérito.

.DESCRIPTION
Usa una transacción coherente (--single-transaction): no detiene el servicio ni bloquea a los usuarios.
El archivo queda como merito_<base>_<fecha>.sql.gz junto a su huella .sha256. Solo se conserva un
respaldo si mariadb-dump terminó completo. Los respaldos anteriores a -DiasRetencion se eliminan.
Cada ejecución queda anotada en respaldos.log dentro de la carpeta de destino.

.EXAMPLE
.\respaldar.ps1 -MariadbDump "C:\MariaDB\bin\mariadb-dump.exe" -Opciones "C:\Merito\privado\respaldo.cnf" -BaseDatos merito -Carpeta "D:\Respaldos\Merito"
#>
param(
    [Parameter(Mandatory)] [string] $MariadbDump,
    [Parameter(Mandatory)] [string] $Opciones,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9_]+$')] [string] $BaseDatos,
    [Parameter(Mandatory)] [string] $Carpeta,
    [ValidateRange(1, 3650)] [int] $DiasRetencion = 30
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $Carpeta | Out-Null
$registro = Join-Path $Carpeta 'respaldos.log'
function Anotar([string] $texto) { Add-Content -Path $registro -Value "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $texto" -Encoding UTF8 }

$nombre = "merito_${BaseDatos}_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
$temporal = Join-Path $Carpeta "$nombre.sql.parcial"
$destino = Join-Path $Carpeta "$nombre.sql.gz"
try {
    $proceso = Start-Process -FilePath $MariadbDump -NoNewWindow -Wait -PassThru -RedirectStandardOutput $temporal -RedirectStandardError "$temporal.err" -ArgumentList @(
        "--defaults-extra-file=`"$Opciones`"", '--single-transaction', '--quick', '--routines', '--triggers', '--events',
        '--hex-blob', '--default-character-set=utf8mb4', '--no-tablespaces', $BaseDatos)
    if ($proceso.ExitCode -ne 0) { throw "mariadb-dump terminó con código $($proceso.ExitCode): $(Get-Content "$temporal.err" -Raw)" }
    $final = Get-Content $temporal -Tail 1
    if ($final -notmatch 'Dump completed') { throw 'El respaldo quedó incompleto: falta la marca de finalización.' }

    $entrada = [IO.File]::OpenRead($temporal)
    $salida = [IO.File]::Create("$destino.parcial")
    try {
        $zip = New-Object IO.Compression.GZipStream($salida, [IO.Compression.CompressionLevel]::Optimal)
        $entrada.CopyTo($zip); $zip.Dispose()
    } finally { $entrada.Dispose(); $salida.Dispose() }
    Move-Item "$destino.parcial" $destino
    $huella = (Get-FileHash $destino -Algorithm SHA256).Hash
    Set-Content -Path "$destino.sha256" -Value "$huella  $nombre.sql.gz" -Encoding ASCII
    $tamano = [math]::Round((Get-Item $destino).Length / 1MB, 2)
    Anotar "correcto $nombre.sql.gz ($tamano MB)"

    $limite = (Get-Date).AddDays(-$DiasRetencion)
    Get-ChildItem $Carpeta -Filter "merito_${BaseDatos}_*.sql.gz" | Where-Object LastWriteTime -lt $limite | ForEach-Object {
        Remove-Item $_.FullName, "$($_.FullName).sha256" -ErrorAction SilentlyContinue
        Anotar "retirado por antigüedad $($_.Name)"
    }
    Write-Output $destino
}
catch {
    Anotar "FALLÓ ${nombre}: $($_.Exception.Message)"
    throw
}
finally {
    Remove-Item $temporal, "$temporal.err", "$destino.parcial" -ErrorAction SilentlyContinue
}
