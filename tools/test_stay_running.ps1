# tools/test_stay_running.ps1
$exe = "D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control\bin\ClearWin.exe"
Get-Process -Name "ClearWin" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

$proc = Start-Process -FilePath $exe -PassThru
Write-Host "Started ClearWin PID: $($proc.Id)"

for ($i = 1; $i -le 10; $i++) {
    Start-Sleep -Seconds 2
    $p = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
    if ($p) {
        Write-Host "[$i] ClearWin PID $($p.Id) is alive! WorkingSet: $([math]::Round($p.WorkingSet64 / 1MB, 2)) MB, Responding: $($p.Responding), Threads: $($p.Threads.Count)"
    } else {
        Write-Warning "[$i] Process $($proc.Id) has exited! ExitCode: $($proc.ExitCode)"
        break
    }
}
