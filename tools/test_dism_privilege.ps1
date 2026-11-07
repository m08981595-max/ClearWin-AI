# Test Dism / Enable-WindowsOptionalFeature privilege
try {
    $proc = Start-Process -FilePath "dism.exe" -ArgumentList @("/Online", "/Enable-Feature", "/FeatureName:Containers-DisposableClientVM", "/All", "/NoRestart") -NoNewWindow -Wait -PassThru
    Write-Host "DISM Exit Code: $($proc.ExitCode)"
} catch {
    Write-Host "Exception: $($_.Exception.Message)"
}
