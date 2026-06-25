; Inno Setup script for mixscope
; Build the payload first:  ./build-release.ps1
; Then compile this with Inno Setup (ISCC.exe installer.iss) to produce dist\mixscope-Setup-*.exe
;
; Installs per-user (no admin), adds a Start Menu entry, optional auto-start, and an uninstaller.

#define MyAppName "mixscope"
#define MyAppVersion "0.2.1"
#define MyAppPublisher "Martin"
#define MyAppExeName "mixscope.exe"

[Setup]
AppId={{C4E2A7F1-9D3B-4A6E-B8C2-1F5D7E0A9B34}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\mixscope
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=mixscope.ico
OutputDir=dist
OutputBaseFilename=mixscope-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest

[Files]
Source: "dist\mixscope\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Tasks]
Name: "autostart"; Description: "Start automatically with Windows (recommended)"; GroupDescription: "Startup:"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
    ValueName: "mixscope"; ValueData: """{app}\{#MyAppExeName}"""; \
    Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName} now"; Flags: nowait postinstall skipifsilent
