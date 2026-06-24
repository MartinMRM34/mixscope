; Inno Setup script for Audio Channel Overlay
; Build the payload first:  ./build-release.ps1
; Then compile this with Inno Setup (ISCC.exe installer.iss) to produce dist\AudioChannelOverlay-Setup-*.exe
;
; Installs per-user (no admin), adds a Start Menu entry, optional auto-start, and an uninstaller.

#define MyAppName "Audio Channel Overlay"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "Martin"
#define MyAppExeName "AudioChannelOverlay.exe"

[Setup]
AppId={{8F2A6C14-3B9E-4D77-A1E2-9C5B7F0A2D31}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\AudioChannelOverlay
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=dist
OutputBaseFilename=AudioChannelOverlay-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest

[Files]
Source: "dist\AudioChannelOverlay\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Tasks]
Name: "autostart"; Description: "Start automatically with Windows (recommended)"; GroupDescription: "Startup:"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
    ValueName: "AudioChannelOverlay"; ValueData: """{app}\{#MyAppExeName}"""; \
    Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName} now"; Flags: nowait postinstall skipifsilent
