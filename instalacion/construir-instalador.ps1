<#
.SYNOPSIS
Publica cliente y servidor autocontenidos, reúne MariaDB y las herramientas, y compila el instalador.

.EXAMPLE
.\instalacion\construir-instalador.ps1 -Version 1.0.0
#>
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')] [string] $Version = '1.0.0',
    [string] $Iscc = (Join-Path $PSScriptRoot '..\.herramientas\innosetup-6.7.3\ISCC.exe'),
    [string] $MariaDB = (Join-Path $PSScriptRoot '..\.herramientas\mariadb-11.4.10-winx64')
)
$ErrorActionPreference = 'Stop'
$raiz = Resolve-Path (Join-Path $PSScriptRoot '..')
$publicar = Join-Path $raiz 'salida\publicar'
if (Test-Path $publicar) { Remove-Item $publicar -Recurse -Force }
New-Item -ItemType Directory -Force $publicar | Out-Null

function Publicar([string] $proyecto, [string] $destino) {
    # Autocontenido: los equipos no necesitan instalar .NET. ReadyToRun acorta el arranque.
    dotnet publish (Join-Path $raiz $proyecto) -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true `
        -p:Version=$Version -p:DebugType=none -o (Join-Path $publicar $destino) -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "No se pudo publicar $proyecto." }
}
Publicar 'src\Escritorio\Escritorio.csproj' 'cliente'
Publicar 'src\Servidor\Servidor.csproj' 'servidor'

# Solo lo que necesita el servidor de MariaDB; se omiten pruebas, símbolos y herramientas de laboratorio.
$bd = Join-Path $publicar 'mariadb'
New-Item -ItemType Directory -Force (Join-Path $bd 'bin'), (Join-Path $bd 'share') | Out-Null
# mariadb-install-db y el servicio llaman al motor por su nombre clásico, mysqld.exe.
foreach ($archivo in 'mysqld.exe', 'mariadbd.exe', 'mariadb.exe', 'mariadb-dump.exe', 'mariadb-admin.exe', 'mariadb-install-db.exe', 'mariadb-upgrade.exe') {
    Copy-Item (Join-Path $MariaDB "bin\$archivo") (Join-Path $bd 'bin')
}
Copy-Item (Join-Path $MariaDB 'bin\*.dll') (Join-Path $bd 'bin')
Get-ChildItem (Join-Path $MariaDB 'share') | Where-Object Extension -ne '.jar' | Copy-Item -Destination (Join-Path $bd 'share') -Recurse
Copy-Item (Join-Path $MariaDB 'COPYING*') $bd -ErrorAction SilentlyContinue

$herramientas = Join-Path $publicar 'herramientas'
New-Item -ItemType Directory -Force $herramientas | Out-Null
foreach ($archivo in 'preparar-servidor.ps1', 'retirar-servidor.ps1', 'aplicar-permisos.ps1', 'permisos.sql', 'respaldar.ps1', 'restaurar.ps1', 'programar-respaldo.ps1') {
    Copy-Item (Join-Path $PSScriptRoot $archivo) $herramientas
}
# A los equipos va el manual en PDF.
$manuales = Join-Path $publicar 'manuales'
New-Item -ItemType Directory -Force $manuales | Out-Null
Copy-Item (Join-Path $raiz 'documentacion\entregables\Manual de Mérito.pdf') $manuales

& $Iscc "/DVersion=$Version" (Join-Path $PSScriptRoot 'Merito.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup no pudo compilar el instalador (código $LASTEXITCODE)." }
$instalador = Join-Path $raiz "salida\Merito-$Version-instalador.exe"
"{0} ({1:N1} MB)" -f $instalador, ((Get-Item $instalador).Length / 1MB)
