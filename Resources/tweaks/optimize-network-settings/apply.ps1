if ($env:CLEARWIN_ALLOW_DEFERRED_VM_TWEAKS -ne 'YES') {
    Write-Warning "BLOCKED (NOT TESTED): optimize-network-settings modifies global TCP/IP stack parameters and is deferred from production execution until verified in an isolated VM."
    exit 3
}

$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Save-NetworkGlobalState" -ErrorAction SilentlyContinue) {
    Save-NetworkGlobalState -TweakName "optimize-network-settings"
}

Write-Host "Applying low-latency TCP network tweaks..."
netsh int tcp set global rss=enabled 2>$null
netsh int tcp set global ecncapability=enabled 2>$null
netsh int tcp set global timestamps=disabled 2>$null
netsh int tcp set global initialRto=2000 2>$null
netsh int tcp set global autotuninglevel=normal 2>$null
Write-Host "Network optimization applied. Pre-tweak TCP stack parameters captured."
