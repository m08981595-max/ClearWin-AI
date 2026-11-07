; ClearWin v2.7.3 - Offline WebView2 Standalone Installer
; Build: ISCC.exe installer-2.7.3-offline.iss
; Output: release\v2.7.3\ClearWin-Setup-2.7.3-Offline.exe

#define MyAppName "ClearWin"
#define MyAppVersion "2.7.3"
#define MyAppPublisher "Abdullah Mohamed"
#define MyAppExeName "ClearWin.exe"
#define MyAppId "{{8D2F7C31-6A5B-4C89-9E37-C1B9A7D6E2F4}}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion} (Offline Edition)
AppPublisher={#MyAppPublisher}
AppCopyright=Copyright (c) 2026 {#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=EULA.txt
OutputDir=release\v2.7.3
OutputBaseFilename=ClearWin-Setup-{#MyAppVersion}-Offline
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
; Core executable + WebView2 runtime managed dlls + app icon
Source: "bin\ClearWin.exe"; DestDir: "{app}"; Flags: ignoreversion
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
; Legal files
Source: "LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "EULA.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "runtime-check.ps1"; DestDir: "{app}"; Flags: ignoreversion
; Bundled Official Microsoft Evergreen Standalone Installer (x64)
; Extracted only if WebView2 is missing, deleted upon setup completion
Source: "tools\webview2-offline\MicrosoftEdgeWebView2RuntimeInstallerX64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall ignoreversion; Check: not IsWebView2Installed

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"

[Run]
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

function GetWebView2VersionString(): string;
var
  Version: string;
begin
  Version := '';
  if RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) and (Version <> '') then
    Result := Version
  else if RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) and (Version <> '') then
    Result := Version
  else if RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) and (Version <> '') then
    Result := Version
  else
    Result := 'Not Installed';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  InstallerPath: string;
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    if IsWebView2Installed() then
    begin
      Log('Microsoft Edge WebView2 Runtime is already installed: ' + GetWebView2VersionString());
    end
    else
    begin
      InstallerPath := ExpandConstant('{tmp}\MicrosoftEdgeWebView2RuntimeInstallerX64.exe');
      Log('Checking bundled offline installer payload at: ' + InstallerPath);
      if not FileExists(InstallerPath) then
      begin
        Log('ERROR: Bundled WebView2 installer payload not found at ' + InstallerPath);
        MsgBox('Microsoft Edge WebView2 Runtime installer was not found in temporary files.' + #13#10 +
               'Please install WebView2 manually.', mbError, MB_OK);
        Exit;
      end;

      Log('Executing bundled WebView2 standalone installer: ' + InstallerPath + ' /silent /install');
      if Exec(InstallerPath, '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      begin
        Log(Format('WebView2 installer completed with exit code: %d', [ResultCode]));
        if (ResultCode = 0) or (ResultCode = 3010) then
        begin
          Log('Microsoft Edge WebView2 Runtime successfully installed. Validating registry...');
          if IsWebView2Installed() then
            Log('Registry validation succeeded: ' + GetWebView2VersionString())
          else
            Log('WARNING: Installer reported success but registry pv key is not yet readable.');
        end
        else
        begin
          Log(Format('ERROR: WebView2 Runtime installer failed with exit code: %d', [ResultCode]));
          MsgBox(Format('Microsoft Edge WebView2 Runtime installation failed (Exit Code: %d).' + #13#10 +
                        'ClearWin may not display correctly until WebView2 is installed.', [ResultCode]), mbError, MB_OK);
        end;
      end
      else
      begin
        Log('ERROR: Failed to launch WebView2 standalone installer process.');
        MsgBox('Failed to launch the Microsoft Edge WebView2 installer process.', mbError, MB_OK);
      end;
    end;
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
end;
