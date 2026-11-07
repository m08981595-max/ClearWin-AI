$logPath = Join-Path $env:LOCALAPPDATA "ClearWin\logs\clearwin.log"
if (Test-Path $logPath) {
    Write-Host "Log file: $logPath"
    Get-Content $logPath -Tail 80
} else {
    Write-Warning "Log file not found at $logPath"
}
