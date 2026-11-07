# Wait for worker process to complete
$pidToWatch = 8092
$timeoutSec = 120
$elapsed = 0

while ($elapsed -lt $timeoutSec) {
    $proc = Get-Process -Id $pidToWatch -ErrorAction SilentlyContinue
    if (-not $proc) {
        Write-Host "Process $pidToWatch has finished."
        break
    }
    Start-Sleep -Seconds 5
    $elapsed += 5
    Write-Host "Waiting for PID $pidToWatch... Elapsed: $elapsed s"
}

if (Test-Path "tools\enable_sandbox_result.json") {
    Write-Host "Result JSON found:"
    Get-Content "tools\enable_sandbox_result.json"
} else {
    Write-Host "Result JSON not yet found."
}

if (Test-Path "tools\enable_sandbox_transcript.log") {
    Write-Host "Tail of Transcript:"
    Get-Content "tools\enable_sandbox_transcript.log" -Tail 10
}
