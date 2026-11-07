$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$rawPath = "HKCU:\Control Panel\Mouse"
$path = if (Get-Command "Get-TweakRegistryPath" -ErrorAction SilentlyContinue) { Get-TweakRegistryPath $rawPath } else { $rawPath }

$targets = @(
    @{ Path = $path; Name = "MouseSpeed" },
    @{ Path = $path; Name = "MouseThreshold1" },
    @{ Path = $path; Name = "MouseThreshold2" }
)
if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "disable-mouse-acceleration" -Targets $targets
}
if (-not (Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
Set-ItemProperty -Path $path -Name "MouseSpeed" -Value "0" -Type String -Force
Set-ItemProperty -Path $path -Name "MouseThreshold1" -Value "0" -Type String -Force
Set-ItemProperty -Path $path -Name "MouseThreshold2" -Value "0" -Type String -Force
Write-Host "Mouse acceleration disabled (linear curve). Pre-tweak state captured."
