@echo off
setlocal EnableExtensions
set "SRC=%~dp0Uninstall-ClearWin.ps1"
set "TMP_PS=%TEMP%\ClearWin-Uninstall-%RANDOM%-%RANDOM%.ps1"
if exist "%SRC%" (
    copy /Y "%SRC%" "%TMP_PS%" >nul 2>&1
    start "" powershell.exe -Sta -NoLogo -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%TMP_PS%" -InstallRoot "%~dp0" -Gui
)
exit /b 0
