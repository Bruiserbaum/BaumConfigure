#define AppName      "BaumConfigure"
#define AppVersion   "1.5.8"
#define AppVersionFull "1.5.8"
#define AppPublisher "Bruiserbaum"
#define AppExeName   "BaumConfigure.exe"
#define PublishDir   "..\BaumConfigureGUI\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={{D4E5F6A7-B8C9-0DA1-B2C3-D4E5F6A7B8C9}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/Bruiserbaum/BaumConfigure
AppSupportURL=https://github.com/Bruiserbaum/BaumConfigure
AppUpdatesURL=https://github.com/Bruiserbaum/BaumConfigure/releases
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
OutputDir=output
OutputBaseFilename=BaumConfigure-Setup-{#AppVersionFull}
SetupIconFile=..\BaumConfigureGUI\Resources\app.ico
UninstallDisplayIcon={app}\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
CloseApplications=yes
MinVersion=10.0.19041
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion restartreplace uninsrestartdelete
Source: "{#PublishDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\BaumConfigureGUI\Resources\app.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; Silent update path: auto-launch after install (postinstall is skipped in /VERYSILENT mode)
Filename: "{app}\{#AppExeName}"; Flags: nowait runascurrentuser; Check: WizardSilent
; Interactive install path: show "Launch" checkbox at end
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{app}\*.json"
Type: dirifempty; Name: "{app}"
