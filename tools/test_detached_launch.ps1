# tools/test_detached_launch.ps1
$exe = "D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control\bin\ClearWin.exe"
Get-Process -Name "ClearWin" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

Write-Host "Launching ClearWin via cmd start..."
cmd.exe /c start "" "$exe"
Start-Sleep -Seconds 3

for ($i = 1; $i -le 6; $i++) {
    $p = Get-Process -Name "ClearWin" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p) {
        Write-Host "[$i] ClearWin is healthy and running! PID: $($p.Id), WorkingSet: $([math]::Round($p.WorkingSet64 / 1MB, 2)) MB, Responding: $($p.Responding)"
    } else {
        Write-Warning "[$i] ClearWin not found!"
    }
    Start-Sleep -Seconds 2
}
