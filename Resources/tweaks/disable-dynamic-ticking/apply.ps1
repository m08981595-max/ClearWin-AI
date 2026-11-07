if ($env:CLEARWIN_ALLOW_DEFERRED_VM_TWEAKS -ne 'YES') {
    Write-Warning "BLOCKED (NOT TESTED): disable-dynamic-ticking modifies live BCD boot configuration and is deferred from production execution until verified in an isolated VM."
    exit 3
}

$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Save-BcdeditSettingState" -ErrorAction SilentlyContinue) {
    Save-BcdeditSettingState -TweakName "disable-dynamic-ticking" -SettingName "disabledynamictick"
}

try {
    bcdedit /set disabledynamictick yes
    if ($LASTEXITCODE -ne 0) { throw "bcdedit returned exit code $LASTEXITCODE" }
    Write-Host "Dynamic Ticking disabled successfully. Pre-tweak BCD state captured."
} catch {
    Write-Error "Failed to apply Dynamic Ticking tweak: $($_.Exception.Message)"
    exit 1
}