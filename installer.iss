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
Source: "bin\Release\net8.0-windows\win-x64\publish\Soldado.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Soldado"; Filename: "{app}\Soldado.exe"; IconFilename: "{app}\icon.ico"
Name: "{group}\Desinstalar Soldado"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Soldado"; Filename: "{app}\Soldado.exe"; IconFilename: "{app}\icon.ico"; Tasks: desktopicon
Name: "{userstartup}\Soldado"; Filename: "{app}\Soldado.exe"; Tasks: startupicon

[Run]
Filename: "{app}\Soldado.exe"; Description: "Ejecutar Soldado"; Flags: nowait postinstall skipifsilent
