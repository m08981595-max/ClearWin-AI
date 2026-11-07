# tools/track_launch.ps1
$exe = "D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control\bin\ClearWin.exe"
$bin = "D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control\bin"

Get-Process -Name ClearWin -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

$shell = New-Object -ComObject "Shell.Application"
$shell.ShellExecute($exe, "", $bin, "runas", 1)

Start-Sleep -Milliseconds 1500
$p = Get-Process -Name ClearWin -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) {
    Write-Host "Process not started or UAC pending"
    exit
}

Write-Host "Tracking PID $($p.Id)..."
for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 500
    if ($p.HasExited) {
        Write-Host "Process exited at step $i with exit code $($p.ExitCode)!"
        break
    } else {
        $p.Refresh()
        Write-Host "Step $($i): Responding=$($p.Responding), Handle=$($p.MainWindowHandle), WS=$([math]::Round($p.WorkingSet64/1MB,1))MB"
    }
}
