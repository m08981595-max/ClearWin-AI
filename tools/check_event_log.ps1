# tools/check_event_log.ps1
$events = Get-WinEvent -LogName Application -MaxEvents 50 -ErrorAction SilentlyContinue | Where-Object { 
    $_.ProviderName -eq ".NET Runtime" -or 
    $_.ProviderName -eq "Application Error" -or 
    ($_.Message -and $_.Message.Contains("ClearWin"))
}
foreach ($e in $events) {
    Write-Host "Time: $($e.TimeCreated) | Provider: $($e.ProviderName) | Id: $($e.Id)"
    Write-Host $e.Message
    Write-Host "--------------------------------------------------"
}
if (-not $events) {
    Write-Host "No crash events found in Application log."
}
