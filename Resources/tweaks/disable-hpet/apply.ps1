if ($env:CLEARWIN_ALLOW_DEFERRED_VM_TWEAKS -ne 'YES') {
    Write-Warning "BLOCKED (NOT TESTED): disable-hpet modifies live BCD boot configuration and is deferred from production execution until verified in an isolated VM."
    exit 3
}

$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This tweak requires Administrator privileges."
    exit 1
}

if (Get-Command "Save-BcdeditSettingState" -ErrorAction SilentlyContinue) {
    Save-BcdeditSettingState -TweakName "disable-hpet" -SettingName "useplatformclock"
}

& bcdedit.exe /deletevalue useplatformclock 2>$null
Write-Host "Platform clock override removed; Windows selects default clock. Pre-tweak BCD state captured."
