# tools/launch_shell_app.ps1
$exe = "D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control\bin\ClearWin.exe"
$bin = "D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control\bin"

Write-Host "Invoking Shell.Application to launch ClearWin..."
$shell = New-Object -ComObject "Shell.Application"
# Operation "runas" triggers elevated execution directly
$shell.ShellExecute($exe, "", $bin, "runas", 1)

Start-Sleep -Seconds 5

$procs = Get-Process -Name ClearWin -ErrorAction SilentlyContinue
if ($procs) {
    foreach ($p in $procs) {
        Write-Host "ClearWin active! PID: $($p.Id), MainWindowHandle: $($p.MainWindowHandle), Title: '$($p.MainWindowTitle)'"
    }
} else {
    Write-Warning "ClearWin process not found. Waiting for UAC prompt approval..."
}
