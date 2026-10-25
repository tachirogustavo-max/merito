<#
.SYNOPSIS
Prepara o actualiza el servidor de Mérito: base de datos, cuentas, migraciones, permisos, servicio, firewall y respaldo.

.DESCRIPTION
Lo ejecuta el instalador como administrador. En una instalación nueva crea MariaDB como servicio local
(solo escucha en 127.0.0.1), tres cuentas con contraseñas aleatorias y la configuración protegida en
<Datos>\privado. En una actualización conserva los datos: aplica las migraciones nuevas y reinicia.
Nunca escribe contraseñas en el registro de instalación. Si todavía no hay administración, deja un código de configuración
de un solo uso en <Datos>\privado\codigo-configuracion.txt: con él se crea la primera cuenta desde el programa.

-SinServicios ejecuta todo como procesos comunes, sin servicios, firewall ni tareas programadas;
se usa para probar el procedimiento sin modificar la configuración del equipo.
#>
param(
    [Parameter(Mandatory)] [string] $Programa,
    [string] $Datos = (Join-Path $env:ProgramData 'Merito'),
    [ValidateRange(1024, 65535)] [int] $PuertoServicio = 5187,
    [ValidateRange(1024, 65535)] [int] $PuertoBD = 3307,
    [string] $CarpetaRespaldos,
    [ValidatePattern('^\d{2}:\d{2}$')] [string] $HoraRespaldo = '22:00',
    [switch] $SinServicios
)
$ErrorActionPreference = 'Stop'
$bin = Join-Path $Programa 'mariadb\bin'
$servidor = Join-Path $Programa 'servidor\Merito.Servidor.exe'
$herramientas = Join-Path $Programa 'herramientas'
$privado = Join-Path $Datos 'privado'
$baseDatos = Join-Path $Datos 'basedatos'
if (-not $CarpetaRespaldos) { $CarpetaRespaldos = Join-Path $Datos 'respaldos' }
New-Item -ItemType Directory -Force $Datos, $privado | Out-Null
$registro = Join-Path $Datos 'instalacion.log'
function Anotar([string] $texto) { Add-Content -Path $registro -Value "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $texto" -Encoding UTF8; Write-Output $texto }
# El instalador solo ve el código de salida: el motivo de cualquier fallo tiene que quedar en el registro.
trap { Anotar "ERROR: $($_.Exception.Message) ($([IO.Path]::GetFileName($_.InvocationInfo.ScriptName)), línea $($_.InvocationInfo.ScriptLineNumber))"; break }

function NuevaClave {
    $alfabeto = [char[]]'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
    $bytes = New-Object byte[] 40
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    -join ($bytes | ForEach-Object { $alfabeto[$_ % $alfabeto.Length] })
}
function Escribir([string] $ruta, [string] $contenido) { [IO.File]::WriteAllText($ruta, $contenido, [Text.UTF8Encoding]::new($false)) }
function EsperarPuerto([int] $puerto, [int] $segundos) {
    for ($i = 0; $i -lt $segundos; $i++) {
        $cliente = New-Object Net.Sockets.TcpClient
        try { $cliente.Connect('127.0.0.1', $puerto); return } catch { Start-Sleep 1 } finally { $cliente.Dispose() }
    }
    throw "Nada responde en el puerto $puerto después de $segundos segundos."
}
function Sql([string] $sentencias) {
    $sentencias | & (Join-Path $bin 'mariadb.exe') "--defaults-extra-file=$(Join-Path $privado 'raiz.cnf')" --batch
    if ($LASTEXITCODE -ne 0) { throw "MariaDB rechazó la configuración (código $LASTEXITCODE)." }
}
function Opciones([string] $usuario, [string] $clave) { "[client]`r`nuser=$usuario`r`npassword=$clave`r`nhost=127.0.0.1`r`nport=$PuertoBD`r`n" }

Anotar "Inicio. Programa en $Programa, datos en $Datos"

