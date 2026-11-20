#define AppName "RecordRun"
#define AppVersion "1.0.0"

[Setup]
AppId={{B6F1D2A4-7C3E-4F55-9A1B-3E8D2C6A9F10}
AppName={#AppName}
AppVersion={#AppVersion}
AppCopyright=Copyright (C) 2026 FaveryX
AppPublisher=FaveryX
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
OutputDir=installer
OutputBaseFilename=RecordRun-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
WizardStyle=modern
UninstallDisplayIcon={app}\RecordRun.exe

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
Source: "exe\RecordRun.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\RecordRun.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\RecordRun.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\RecordRun.exe"; Description: "Launch RecordRun"; Flags: nowait postinstall skipifsilent
