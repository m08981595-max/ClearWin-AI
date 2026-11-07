$p = Start-Process bin\ClearWin.exe -PassThru
Write-Host "Started ClearWin PID: $($p.Id)"
Start-Sleep -Seconds 2
& .\tools\get_windows_for_pid.ps1 -TargetPid $p.Id
Start-Sleep -Seconds 2
& .\tools\get_windows_for_pid.ps1 -TargetPid $p.Id
