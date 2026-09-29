; Inno Setup Script for Medistock - Pharmacy ERP & POS
; Full Offline-First Standalone Setup with Auto-Desktop Shortcut & Uninstaller

#define MyAppName "Medistock"
#define MyAppVersion "1.0.5"
#define MyAppPublisher "Medistock Health Technologies"
#define MyAppURL "https://medistock.local"
#define MyAppExeName "Medistock.Desktop.exe"
#define SourceDistDir "D:\Projects\Medistock-offlinefirst\dist\Medistock-Release-win-x64"
#define AssetsDir "D:\Projects\Medistock-offlinefirst\src\Clients\Medistock.Desktop\Assets"

[Setup]
AppId={{D37E8F91-C8A5-4E76-A8E2-2B3D6A0E4F90}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=D:\Projects\Medistock-offlinefirst\dist\Installer
OutputBaseFilename=Medistock-Setup-v1.0.5
SetupIconFile={#AssetsDir}\AppIcon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
CloseApplicationsFilter=Medistock.Desktop.exe
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Copy all app files — but EXCLUDE the WebView2 user-data cache folder entirely.
; The *.WebView2 folder is a RUNTIME cache created by WebView2 on first launch;
; it does NOT belong in the installer and causes "Could not find a part of the path"
; errors on machines where the nested EBWebView sub-dirs don't pre-exist.
; WebView2 regenerates this folder automatically on next app start.
Source: "{#SourceDistDir}\*"; DestDir: "{app}"; \
  Flags: ignoreversion recursesubdirs createallsubdirs; \
  Excludes: "*.WebView2,*.WebView2\*"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\AppIcon.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\AppIcon.ico"; Tasks: desktopicon

[Run]
; Register & start the silent updater Windows Service if exists and installer has admin privileges
Filename: "sc.exe"; Parameters: "create MedistockUpdater binPath= ""{app}\Medistock.UpdaterService.exe"" start= auto DisplayName= ""Medistock Updater Service"""; Flags: runhidden; Check: IsAdminLoggedOn
Filename: "sc.exe"; Parameters: "start MedistockUpdater"; Flags: runhidden; Check: IsAdminLoggedOn
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "sc.exe"; Parameters: "stop MedistockUpdater"; Flags: runhidden; Check: IsAdminLoggedOn
Filename: "sc.exe"; Parameters: "delete MedistockUpdater"; Flags: runhidden; Check: IsAdminLoggedOn

[Code]
// Pre-install: forcefully delete the WebView2 EBWebView cache from the target
// install directory if it already exists, so the installer doesn't fail trying
// to write into locked or missing nested directories.
procedure CurStepChanged(CurStep: TSetupStep);
var
  WebView2CacheDir: String;
begin
  if CurStep = ssInstall then
  begin
    WebView2CacheDir := ExpandConstant('{app}\Medistock.Desktop.exe.WebView2\EBWebView');
    if DirExists(WebView2CacheDir) then
    begin
      DelTree(WebView2CacheDir, True, True, True);
    end;
  end;
end;
