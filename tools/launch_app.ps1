# tools/launch_app.ps1
$exe = "D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control\bin\ClearWin.exe"
Get-Process -Name "ClearWin" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300
Start-Process -FilePath $exe
Start-Sleep -Seconds 2
$p = Get-Process -Name "ClearWin" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($p) {
    Write-Host "ClearWin 2.7.4 launched successfully! PID: $($p.Id), MainWindowHandle: $($p.MainWindowHandle)"
} else {
    Write-Error "Failed to start ClearWin"
}
