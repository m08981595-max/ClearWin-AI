; ClearWin v2.7.7 - WebView2 UI
; Build: ISCC.exe installer.iss
;
; Packaging layout:
;   {app}\ClearWin.exe                      (compiled, ~1628 KB, x64)
;   {app}\app.ico
;   {app}\Microsoft.Web.WebView2.{Core,Wpf}.dll
;   {app}\runtimes\win-x64\native\WebView2Loader.dll
;   {app}\Resources\WebUI\*                 (index.html, app.js, style.css)
;   {app}\Resources\tweaks\<name>\{apply,undo}.ps1 + meta.json
;   {app}\runtime-check.ps1
; The app's manifest requests requireAdministrator, so the standard
; Program Files install location is writable at runtime by design.

#define MyAppName "ClearWin"
#define MyAppVersion "2.7.7"
#define MyAppPublisher "Abdullah Mohamed"
#define MyAppExeName "ClearWin.exe"
#define MyAppId "{{8D2F7C31-6A5B-4C89-9E37-C1B9A7D6E2F4}}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppCopyright=Copyright (c) 2026 {#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=EULA.txt
OutputDir=release\v2.7.7
OutputBaseFilename=ClearWin-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=Resources\app.ico
UninstallDisplayIcon={app}\ClearWin.exe
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
DisableWelcomePage=no
ShowLanguageDialog=no
RestartIfNeededByRun=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Core executable + WebView2 runtime + app icon
Source: "bin\ClearWin.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\ClearWin.AdminHelper.exe"; DestDir: "{app}"; Flags: ignoreversion
#if DirExists(AddBackslash(SourcePath) + "bin\python")
Source: "bin\python\*"; DestDir: "{app}\python"; Flags: ignoreversion recursesubdirs createallsubdirs
#endif
Source: "bin\app.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Microsoft.Web.WebView2.Core.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Microsoft.Web.WebView2.Wpf.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\runtimes\win-x64\native\WebView2Loader.dll"; DestDir: "{app}\runtimes\win-x64\native"; Flags: ignoreversion
; Web UI (WebView2 page)
Source: "bin\Resources\WebUI\*"; DestDir: "{app}\Resources\WebUI"; Flags: ignoreversion recursesubdirs createallsubdirs
; Tweak engine: apply.ps1 + undo.ps1 + meta.json per tweak
Source: "bin\Resources\tweaks\*"; DestDir: "{app}\Resources\tweaks"; Flags: ignoreversion recursesubdirs createallsubdirs
; Legal + post-install runtime check
Source: "LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "EULA.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "runtime-check.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"; IconFilename: "{app}\app.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"
Name: "{autodesktop}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"; IconFilename: "{app}\app.ico"

[Run]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\runtime-check.ps1"""; StatusMsg: "Checking Microsoft Edge WebView2 Runtime..."; Flags: waituntilterminated
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent; Check: IsWebView2Installed

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Resources"

[Code]
function IsWebView2Installed(): Boolean;
var
  Version: string;
begin
  Version := '';
  Result := RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) and (Version <> '');
  if not Result then begin
    Version := '';
    Result := RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) and (Version <> '');
  end;
  if not Result then begin
    Version := '';
    Result := RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) and (Version <> '');
  end;
end;

function InitializeSetup(): Boolean;
begin
  if not IsWebView2Installed() then
  begin
    if MsgBox('Microsoft Edge WebView2 Runtime غير مثبت. سيتم تنزيله تلقائيا بعد التثبيت من:' #13#10 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' #13#10 #13#10 'هل تريد المتابعة؟', mbConfirmation, MB_YESNO) = IDNO then
      Result := False
    else
      Result := True;
  end else
    Result := True;
end;
