$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Restore-HibernationState" -ErrorAction SilentlyContinue) {
    Restore-HibernationState -TweakName "disable-hibernation"
} else {
    powercfg.exe /hibernate on
}
