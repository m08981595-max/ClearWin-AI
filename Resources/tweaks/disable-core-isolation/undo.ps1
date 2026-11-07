$ErrorActionPreference = 'Stop'

$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$tweak = "disable-core-isolation"
if (Get-Command "Restore-RegistryState" -ErrorAction SilentlyContinue) {
    Restore-RegistryState -TweakName $tweak
} else {
    $path = "HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity"
    Set-ItemProperty -Path $path -Name "Enabled" -Value 1 -Type DWord
    Write-Host "Core Isolation (HVCI) restored to Enabled"
}
