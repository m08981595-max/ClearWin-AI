$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

Write-Host "ClearWin: Restoring services from pre-tweak saved state..."
if (Get-Command "Restore-ServicesState" -ErrorAction SilentlyContinue) {
    Restore-ServicesState -TweakName "set-services-to-manual"
} else {
    Write-Error "CRITICAL: TweakBackupEngine not found. Cannot restore services safely."
    exit 1
}
