@echo off
echo [1/3] Importing registry buffer as Administrator...
reg.exe import "C:\Users\maher\Desktop\تفعيل_خزان_الفريمات_AMD_DirectX.reg"
echo [2/3] Restarting Windows Explorer Shell...
taskkill /f /im explorer.exe >nul 2>&1
timeout /t 1 /nobreak >nul
start explorer.exe
echo [3/3] Refreshing Desktop Wallpapers and Icons...
RUNDLL32.EXE user32.dll,UpdatePerUserSystemParameters
echo Done!
