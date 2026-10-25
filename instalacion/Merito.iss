; Instalador de Mérito. Se compila con construir-instalador.ps1.
#ifndef Version
  #define Version "1.0.0"
#endif
#define Nombre "Mérito"
#define Publicar "..\salida\publicar"
#define PuertoServicio "5187"

[Setup]
AppId={{6F2B9C1E-4A7D-4E2B-9C3A-8D1F5E7A2B40}
AppName={#Nombre}
AppVersion={#Version}
AppVerName={#Nombre} {#Version}
AppPublisher={#Nombre}
AppComments=Registro académico para la red local de la institución
VersionInfoDescription=Instalador de Mérito
VersionInfoProductName=Mérito
DefaultDirName={autopf}\Merito
DisableProgramGroupPage=yes
DisableWelcomePage=no
UsePreviousAppDir=yes
UsePreviousSetupType=yes
OutputDir=..\salida
OutputBaseFilename=Merito-{#Version}-instalador
SetupIconFile=..\src\Escritorio\Recursos\merito.ico
UninstallDisplayIcon={app}\merito.ico
UninstallDisplayName={#Nombre}
WizardStyle=modern dynamic
WizardSizePercent=110
WizardImageFile=imagenes\asistente-100.bmp,imagenes\asistente-125.bmp,imagenes\asistente-150.bmp,imagenes\asistente-175.bmp,imagenes\asistente-200.bmp,imagenes\asistente-225.bmp,imagenes\asistente-250.bmp
WizardImageFileDynamicDark=imagenes\asistente-100.bmp,imagenes\asistente-125.bmp,imagenes\asistente-150.bmp,imagenes\asistente-175.bmp,imagenes\asistente-200.bmp,imagenes\asistente-225.bmp,imagenes\asistente-250.bmp
WizardSmallImageFile=imagenes\pequena-100.bmp,imagenes\pequena-125.bmp,imagenes\pequena-150.bmp,imagenes\pequena-175.bmp,imagenes\pequena-200.bmp,imagenes\pequena-225.bmp,imagenes\pequena-250.bmp
WizardSmallImageFileDynamicDark=imagenes\pequena-100.bmp,imagenes\pequena-125.bmp,imagenes\pequena-150.bmp,imagenes\pequena-175.bmp,imagenes\pequena-200.bmp,imagenes\pequena-225.bmp,imagenes\pequena-250.bmp
; El logo de la cabecera es de 32 bits con transparencia: se ve igual en el tema claro y en el oscuro.
WizardImageAlphaFormat=defined
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#ifdef Vista
; Solo para revisar el asistente sin instalar: no pide elevación.
PrivilegesRequired=lowest
#else
PrivilegesRequired=admin
#endif
MinVersion=10.0.17763
CloseApplications=yes
RestartApplications=no
ShowLanguageDialog=no

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Messages]
es.WelcomeLabel1=Te damos la bienvenida a Mérito
es.WelcomeLabel2=Este asistente instala Mérito, el registro académico de tu institución: evaluaciones, matrícula y récord de notas en la red local, sin necesidad de internet.%n%nEn el siguiente paso elegirás si este equipo será el servidor, un equipo cliente o ambos.
es.SelectComponentsDesc=¿Qué papel tendrá este equipo?
es.SelectComponentsLabel2=Instala el servidor en un solo equipo de la institución, el que permanezca encendido. En los demás instala el equipo cliente.
es.FinishedHeadingLabel=Mérito está listo
es.SetupLdrStartupMessage=Se instalará %1. ¿Quieres continuar?
es.SetupAppRunningError=%1 está abierto.%n%nCiérralo y pulsa Aceptar para continuar, o Cancelar para salir.
es.UninstallAppRunningError=%1 está abierto.%n%nCiérralo y pulsa Aceptar para continuar, o Cancelar para salir.
es.ExitSetupTitle=Salir de la instalación
es.ExitSetupMessage=La instalación todavía no terminó. Si sales ahora, Mérito no quedará instalado.%n%nPuedes volver a ejecutar este instalador cuando quieras.%n%n¿Salir de la instalación?
es.ClickNext=Pulsa Siguiente para continuar o Cancelar para salir.
es.BrowseDialogLabel=Elige una carpeta y pulsa Aceptar.
es.WizardSelectDir=Carpeta de instalación
es.SelectDirDesc=¿Dónde se instalará Mérito?
es.SelectDirLabel3=Mérito se instalará en esta carpeta.
es.SelectDirBrowseLabel=Pulsa Siguiente para continuar. Para usar otra carpeta, pulsa Examinar.
es.DirExists=La carpeta%n%n%1%n%nya existe. ¿Instalar en ella de todos modos?
es.DirDoesntExist=La carpeta%n%n%1%n%nno existe. ¿Crearla?
es.DiskSpaceWarning=La instalación necesita al menos %1 KB libres y la unidad elegida tiene %2 KB.%n%n¿Continuar de todos modos?
es.WizardSelectComponents=Papel de este equipo
es.WizardSelectTasks=Opciones adicionales
es.SelectTasksDesc=¿Quieres algo más?
es.SelectTasksLabel2=Marca lo que quieras y pulsa Siguiente.
es.WizardReady=Todo listo para instalar
es.ReadyLabel1=Revisa el resumen antes de comenzar.
es.ReadyLabel2a=Pulsa Instalar para comenzar, o Atrás para cambiar algo.
es.ReadyLabel2b=Pulsa Instalar para comenzar.
es.ReadyMemoDir=Carpeta:
es.ReadyMemoType=Tipo de instalación:
es.ReadyMemoComponents=Se instalará:
es.ReadyMemoTasks=Opciones:
es.WizardPreparing=Preparando la instalación
es.PreparingDesc=Se están preparando los archivos de Mérito.
es.WizardInstalling=Instalando
es.InstallingLabel=Espera un momento mientras se instala Mérito.
es.FinishedLabelNoIcons=Mérito quedó instalado en este equipo.
es.FinishedLabel=Mérito quedó instalado en este equipo. Lo encontrarás en el menú Inicio.
es.ClickFinish=Pulsa Finalizar para cerrar el instalador.
es.ConfirmUninstall=¿Desinstalar %1 de este equipo?
es.UninstallStatusLabel=Espera un momento mientras se desinstala %1.
es.UninstalledAll=%1 se desinstaló de este equipo.
es.WizardUninstalling=Desinstalando

[Types]
Name: "cliente"; Description: "Equipo cliente: se conecta al servidor de la institución"
Name: "servidor"; Description: "Servidor: guarda la información para toda la red"
Name: "completo"; Description: "Servidor y cliente en este mismo equipo"

[Components]
Name: "cliente"; Description: "Programa Mérito"; Types: cliente completo
Name: "servidor"; Description: "Servidor y base de datos (MariaDB)"; Types: servidor completo

[Tasks]
Name: "escritorio"; Description: "Crear un acceso directo en el escritorio"; Components: cliente

[Files]
Source: "..\src\Escritorio\Recursos\merito.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Publicar}\cliente\*"; DestDir: "{app}\cliente"; Components: cliente; Flags: recursesubdirs ignoreversion
Source: "{#Publicar}\servidor\*"; DestDir: "{app}\servidor"; Components: servidor; Flags: recursesubdirs ignoreversion
Source: "{#Publicar}\mariadb\*"; DestDir: "{app}\mariadb"; Components: servidor; Flags: recursesubdirs ignoreversion
Source: "{#Publicar}\herramientas\*"; DestDir: "{app}\herramientas"; Components: servidor; Flags: recursesubdirs ignoreversion
Source: "{#Publicar}\manuales\*"; DestDir: "{app}\manuales"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{autoprograms}\Mérito"; Filename: "{app}\cliente\Merito.exe"; Comment: "Registro académico"; Components: cliente
Name: "{autoprograms}\Manuales de Mérito"; Filename: "{app}\manuales"; Comment: "Guías de uso e instalación"
Name: "{autodesktop}\Mérito"; Filename: "{app}\cliente\Merito.exe"; Comment: "Registro académico"; Tasks: escritorio

[Run]
Filename: "{app}\cliente\Merito.exe"; Description: "Abrir Mérito ahora"; Flags: postinstall nowait skipifsilent; Components: cliente

[Code]
var
  PaginaConexion: TWizardPage;
  CampoServidor, CampoPuerto: TNewEdit;
  BotonProbar: TNewButton;
  ResultadoPrueba: TNewStaticText;
  ConexionComprobada: Boolean;
  PaginaRespaldo: TInputDirWizardPage;
  CampoHora: TNewEdit;
  ServidorPreparado: Boolean;
  CampoCodigo: TNewEdit;
  BotonCopiar: TNewButton;

function Configurado: Boolean;
begin
  Result := FileExists(ExpandConstant('{commonappdata}\Merito\privado\servidor.json'));
end;

function DireccionServidor: string;
begin
  if WizardIsComponentSelected('servidor') then
    Result := 'http://127.0.0.1:{#PuertoServicio}'
  else
    Result := 'http://' + Trim(CampoServidor.Text) + ':' + Trim(CampoPuerto.Text);
end;

{ Consulta /salud con WinHTTP: confirma que en esa dirección responde un servidor de Mérito. }
function ProbarServidor(const Direccion: string; var Detalle: string): Boolean;
var
  Http: Variant;
  Estado: Integer;
  Respuesta: string;
begin
  Result := False;
  try
    Http := CreateOleObject('WinHttp.WinHttpRequest.5.1');
    Http.SetTimeouts(3000, 3000, 3000, 5000);
    Http.Open('GET', Direccion + '/salud', False);
    Http.Send('');
    Estado := Http.Status;
    Respuesta := Http.ResponseText;
    if (Estado = 200) and (Pos('"merito"', Respuesta) > 0) then
    begin
      Result := True;
      Detalle := 'Conectado. El servidor de Mérito responde en ' + Direccion + '.';
    end
    else
      Detalle := 'En esa dirección responde otro programa (código ' + IntToStr(Estado) + ').';
  except
    Detalle := 'Sin respuesta. Comprueba la IP, que el servidor esté encendido y que el firewall permita el puerto.';
  end;
end;

procedure AlProbar(Sender: TObject);
var
  Detalle: string;
begin
  ResultadoPrueba.Caption := 'Comprobando…';
  ResultadoPrueba.Font.Color := clGrayText;
  WizardForm.Refresh;
  ConexionComprobada := ProbarServidor(DireccionServidor, Detalle);
  ResultadoPrueba.Caption := Detalle;
  if ConexionComprobada then ResultadoPrueba.Font.Color := $005B7913 else ResultadoPrueba.Font.Color := $004431BE;
end;

procedure AlCambiarDireccion(Sender: TObject);
begin
  ConexionComprobada := False;
  ResultadoPrueba.Caption := '';
end;

function Etiqueta(Pagina: TWizardPage; const Texto: string; Arriba: Integer): TNewStaticText;
begin
  Result := TNewStaticText.Create(Pagina);
  Result.Parent := Pagina.Surface;
  Result.Caption := Texto;
  Result.Top := Arriba;
  Result.Width := Pagina.SurfaceWidth;
  Result.AutoSize := True;
  Result.WordWrap := True;
end;

procedure CrearPaginaConexion;
var
  Rotulo: TNewStaticText;
begin
  PaginaConexion := CreateCustomPage(wpSelectTasks, 'Servidor de Mérito',
    '¿En qué equipo de la red está instalado el servidor?');
  Etiqueta(PaginaConexion, 'Escribe la IP del equipo servidor. Aparece al final de su instalación; por ejemplo, 192.168.1.10. ' +
    'Si algún día cambia, vuelve a ejecutar este instalador y escribe la nueva.', 0);
  Etiqueta(PaginaConexion, 'IP o nombre del servidor:', ScaleY(52));
  CampoServidor := TNewEdit.Create(PaginaConexion);
  CampoServidor.Parent := PaginaConexion.Surface;
  CampoServidor.Top := ScaleY(72);
  CampoServidor.Width := ScaleX(250);
  CampoServidor.OnChange := @AlCambiarDireccion;
  Rotulo := Etiqueta(PaginaConexion, 'Puerto:', ScaleY(52));
  Rotulo.Left := ScaleX(266);
  CampoPuerto := TNewEdit.Create(PaginaConexion);
  CampoPuerto.Parent := PaginaConexion.Surface;
  CampoPuerto.Top := ScaleY(72);
  CampoPuerto.Left := ScaleX(266);
  CampoPuerto.Width := ScaleX(70);
  CampoPuerto.Text := '{#PuertoServicio}';
  CampoPuerto.OnChange := @AlCambiarDireccion;
  BotonProbar := TNewButton.Create(PaginaConexion);
  BotonProbar.Parent := PaginaConexion.Surface;
  BotonProbar.Caption := 'Probar conexión';
  BotonProbar.Top := ScaleY(70);
  BotonProbar.Left := ScaleX(350);
  BotonProbar.Width := ScaleX(120);
  BotonProbar.Height := ScaleY(25);
  BotonProbar.OnClick := @AlProbar;
  ResultadoPrueba := Etiqueta(PaginaConexion, '', ScaleY(112));
  { En una instalación silenciosa la dirección llega por parámetros: /SERVIDOR=192.168.1.10 /PUERTO=5187. }
  CampoServidor.Text := ExpandConstant('{param:SERVIDOR|' + GetPreviousData('Servidor', '') + '}');
  CampoPuerto.Text := ExpandConstant('{param:PUERTO|{#PuertoServicio}}');
end;

procedure CrearPaginasServidor;
begin
  PaginaRespaldo := CreateInputDirPage(wpSelectTasks, 'Respaldos automáticos',
    'Mérito guarda una copia completa de la base de datos todos los días.',
    'Elige la carpeta de los respaldos. Copia esa carpeta a otro disco o equipo al menos una vez por semana.',
    False, '');
  PaginaRespaldo.Add('Carpeta de respaldos:');
  PaginaRespaldo.Values[0] := GetPreviousData('Respaldos', ExpandConstant('{commonappdata}\Merito\respaldos'));
  Etiqueta(PaginaRespaldo, 'Hora del respaldo diario (HH:MM):', ScaleY(120));
  CampoHora := TNewEdit.Create(PaginaRespaldo);
  CampoHora.Parent := PaginaRespaldo.Surface;
  CampoHora.Top := ScaleY(140);
  CampoHora.Width := ScaleX(70);
  CampoHora.Text := GetPreviousData('Hora', '22:00');
end;

procedure InitializeWizard;
begin
  CrearPaginaConexion;
  CrearPaginasServidor;
end;

procedure RegisterPreviousData(PreviousDataKey: Integer);
begin
  SetPreviousData(PreviousDataKey, 'Servidor', CampoServidor.Text);
  SetPreviousData(PreviousDataKey, 'Respaldos', PaginaRespaldo.Values[0]);
  SetPreviousData(PreviousDataKey, 'Hora', CampoHora.Text);
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if PageID = PaginaConexion.ID then Result := WizardIsComponentSelected('servidor') or not WizardIsComponentSelected('cliente');
  { En una actualización los respaldos ya están programados. }
  if PageID = PaginaRespaldo.ID then Result := not WizardIsComponentSelected('servidor') or Configurado;
end;

function HoraValida(const Hora: string): Boolean;
var
  Horas, Minutos: Integer;
begin
  Horas := StrToIntDef(Copy(Hora, 1, 2), -1);
  Minutos := StrToIntDef(Copy(Hora, 4, 2), -1);
  Result := (Length(Hora) = 5) and (Copy(Hora, 3, 1) = ':') and (Horas >= 0) and (Horas <= 23) and (Minutos >= 0) and (Minutos <= 59);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Detalle: string;
begin
  Result := True;
  if CurPageID = PaginaConexion.ID then
  begin
    if Trim(CampoServidor.Text) = '' then
    begin
      MsgBox('Escribe la IP o el nombre del equipo servidor.', mbError, MB_OK);
      Result := False;
    end
    else if StrToIntDef(Trim(CampoPuerto.Text), 0) <= 0 then
    begin
      MsgBox('El puerto debe ser un número, normalmente {#PuertoServicio}.', mbError, MB_OK);
      Result := False;
    end
    else if not ConexionComprobada and not ProbarServidor(DireccionServidor, Detalle) then
      Result := MsgBox(Detalle + #13#10#13#10 + '¿Continuar de todos modos? Podrás corregir la dirección desde Mérito.', mbConfirmation, MB_YESNO) = IDYES;
  end
  else if CurPageID = PaginaRespaldo.ID then
  begin
    if not HoraValida(Trim(CampoHora.Text)) then
    begin
      MsgBox('Escribe la hora con el formato HH:MM, por ejemplo 22:00.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: string): string;
begin
  Result := MemoDirInfo + NewLine + NewLine + MemoTypeInfo + NewLine + NewLine + MemoComponentsInfo;
  if WizardIsComponentSelected('cliente') and not WizardIsComponentSelected('servidor') then
    Result := Result + NewLine + NewLine + 'Servidor:' + NewLine + Space + DireccionServidor;
  if WizardIsComponentSelected('servidor') then
  begin
    Result := Result + NewLine + NewLine + 'Servidor:' + NewLine + Space + 'Servicio de Mérito en el puerto {#PuertoServicio}, accesible solo desde la red local';
    if not Configurado then
      Result := Result + NewLine + Space + 'Respaldo diario a las ' + CampoHora.Text + ' en ' + PaginaRespaldo.Values[0];
  end;
  if MemoTasksInfo <> '' then Result := Result + NewLine + NewLine + MemoTasksInfo;
end;

{ Una actualización detiene los servicios para poder reemplazar sus archivos. }
function PrepareToInstall(var NeedsRestart: Boolean): string;
var
  Codigo: Integer;
begin
  Result := '';
  if WizardIsComponentSelected('servidor') then
    Exec('powershell.exe', '-NoProfile -Command "Stop-Service Merito,MeritoBD -Force -ErrorAction SilentlyContinue"', '', SW_HIDE, ewWaitUntilTerminated, Codigo);
end;

procedure EscribirCliente;
var
  Carpeta: string;
begin
  { Sin servidor indicado no se escribe una dirección rota: se conserva la anterior o Mérito la pedirá con Ctrl+Mayús+S. }
  if not WizardIsComponentSelected('servidor') and (Trim(CampoServidor.Text) = '') then Exit;
  Carpeta := ExpandConstant('{commonappdata}\Merito');
  ForceDirectories(Carpeta);
  SaveStringToFile(Carpeta + '\cliente.json', '{ "Servidor": "' + DireccionServidor + '" }', False);
end;

function CodigoConfiguracion: string;
var
  Contenido: AnsiString;
begin
  Result := '';
#ifdef Vista
  Result := 'ABCD-EFGH-JKLM-NPQR';
#else
  if LoadStringFromFile(ExpandConstant('{commonappdata}\Merito\privado\codigo-configuracion.txt'), Contenido) then Result := Trim(String(Contenido));
#endif
end;

procedure PrepararServidor;
var
  Argumentos, Texto: string;
  Codigo: Integer;
begin
  WizardForm.StatusLabel.Caption := 'Preparando la base de datos y el servicio de Mérito…';
  WizardForm.FilenameLabel.Caption := 'Esto puede tardar uno o dos minutos.';
  WizardForm.ProgressGauge.Style := npbstMarquee;
  Argumentos := '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}\herramientas\preparar-servidor.ps1') + '"' +
    ' -Programa "' + ExpandConstant('{app}') + '" -PuertoServicio {#PuertoServicio}';
  if not Configurado then
    Argumentos := Argumentos + ' -CarpetaRespaldos "' + PaginaRespaldo.Values[0] + '" -HoraRespaldo "' + Trim(CampoHora.Text) + '"';
  ServidorPreparado := Exec('powershell.exe', Argumentos, '', SW_HIDE, ewWaitUntilTerminated, Codigo) and (Codigo = 0);
  WizardForm.ProgressGauge.Style := npbstNormal;
  if not ServidorPreparado then
  begin
    Texto := 'No se pudo terminar de preparar el servidor.' + #13#10#13#10 + 'Los detalles están en ' +
      ExpandConstant('{commonappdata}\Merito\instalacion.log') + '. Puedes volver a ejecutar este instalador para reintentarlo.';
    { Aunque falle un paso posterior, el código ya puede servir: sin él no hay forma de crear la primera cuenta. }
    if CodigoConfiguracion <> '' then
      Texto := Texto + #13#10#13#10 + 'Para crear la cuenta de administración, abre Mérito, pulsa Configurar Mérito y escribe este código de configuración:' + #13#10#13#10 +
        '    ' + CodigoConfiguracion + #13#10#13#10 + 'Con Ctrl+C se copia este mensaje completo.';
    MsgBox(Texto, mbError, MB_OK);
  end;
end;

function DireccionesDeEsteEquipo: string;
var
  Archivo: string;
  Codigo: Integer;
  Contenido: AnsiString;
begin
  Result := '';
  Archivo := ExpandConstant('{tmp}\direcciones.txt');
  Exec('powershell.exe', '-NoProfile -Command "(Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike ''127.*'' -and $_.IPAddress -notlike ''169.254.*'' }).IPAddress -join '', '' | Set-Content -Encoding ASCII ''' + Archivo + '''"',
    '', SW_HIDE, ewWaitUntilTerminated, Codigo);
  if LoadStringFromFile(Archivo, Contenido) then Result := Trim(String(Contenido));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
#ifdef Vista
    { La vista previa no toca la configuración del equipo: solo muestra la pantalla final. }
    ServidorPreparado := True;
#else
    if WizardIsComponentSelected('cliente') then EscribirCliente;
    if WizardIsComponentSelected('servidor') then PrepararServidor;
#endif
  end;
end;

procedure AlCopiarCodigo(Sender: TObject);
begin
  CampoCodigo.SelectAll;
  SendMessage(CampoCodigo.Handle, $0301 { WM_COPY }, 0, 0);
  BotonCopiar.Caption := 'Copiado';
end;

procedure CurPageChanged(CurPageID: Integer);
var
  Codigo: string;
  Abajo: Integer;
  Nota: TNewStaticText;
begin
  if (CurPageID = wpFinished) and WizardIsComponentSelected('servidor') and ServidorPreparado then
  begin
    Codigo := CodigoConfiguracion;
    Abajo := WizardForm.FinishedLabel.Top;
    if Codigo <> '' then
    begin
      { El código va en una casilla de solo lectura para poder seleccionarlo y copiarlo. }
      WizardForm.FinishedLabel.Caption := 'Para crear la cuenta de administración, abre Mérito, pulsa Configurar Mérito y escribe este código de configuración:';
      WizardForm.FinishedLabel.AdjustHeight;
      CampoCodigo := TNewEdit.Create(WizardForm);
      CampoCodigo.Parent := WizardForm.FinishedLabel.Parent;
      CampoCodigo.Left := WizardForm.FinishedLabel.Left;
      CampoCodigo.Top := WizardForm.FinishedLabel.Top + WizardForm.FinishedLabel.Height + ScaleY(10);
      CampoCodigo.Width := ScaleX(200);
      CampoCodigo.ReadOnly := True;
      CampoCodigo.Text := Codigo;
      BotonCopiar := TNewButton.Create(WizardForm);
      BotonCopiar.Parent := CampoCodigo.Parent;
      BotonCopiar.Caption := 'Copiar';
      BotonCopiar.Left := CampoCodigo.Left + CampoCodigo.Width + ScaleX(8);
      BotonCopiar.Top := CampoCodigo.Top - ScaleY(1);
      BotonCopiar.Width := ScaleX(80);
      BotonCopiar.Height := CampoCodigo.Height + ScaleY(2);
      BotonCopiar.OnClick := @AlCopiarCodigo;
      Abajo := CampoCodigo.Top + CampoCodigo.Height + ScaleY(18);
      Nota := TNewStaticText.Create(WizardForm);
      Nota.Parent := CampoCodigo.Parent;
      Nota.Left := WizardForm.FinishedLabel.Left;
      Nota.Width := WizardForm.FinishedLabel.Width;
      Nota.WordWrap := True;
    end
    else
      Nota := WizardForm.FinishedLabel;
    Nota.Top := Abajo;
    Nota.Caption := 'En los equipos clientes, escribe como servidor:' + #13#10 + '    ' + DireccionesDeEsteEquipo + #13#10#13#10 +
      'El servidor arranca solo con el equipo y el respaldo diario ya está programado.';
    Nota.AdjustHeight;
    { La casilla de abrir Mérito baja para no tapar el texto, que es más largo que el de fábrica. }
    WizardForm.RunList.Top := Nota.Top + Nota.Height + ScaleY(16);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Argumentos: string;
  Codigo: Integer;
begin
  if (CurUninstallStep = usUninstall) and FileExists(ExpandConstant('{app}\herramientas\retirar-servidor.ps1')) then
  begin
    Argumentos := '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}\herramientas\retirar-servidor.ps1') + '" -Programa "' + ExpandConstant('{app}') + '"';
    if not UninstallSilent then
      if MsgBox('¿Eliminar también la base de datos y los respaldos de Mérito?' + #13#10#13#10 +
        'Si eliges No, se conservan en ' + ExpandConstant('{commonappdata}\Merito') + ' y una nueva instalación los recupera.',
        mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        Argumentos := Argumentos + ' -BorrarDatos';
    Exec('powershell.exe', Argumentos, '', SW_HIDE, ewWaitUntilTerminated, Codigo);
  end;
  { La dirección del cliente se va con el programa. RemoveDir solo quita la carpeta si quedó vacía: los datos de un servidor no se tocan. }
  if CurUninstallStep = usPostUninstall then
  begin
    DeleteFile(ExpandConstant('{commonappdata}\Merito\cliente.json'));
    RemoveDir(ExpandConstant('{commonappdata}\Merito'));
  end;
end;
