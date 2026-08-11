; Mohasabi — script d'installation Inno Setup

#ifndef Version
  #define Version "1.0.3"
#endif

#ifndef SourceStaging
  #define SourceStaging "..\release\staging"
#endif

#ifndef WebView2Installer
  #define WebView2Installer "..\.cache\webview2\MicrosoftEdgeWebView2RuntimeInstallerX64.exe"
#endif

#define WebView2InstallerExe "MicrosoftEdgeWebView2RuntimeInstallerX64.exe"

#define AppName "Mohasabi"
#define AppPublisher "Mohasabi"
#define AppExeName "Mohasabi.exe"

[Setup]
AppId={{4F2B7E1A-8C3D-4E9B-B5A7-1C2D3E4F5A6B}
AppName={#AppName}
AppVersion={#Version}
AppVerName={#AppName}
AppPublisher={#AppPublisher}
AppCopyright=Copyright (c) 2026 {#AppPublisher}

VersionInfoVersion={#Version}
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#Version}
VersionInfoDescription={#AppName} Setup

CloseApplications=force
RestartApplications=no

DefaultDirName={userpf}\Mohasabi
DefaultGroupName=Mohasabi
DisableProgramGroupPage=yes
PrivilegesRequired=lowest

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

OutputDir=..\dist\release
OutputBaseFilename=Mohasabi_setup

Compression=lzma2/ultra
SolidCompression=yes

UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\mohasabi.ico

; ===== الهوية الجديدة =====
SetupIconFile=..\assets\mohasabi.ico

WizardStyle=modern

; استعمال شعار Mohasabi الحقيقي
WizardImageFile=..\assets\installerbig.bmp
WizardSmallImageFile=..\assets\installersmall.bmp

DisableWelcomePage=no
MinVersion=10.0


[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"


[Tasks]
Name: "desktopicon"; Description: "Créer une icône sur le bureau"; GroupDescription: "Icônes supplémentaires:"; Flags: unchecked


[Files]
Source: "{#SourceStaging}\app\*"; DestDir: "{app}\app"; Flags: ignoreversion recursesubdirs createallsubdirs

Source: "{#SourceStaging}\Mohasabi.exe"; DestDir: "{app}"; Flags: ignoreversion

Source: "{#SourceStaging}\mohasabi.ico"; DestDir: "{app}"; Flags: ignoreversion

Source: "{#SourceStaging}\mohasabi.png"; DestDir: "{app}"; Flags: ignoreversion

Source: "{#SourceStaging}\launcher.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist

; Runtime Microsoft Edge WebView2 (requis par l'application embarquée)
Source: "{#WebView2Installer}"; DestDir: "{tmp}"; Flags: deleteafterinstall


[Icons]
Name: "{group}\Mohasabi"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\mohasabi.ico"; WorkingDir: "{app}"

Name: "{userdesktop}\Mohasabi"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\mohasabi.ico"; WorkingDir: "{app}"; Tasks: desktopicon


[Run]
Filename: "{tmp}\{#WebView2InstallerExe}"; Parameters: "/silent /install"; StatusMsg: "Installation de Microsoft Edge WebView2 Runtime..."; Flags: runhidden waituntilterminated skipifdoesntexist; Check: IsWebView2Missing

Filename: "{app}\{#AppExeName}"; Description: "Démarrer Mohasabi"; WorkingDir: "{app}"; Flags: nowait postinstall; Check: ShouldLaunchApp


[Code]
function IsWebView2Missing(): Boolean;
var
  S: string;
begin
  if RegQueryStringValue(HKLM32, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', S) then
  begin
    Result := False;
    Exit;
  end;

  if RegQueryStringValue(HKLM64, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', S) then
  begin
    Result := False;
    Exit;
  end;

  if RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', S) then
  begin
    Result := False;
    Exit;
  end;

  Result := True;
end;

// Évite de relancer Mohasabi en fin d'installation lorsque l'option
// /NOLAUNCH est passée (mise à jour programmée sans redémarrage automatique).
function ShouldLaunchApp(): Boolean;
var
  I: Integer;
begin
  Result := True;
  for I := 1 to ParamCount do
  begin
    if LowerCase(ParamStr(I)) = '/nolaunch' then
    begin
      Result := False;
      Exit;
    end;
  end;
end;