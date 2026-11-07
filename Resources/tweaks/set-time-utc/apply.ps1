$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$rawDir = "HKLM:\SYSTEM\CurrentControlSet\Control\TimeZoneInformation"
$dir = if (Get-Command "Get-TweakRegistryPath" -ErrorAction SilentlyContinue) { Get-TweakRegistryPath $rawDir } else { $rawDir }

$targets = @(
    @{ Path = $dir; Name = "RealTimeIsUniversal" }
)

if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "set-time-utc" -Targets $targets
}

if (-not (Test-Path $dir)) { New-Item -Path $dir -Force | Out-Null }
Set-ItemProperty -Path $dir -Name "RealTimeIsUniversal" -Value 1 -Type DWord -Force
Write-Host "Applied set-time-utc successfully. Pre-tweak state captured for exact rollback."
