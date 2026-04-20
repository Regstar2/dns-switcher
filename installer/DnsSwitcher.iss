#ifndef AppVersion
#define AppVersion "1.4.0"
#endif

#ifndef Runtime
#define Runtime "win-x64"
#endif

#define AppName "DnsSwitcher"
#define Publisher "Regstar2"
#define SourceDir "..\artifacts\release\v" + AppVersion + "\DnsSwitcher-" + AppVersion + "-" + Runtime

[Setup]
AppId={{7E9A4D5D-8160-4B7B-8E2B-0C6E98B8D9D2}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#Publisher}
DefaultDirName={autopf}\DnsSwitcher
DefaultGroupName=DnsSwitcher
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer\v{#AppVersion}
OutputBaseFilename=DnsSwitcher-{#AppVersion}-{#Runtime}-setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\ui\DnsSwitcher.Ui.exe
SetupLogging=yes

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Dirs]
Name: "{app}\data"; Permissions: users-modify
Name: "{app}\data\config"; Permissions: users-modify
Name: "{app}\data\logs"; Permissions: users-modify

[Icons]
Name: "{autoprograms}\DnsSwitcher"; Filename: "{app}\ui\DnsSwitcher.Ui.exe"; WorkingDir: "{app}"
Name: "{autoprograms}\DnsSwitcher Tray"; Filename: "{app}\tray\DnsSwitcher.Tray.exe"; WorkingDir: "{app}"
Name: "{autoprograms}\DnsSwitcher CLI"; Filename: "{app}\cli\DnsSwitcher.Cli.exe"; WorkingDir: "{app}\cli"
Name: "{autodesktop}\DnsSwitcher"; Filename: "{app}\ui\DnsSwitcher.Ui.exe"; WorkingDir: "{app}"

[Run]
Filename: "{app}\cli\DnsSwitcher.Cli.exe"; Parameters: "service reinstall"; WorkingDir: "{app}"; Flags: runhidden waituntilterminated; StatusMsg: "Installing DnsSwitcher Agent service..."
Filename: "{app}\tray\DnsSwitcher.Tray.exe"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent; Description: "Start DnsSwitcher Tray"

[UninstallRun]
Filename: "{app}\cli\DnsSwitcher.Cli.exe"; Parameters: "service stop"; WorkingDir: "{app}"; Flags: runhidden waituntilterminated; RunOnceId: "StopDnsSwitcherAgent"
Filename: "{app}\cli\DnsSwitcher.Cli.exe"; Parameters: "service uninstall"; WorkingDir: "{app}"; Flags: runhidden waituntilterminated; RunOnceId: "UninstallDnsSwitcherAgent"
