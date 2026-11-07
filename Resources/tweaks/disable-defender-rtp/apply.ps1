$ErrorActionPreference = 'Stop'

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This tweak requires Administrator privileges."
    exit 1
}

# Never allow silent/background execution of this security-reducing tweak.
# ClearWin sets this variable only after the user confirms the explicit warning.
if ($env:CLEARWIN_DEFENDER_OPT_IN -ne 'YES') {
    Write-Error "Blocked: Windows Defender Real-time Protection may only be disabled after an explicit ClearWin opt-in confirmation."
    exit 2
}

$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

Save-DefenderState -TweakName "disable-defender-rtp"

try {
    Set-MpPreference -DisableRealtimeMonitoring $true -ErrorAction Stop
    Write-Warning "Windows Defender Real-time Protection is now disabled. Pre-tweak state captured for rollback."
} catch {
    Write-Error "Failed to disable Defender: $_"
    exit 1
}
exit 0
