$logPath = Join-Path $env:LOCALAPPDATA "ClearWin\logs\clearwin.log"
if (Test-Path $logPath) {
    Get-Content $logPath | Select-String -Pattern '(WebUi|Boot|Message|Error|Ignored|BLOCKED|Action)' | Select-Object -First 40 | ForEach-Object { $_.Line }
}
