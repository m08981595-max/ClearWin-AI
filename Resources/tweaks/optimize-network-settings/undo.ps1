$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Restore-NetworkGlobalState" -ErrorAction SilentlyContinue) {
    Restore-NetworkGlobalState -TweakName "optimize-network-settings"
} else {
    netsh int tcp reset 2>$null
}
