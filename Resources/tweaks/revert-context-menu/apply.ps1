$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$rawPath = "HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32"
$path = if (Get-Command "Get-TweakRegistryPath" -ErrorAction SilentlyContinue) { Get-TweakRegistryPath $rawPath } else { $rawPath }

$targets = @(
    @{ Path = $path; Name = "" }
)
if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "revert-context-menu" -Targets $targets
}
New-Item -Path $path -Force -Value "" | Out-Null
Write-Host "Classic Windows 10 context menu enabled. Pre-tweak state captured."
