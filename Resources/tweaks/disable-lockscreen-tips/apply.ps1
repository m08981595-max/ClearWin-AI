$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$rawPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager"
$p = if (Get-Command "Get-TweakRegistryPath" -ErrorAction SilentlyContinue) { Get-TweakRegistryPath $rawPath } else { $rawPath }

$targets = @(
    @{ Path = $p; Name = "RotatingLockScreenOverlayEnabled" },
    @{ Path = $p; Name = "SubscribedContent-338387Enabled" }
)
if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "disable-lockscreen-tips" -Targets $targets
}
$path = $targets[0].Path
if (-not (Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
Set-ItemProperty -Path $path -Name "RotatingLockScreenOverlayEnabled" -Value 0 -Type DWord -Force
Set-ItemProperty -Path $path -Name "SubscribedContent-338387Enabled" -Value 0 -Type DWord -Force
Write-Host "Lock screen tips disabled. Pre-tweak state captured."
