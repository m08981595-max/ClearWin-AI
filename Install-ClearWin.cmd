@echo off
setlocal EnableExtensions
start "" powershell.exe -Sta -NoLogo -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0install.ps1" -Gui
exit /b 0
