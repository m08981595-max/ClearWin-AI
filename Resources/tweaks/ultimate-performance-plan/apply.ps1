$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Save-PowerPlanState" -ErrorAction SilentlyContinue) {
    Save-PowerPlanState -TweakName "ultimate-performance-plan"
}

$guidPattern = '([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})'
$ultimatePlanGUID = $null

# Locale-independent lookup: check for existing Ultimate Performance GUID or localized names
$existingLines = @(powercfg -l | Where-Object { $_ -match "e9a42b02-d5df-448d-aa00-03f14749eb61|Ultimate Performance|الأداء الفائق" })
if ($existingLines.Count -gt 0 -and ($existingLines[0] -match $guidPattern)) {
    $ultimatePlanGUID = $matches[1]
    Write-Host "Ultimate Performance plan already exists ($ultimatePlanGUID)."
} else {
    Write-Host "Ultimate Performance plan not found. Creating via powercfg -duplicatescheme..."
    $dupOut = powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61 2>&1 | Out-String
    if ($dupOut -match $guidPattern) {
        $ultimatePlanGUID = $matches[1]
    }
}

if (-not $ultimatePlanGUID) {
    $fallbackLine = @(powercfg -l | Select-Object -Last 1)
    if ($fallbackLine -and ($fallbackLine[0] -match $guidPattern)) {
        $ultimatePlanGUID = $matches[1]
    }
}

if ($ultimatePlanGUID) {
    powercfg -setactive $ultimatePlanGUID
    Write-Host "Ultimate Performance power plan ($ultimatePlanGUID) is now active. Pre-tweak plan scheme captured for rollback."
} else {
    Write-Warning "Failed to determine Ultimate Performance scheme GUID."
    exit 1
}
