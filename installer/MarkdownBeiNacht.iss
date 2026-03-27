#define AppName "Markdown bei Nacht"
#define AppExeName "MarkdownBeiNacht.exe"
#define AppVersion "1.1.1"
#define AppPublisher "Markdown bei Nacht"
#define PublishDir "..\artifacts\publish\win-x64"
#define BootstrapperSource "dependencies\MicrosoftEdgeWebView2Setup.exe"
#define WebView2RuntimeId "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"

[Setup]
AppId={{9F79B0A1-CFEA-48E0-8C47-F8CB5B3E0CC1}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
SetupIconFile=..\src\MarkdownBeiNacht\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir=..\artifacts\installer
OutputBaseFilename=MarkdownBeiNacht-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
ChangesAssociations=yes
VersionInfoVersion={#AppVersion}
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#BootstrapperSource}"; DestDir: "{tmp}"; DestName: "MicrosoftEdgeWebView2Setup.exe"; Flags: deleteafterinstall ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{#AppName} User Guide"; Filename: "{app}\{#AppExeName}"; Parameters: """{app}\README.md"""; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Classes\Applications\{#AppExeName}"; ValueType: none; Flags: uninsdeletekeyifempty
Root: HKCU; Subkey: "Software\Classes\Applications\{#AppExeName}\shell\open\command"; ValueType: string; ValueData: """{app}\{#AppExeName}"" ""%1"""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\{#AppExeName}\SupportedTypes"; ValueType: none; Flags: uninsdeletekeyifempty
Root: HKCU; Subkey: "Software\Classes\Applications\{#AppExeName}\SupportedTypes"; ValueType: string; ValueName: ".md"; ValueData: ""; Flags: uninsdeletekeyifempty
Root: HKCU; Subkey: "Software\Classes\Applications\{#AppExeName}\SupportedTypes"; ValueType: string; ValueName: ".markdown"; ValueData: ""; Flags: uninsdeletekeyifempty
Root: HKCU; Subkey: "Software\Classes\Applications\{#AppExeName}\SupportedTypes"; ValueType: string; ValueName: ".mdown"; ValueData: ""; Flags: uninsdeletekeyifempty
Root: HKCU; Subkey: "Software\Classes\Applications\{#AppExeName}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#AppName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\MarkdownBeiNacht.md"; ValueType: none; Flags: uninsdeletekeyifempty
Root: HKCU; Subkey: "Software\Classes\MarkdownBeiNacht.md"; ValueType: string; ValueData: "Markdown bei Nacht Markdown"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\MarkdownBeiNacht.md\DefaultIcon"; ValueType: string; ValueData: "{app}\{#AppExeName},0"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\MarkdownBeiNacht.md\shell\open\command"; ValueType: string; ValueData: """{app}\{#AppExeName}"" ""%1"""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.md\OpenWithProgids"; ValueType: string; ValueName: "MarkdownBeiNacht.md"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.markdown\OpenWithProgids"; ValueType: string; ValueName: "MarkdownBeiNacht.md"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.mdown\OpenWithProgids"; ValueType: string; ValueName: "MarkdownBeiNacht.md"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#AppExeName}"; ValueType: string; ValueData: "{app}\{#AppExeName}"; Flags: uninsdeletekey

[Run]
Filename: "{tmp}\MicrosoftEdgeWebView2Setup.exe"; Parameters: "/silent /install"; Flags: runhidden waituntilterminated skipifdoesntexist; StatusMsg: "Installing Microsoft Edge WebView2 Runtime..."; Check: not IsWebView2Installed
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
function IsValidRuntimeVersion(const VersionValue: string): Boolean;
begin
  Result := (VersionValue <> '') and (VersionValue <> '0.0.0.0');
end;

function IsWebView2Installed(): Boolean;
var
  VersionValue: string;
begin
  Result := False;

  if IsWin64 then
  begin
    if RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{#WebView2RuntimeId}', 'pv', VersionValue) and IsValidRuntimeVersion(VersionValue) then
    begin
      Result := True;
      exit;
    end;
  end
  else
  begin
    if RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{#WebView2RuntimeId}', 'pv', VersionValue) and IsValidRuntimeVersion(VersionValue) then
    begin
      Result := True;
      exit;
    end;
  end;

  if RegQueryStringValue(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\{#WebView2RuntimeId}', 'pv', VersionValue) and IsValidRuntimeVersion(VersionValue) then
  begin
    Result := True;
  end;
end;
