#define AppName "Wynil"
#define AppVersion "1.0.0"
#define AppPublisher "Wynil contributors"
#define AppExeName "Wynil.App.exe"
#define ProjectRoot ".."

[Setup]
AppId={{4F36BA45-D4CD-43C0-9B17-C765D1DBDC91}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputDir={#ProjectRoot}\artifacts\installer
OutputBaseFilename=Wynil-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
Name: "startup"; Description: "Start Wynil when I sign in"; GroupDescription: "Startup:"; Flags: unchecked
Name: "keepsettings"; Description: "Keep my settings and artwork cache when uninstalling"; GroupDescription: "User data:"; Flags: checkedonce

[Files]
Source: "{#ProjectRoot}\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#ProjectRoot}\browser-extension\*"; DestDir: "{app}\browser-extension"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#ProjectRoot}\lively-package\*"; DestDir: "{app}\lively-package"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: not IsWebView2Installed

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Wynil"; ValueData: """{app}\{#AppExeName}"" --background"; Flags: uninsdeletevalue; Tasks: startup
Root: HKCU; Subkey: "Software\Wynil"; ValueType: dword; ValueName: "KeepSettings"; ValueData: "1"; Tasks: keepsettings
Root: HKCU; Subkey: "Software\Wynil"; ValueType: dword; ValueName: "KeepSettings"; ValueData: "0"; Check: not WizardIsTaskSelected('keepsettings')

[Run]
Filename: "{tmp}\MicrosoftEdgeWebview2Setup.exe"; Parameters: "/silent /install"; StatusMsg: "Installing Microsoft WebView2 Runtime..."; Flags: waituntilterminated; Check: not IsWebView2Installed
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\Wynil"; Check: not KeepSettings

[Code]
function IsWebView2Installed: Boolean;
var
  Version: String;
begin
  Result := RegQueryStringValue(HKLM64, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F1E7E954-3BD5-4B25-9C9B-1D4D581D3314}', 'pv', Version) or
            RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F1E7E954-3BD5-4B25-9C9B-1D4D581D3314}', 'pv', Version);
end;

function KeepSettings: Boolean;
var
  Value: Cardinal;
begin
  Result := RegQueryDWordValue(HKCU, 'Software\Wynil', 'KeepSettings', Value) and (Value = 1);
end;
