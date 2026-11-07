# tools/launch_elevated.ps1
$exe = "D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control\bin\ClearWin.exe"
$bin = "D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control\bin"
Write-Host "Requesting elevated launch of ClearWin 2.7.3..."
try {
    $proc = Start-Process -FilePath $exe -WorkingDirectory $bin -Verb RunAs -PassThru
    Write-Host "Elevated process launch initiated (PID: $($proc.Id))"
} catch {
    Write-Error "Failed to launch elevated: $($_.Exception.Message)"
}
