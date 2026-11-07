$p = Get-Process -Id 5432 -ErrorAction SilentlyContinue
if ($p) {
    Write-Host "Process PID: $($p.Id)"
    Write-Host "Threads count: $($p.Threads.Count)"
    $p.Threads | Select-Object Id, ThreadState, WaitReason, StartTime | Format-Table -AutoSize
}
