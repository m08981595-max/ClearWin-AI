$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$rawPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications"
$dir = if (Get-Command "Get-TweakRegistryPath" -ErrorAction SilentlyContinue) { Get-TweakRegistryPath $rawPath } else { $rawPath }

$targets = @(
    @{ Path = $dir; Name = "GlobalUserDisabled" }
)
if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "disable-background-ms-store-apps" -Targets $targets
}
$path = $targets[0].Path
if (-not (Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
Set-ItemProperty -Path $path -Name "GlobalUserDisabled" -Type DWord -Value 1 -Force
Write-Host "Background apps access disabled. Pre-tweak state captured."
