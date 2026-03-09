[Setup]
AppName=Soldado
AppVersion=1.0.0
AppPublisher=SoftwareLion
DefaultDirName={autopf}\Soldado
DefaultGroupName=Soldado
OutputDir=installer_output
OutputBaseFilename=Soldado_Setup
SetupIconFile=icon.ico
UninstallDisplayIcon={app}\Soldado.exe
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
DisableProgramGroupPage=yes

[Files]
Source: "publish\Soldado.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Soldado"; Filename: "{app}\Soldado.exe"
Name: "{autodesktop}\Soldado"; Filename: "{app}\Soldado.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"

[Run]
Filename: "{app}\Soldado.exe"; Description: "Launch Soldado"; Flags: nowait postinstall skipifsilent
