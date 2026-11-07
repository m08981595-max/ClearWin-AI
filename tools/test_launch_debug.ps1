# tools/test_launch_debug.ps1
$exe = "D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control\bin\ClearWin.exe"
$p = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 1
Write-Host "Process ID: $($p.Id), HasExited: $($p.HasExited)"
if ($p.HasExited) {
    Write-Host "ExitCode: $($p.ExitCode)"
} else {
    Write-Host "MainWindowHandle: $($p.MainWindowHandle)"
    Start-Sleep -Seconds 2
    Write-Host "Still running: $(-not $p.HasExited)"
}
