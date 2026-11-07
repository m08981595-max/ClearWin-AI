$ErrorActionPreference = 'Stop'

# Admin check
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This tweak requires Administrator privileges."
    exit 1
}

# Never allow silent/background execution of this high-risk security tweak.
if ($env:CLEARWIN_CORE_ISOLATION_OPT_IN -ne 'YES') {
    Write-Error "Blocked: Disabling Core Isolation (HVCI) requires an explicit ClearWin opt-in confirmation."
    exit 2
}

$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$tweak = "disable-core-isolation"
$path = "HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity"
$targets = @(
    @{ Path = $path; Name = "Enabled" }
)

if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName $tweak -Targets $targets
}

New-Item -Path $path -Force | Out-Null
Set-ItemProperty -Path $path -Name "Enabled" -Value 0 -Type DWord
Write-Host "Core Isolation (HVCI) disabled. Pre-tweak state captured. Reboot required to take effect."
