$logPath = Join-Path $env:LOCALAPPDATA "ClearWin\logs\clearwin.log"
if (Test-Path $logPath) {
    Get-Content $logPath | Select-String -Pattern '2026-10-15' | Select-Object -First 30 | ForEach-Object { $_.Line }
}
