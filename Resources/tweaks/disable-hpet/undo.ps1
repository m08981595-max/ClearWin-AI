$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Restore-BcdeditSettingState" -ErrorAction SilentlyContinue) {
    Restore-BcdeditSettingState -TweakName "disable-hpet"
} else {
    & bcdedit.exe /deletevalue useplatformclock 2>$null
    Write-Host "Platform clock override removed."
}
