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

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"
Name: "startupicon"; Description: "Iniciar Soldado con Windows"; GroupDescription: "Opciones adicionales:";

[Files]
Source: "bin\Release\net8.0-windows\publish\Soldado.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net8.0-windows\publish\SoldadoWatchdog.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net8.0-windows\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "SoldadoWatchdog"; ValueData: """{app}\SoldadoWatchdog.exe"""; Flags: uninsdeletevalue

[Icons]
Name: "{group}\Soldado"; Filename: "{app}\Soldado.exe"; IconFilename: "{app}\icon.ico"
Name: "{group}\Desinstalar Soldado"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Soldado"; Filename: "{app}\Soldado.exe"; IconFilename: "{app}\icon.ico"; Tasks: desktopicon
Name: "{userstartup}\Soldado"; Filename: "{app}\Soldado.exe"; Tasks: startupicon

[Run]
Filename: "{app}\SoldadoWatchdog.exe"; Description: "Iniciar Watchdog"; Flags: nowait postinstall skipifsilent
Filename: "{app}\Soldado.exe"; Description: "Ejecutar Soldado"; Flags: nowait postinstall skipifsilent
