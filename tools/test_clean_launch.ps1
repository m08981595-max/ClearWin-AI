# tools/test_clean_launch.ps1
$exe = "D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control\bin\ClearWin.exe"
Get-Process -Name "ClearWin" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

$p = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 3

$alive = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
if ($alive) {
    Write-Host "SUCCESS: ClearWin is actively running! PID: $($alive.Id), WorkingSet: $([math]::Round($alive.WorkingSet64 / 1MB, 2)) MB"
    Write-Host "MainWindowTitle: '$($alive.MainWindowTitle)'"
} else {
    Write-Host "Looking for any ClearWin process..."
    $any = Get-Process -Name "ClearWin" -ErrorAction SilentlyContinue
    if ($any) {
        Write-Host "ClearWin is running under PID $($any.Id) (elevated child)"
    } else {
        Write-Error "No ClearWin process running!"
    }
}
