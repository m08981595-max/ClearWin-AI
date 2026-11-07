$p = Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -Command `"taskkill /F /PID 5432`"" -PassThru -Wait
Write-Host "Elevated taskkill exit code: $($p.ExitCode)"
Start-Sleep -Seconds 1
$check = Get-Process -Id 5432 -ErrorAction SilentlyContinue
if ($check) {
    Write-Warning "PID 5432 is still running!"
} else {
    Write-Host "[OK] PID 5432 has been terminated successfully!" -ForegroundColor Green
}
