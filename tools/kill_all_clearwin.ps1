Get-Process -Name ClearWin -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Killing ClearWin PID: $($_.Id)"
    Stop-Process -Id $_.Id -Force
}
Start-Sleep -Seconds 1
$remaining = Get-Process -Name ClearWin -ErrorAction SilentlyContinue
Write-Host "Remaining ClearWin processes: $(if ($remaining) { $remaining.Count } else { 0 })"
