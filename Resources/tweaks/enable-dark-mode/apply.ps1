$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$rawDir = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"
$dir = if (Get-Command "Get-TweakRegistryPath" -ErrorAction SilentlyContinue) { Get-TweakRegistryPath $rawDir } else { $rawDir }

$targets = @(
    @{ Path = $dir; Name = "AppsUseLightTheme" }
)

if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "enable-dark-mode" -Targets $targets
}

if (-not (Test-Path $dir)) { New-Item -Path $dir -Force | Out-Null }
Set-ItemProperty -Path $dir -Name "AppsUseLightTheme" -Value 0 -Type DWord -Force
Write-Host "Applied enable-dark-mode successfully. Pre-tweak state captured for exact rollback."