# 1. Motor de base de datos, solo accesible desde este equipo.
$nueva = -not (Test-Path (Join-Path $baseDatos 'mysql'))
if ($nueva) {
    $raiz = NuevaClave
    $argumentos = @("--datadir=$baseDatos", "--port=$PuertoBD", "--password=$raiz")
    if (-not $SinServicios) { $argumentos += '--service=MeritoBD' }
    & (Join-Path $bin 'mariadb-install-db.exe') @argumentos | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "No se pudo inicializar MariaDB (código $LASTEXITCODE)." }
    $ini = Join-Path $baseDatos 'my.ini'
    $contenido = Get-Content $ini -Raw
    $contenido = $contenido -replace '\[mysqld\]', "[mysqld]`r`nbind-address=127.0.0.1`r`ncharacter-set-server=utf8mb4`r`ncollation-server=utf8mb4_unicode_ci"
    Escribir $ini $contenido
    Escribir (Join-Path $privado 'raiz.cnf') (Opciones 'root' $raiz)
    Anotar 'Base de datos inicializada; solo escucha en 127.0.0.1.'
}
if ($SinServicios) {
    Start-Process (Join-Path $bin 'mariadbd.exe') -ArgumentList "--defaults-file=`"$(Join-Path $baseDatos 'my.ini')`"" -WindowStyle Hidden | Out-Null
} else {
    $servicioBD = Get-Service MeritoBD -ErrorAction SilentlyContinue
    if ($servicioBD.Status -ne 'Running') { Start-Service MeritoBD }
}
EsperarPuerto $PuertoBD 60

# 2. Cuentas y configuración protegida, solo la primera vez.
$configuracion = Join-Path $privado 'servidor.json'
if (-not (Test-Path $configuracion)) {
    $migracion = NuevaClave; $servicio = NuevaClave; $respaldo = NuevaClave
    Sql @"
CREATE DATABASE IF NOT EXISTS merito CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS 'merito_migracion'@'localhost' IDENTIFIED BY '$migracion';
CREATE USER IF NOT EXISTS 'merito_servicio'@'localhost' IDENTIFIED BY '$servicio';
CREATE USER IF NOT EXISTS 'merito_respaldo'@'localhost' IDENTIFIED BY '$respaldo';
GRANT ALL PRIVILEGES ON merito.* TO 'merito_migracion'@'localhost';
"@
    $cadena = { param($usuario, $clave) "Server=127.0.0.1;Port=$PuertoBD;Database=merito;User ID=$usuario;Password=$clave" }
    $json = [ordered]@{
        BaseDatos = (& $cadena 'merito_servicio' $servicio)
        BaseDatosMigracion = (& $cadena 'merito_migracion' $migracion)
        Urls = "http://0.0.0.0:$PuertoServicio"
    } | ConvertTo-Json
    Escribir $configuracion $json
    Escribir (Join-Path $privado 'respaldo.cnf') (Opciones 'merito_respaldo' $respaldo)
    Anotar 'Cuentas creadas con contraseñas aleatorias.'
}

# La cuenta de correo va en su propio archivo, para cambiarla sin tocar servidor.json. Se deja la plantilla vacía;
# con Usuario y Clave completos y el servicio reiniciado, se activa el registro por correo. No se pisa uno que ya exista.
$correo = Join-Path $privado 'correo.json'
if (-not (Test-Path $correo)) {
    Escribir $correo (([ordered]@{ Servidor = 'smtp.gmail.com'; Puerto = 587; Usuario = ''; Clave = ''; Remitente = ''; Nombre = 'Mérito' } | ConvertTo-Json))
    Anotar "Plantilla de correo en $correo."
}

# 3. Esquema y permisos mínimos.
$env:MERITO_CONFIGURACION = $configuracion
& $servidor --migrar | ForEach-Object { Anotar $_ }
if ($LASTEXITCODE -ne 0) { throw "Las migraciones fallaron (código $LASTEXITCODE)." }
& (Join-Path $herramientas 'aplicar-permisos.ps1') -Mariadb (Join-Path $bin 'mariadb.exe') -Opciones (Join-Path $privado 'raiz.cnf') `
    -BaseDatos merito -Servicio merito_servicio -Respaldo merito_respaldo | ForEach-Object { Anotar $_ }

# 4. Código de configuración mientras no exista administración. Solo su huella llega a la base de datos.
$archivoCodigo = Join-Path $privado 'codigo-configuracion.txt'
$administradores = [int]("SELECT COUNT(*) FROM merito.usuarios WHERE rol='administrador'" | & (Join-Path $bin 'mariadb.exe') "--defaults-extra-file=$(Join-Path $privado 'raiz.cnf')" --batch --skip-column-names)
if ($administradores -eq 0) {
    if (-not (Test-Path $archivoCodigo)) {
        $alfabeto = [char[]]'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'
        $bytes = New-Object byte[] 16
        [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
        $letras = -join ($bytes | ForEach-Object { $alfabeto[$_ % $alfabeto.Length] })
        Escribir $archivoCodigo (($letras -split '(.{4})' | Where-Object { $_ }) -join '-')
    }
    $normalizado = ((Get-Content $archivoCodigo -Raw) -replace '[^A-Za-z0-9]', '').ToUpperInvariant()
    $huella = [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($normalizado))).Replace('-', '')
    Sql "INSERT IGNORE INTO merito.codigo_configuracion (huella) VALUES ('$huella');"
    Anotar 'Código de configuración listo para crear la cuenta de administración desde Mérito.'
} elseif (Test-Path $archivoCodigo) { Remove-Item $archivoCodigo -Force }

# 5. Servicio, red local y respaldo diario.
if ($SinServicios) {
    Start-Process $servidor -WindowStyle Hidden | Out-Null
} else {
    # El servicio escribe en el Visor de eventos con este origen; registrarlo requiere administración.
    if (-not [Diagnostics.EventLog]::SourceExists('Merito')) { New-EventLog -LogName Application -Source 'Merito' }
    # Cuenta virtual propia: el servicio no corre como SYSTEM y solo lee su configuración.
    # La lista se arma desde cero: icacls /grant:r no quita permisos que ya tuviera la carpeta, por ejemplo los del
    # usuario que la creó, y con ellos un programa sin elevar podría leer las contraseñas internas.
    $acceso = New-Object Security.AccessControl.DirectorySecurity
    $acceso.SetAccessRuleProtection($true, $false)
    $acceso.SetOwner([Security.Principal.SecurityIdentifier] 'S-1-5-32-544')
    foreach ($sid in 'S-1-5-18', 'S-1-5-32-544') {
        $acceso.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule ([Security.Principal.SecurityIdentifier] $sid), 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
    }
    Set-Acl -Path $privado -AclObject $acceso
    & icacls "$privado\*" /reset /T /Q | Out-Null
    if (-not (Get-Service Merito -ErrorAction SilentlyContinue)) {
        & sc.exe create Merito binPath= "`"$servidor`"" start= delayed-auto depend= MeritoBD obj= 'NT SERVICE\Merito' DisplayName= 'Mérito' | Out-Null
        & sc.exe description Merito 'Servidor de Mérito para la red local de la institución.' | Out-Null
        & sc.exe failure Merito reset= 86400 actions= restart/5000/restart/30000/restart/60000 | Out-Null
    }
    & icacls $privado /grant 'NT SERVICE\Merito:(OI)(CI)R' | Out-Null
    & icacls $configuracion /grant 'NT SERVICE\Merito:M' | Out-Null
    Restart-Service Merito -ErrorAction SilentlyContinue
    if ((Get-Service Merito).Status -ne 'Running') { Start-Service Merito }

    Get-NetFirewallRule -DisplayName 'Mérito' -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    New-NetFirewallRule -DisplayName 'Mérito' -Description 'Acceso de los equipos de la red local al servidor de Mérito.' -Direction Inbound `
        -Protocol TCP -LocalPort $PuertoServicio -Profile Domain,Private -RemoteAddress LocalSubnet -Action Allow | Out-Null
    Anotar "Firewall abierto en el puerto $PuertoServicio solo para la red local."

    & (Join-Path $herramientas 'programar-respaldo.ps1') -MariadbDump (Join-Path $bin 'mariadb-dump.exe') -Opciones (Join-Path $privado 'respaldo.cnf') `
        -BaseDatos merito -Carpeta $CarpetaRespaldos -Hora $HoraRespaldo | ForEach-Object { Anotar $_ }
}

# 6. Comprobación final.
for ($i = 0; $i -lt 60; $i++) {
    try {
        $salud = Invoke-RestMethod "http://127.0.0.1:$PuertoServicio/salud" -TimeoutSec 3
        if ($salud.servicio -eq 'merito') { Anotar "Mérito responde en el puerto $PuertoServicio (esquema $($salud.version))."; exit 0 }
    } catch { Start-Sleep 1 }
}
throw "El servicio no respondió en el puerto $PuertoServicio. Revisa el Visor de eventos y $registro."
