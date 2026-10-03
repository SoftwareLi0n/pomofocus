[Setup]
AppName=Soldado
AppVersion=1.0.0
AppPublisher=Software Lion
AppPublisherURL=https://softwarelion.pe
DefaultDirName={autopf}\Soldado
DefaultGroupName=Soldado
OutputDir=installer_output
OutputBaseFilename=Soldado_Setup_1.0.0
SetupIconFile=icon.ico
UninstallDisplayIcon={app}\Soldado.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
CloseApplications=force

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"

[Files]
Source: "bin\Release\net8.0-windows\win-x64\publish\Soldado.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
; Hide from Apps & Features / Programs and Features
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\Soldado_is1"; ValueType: dword; ValueName: "SystemComponent"; ValueData: "1"; Flags: uninsdeletevalue

[Icons]
Name: "{group}\Soldado"; Filename: "{app}\Soldado.exe"; IconFilename: "{app}\icon.ico"
Name: "{autodesktop}\Soldado"; Filename: "{app}\Soldado.exe"; IconFilename: "{app}\icon.ico"; Tasks: desktopicon

[Run]
; Create scheduled tasks for auto-start at logon
Filename: "schtasks"; Parameters: "/create /tn ""SoldadoAutoStart"" /tr """"""{app}\Soldado.exe"""""" /sc onlogon /f"; Flags: runhidden
Filename: "schtasks"; Parameters: "/create /tn ""SoldadoWatchdogAutoStart"" /tr """"""{app}\SoldadoWatchdog.exe"""""" /sc onlogon /f"; Flags: runhidden
; Launch after install
Filename: "{app}\Soldado.exe"; Description: "Ejecutar Soldado"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Remove scheduled tasks on uninstall
Filename: "schtasks"; Parameters: "/delete /tn ""SoldadoAutoStart"" /f"; Flags: runhidden
Filename: "schtasks"; Parameters: "/delete /tn ""SoldadoWatchdogAutoStart"" /f"; Flags: runhidden

[Code]
procedure KillProcess(ProcessName: String);
var
  ResultCode: Integer;
begin
  Exec('taskkill', '/f /im ' + ProcessName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  I: Integer;
begin
  // Kill all related processes multiple times to handle respawning
  for I := 1 to 3 do
  begin
    KillProcess('SysTaskHost.exe');
    KillProcess('SoldadoWatchdog.exe');
    KillProcess('Soldado.exe');
    Sleep(1500);
  end;
  Result := '';
end;
