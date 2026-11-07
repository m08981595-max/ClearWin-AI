$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Restore-BcdeditSettingState" -ErrorAction SilentlyContinue) {
    Restore-BcdeditSettingState -TweakName "disable-dynamic-ticking"
} else {
    bcdedit /set disabledynamictick no
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Dynamic Ticking re-enabled"
    } else {
        Write-Warning "Failed to re-enable dynamic ticking"
        exit 1
    }
}
