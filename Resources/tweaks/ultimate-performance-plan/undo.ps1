$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Restore-PowerPlanState" -ErrorAction SilentlyContinue) {
    Restore-PowerPlanState -TweakName "ultimate-performance-plan"
} else {
    powercfg -setactive 381b4222-f694-41f0-9685-ff5bb260df2e
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Balanced power plan restored"
    } else {
        Write-Warning "Failed to restore Balanced plan"
        exit 1
    }
}
