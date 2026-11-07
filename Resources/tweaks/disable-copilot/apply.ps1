$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$pathHKCU = if (Get-Command "Get-TweakRegistryPath" -ErrorAction SilentlyContinue) { Get-TweakRegistryPath "HKCU:\Software\Policies\Microsoft\Windows\WindowsCopilot" } else { "HKCU:\Software\Policies\Microsoft\Windows\WindowsCopilot" }
$pathHKLM = if (Get-Command "Get-TweakRegistryPath" -ErrorAction SilentlyContinue) { Get-TweakRegistryPath "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot" } else { "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot" }

$targets = @(
    @{ Path = $pathHKCU; Name = "TurnOffWindowsCopilot" },
    @{ Path = $pathHKLM; Name = "TurnOffWindowsCopilot" }
)
if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "disable-copilot" -Targets $targets
}
foreach ($t in $targets) {
    if (-not (Test-Path $t.Path)) { New-Item -Path $t.Path -Force -ErrorAction SilentlyContinue | Out-Null }
    Set-ItemProperty -Path $t.Path -Name $t.Name -Type DWord -Value 1 -Force -ErrorAction SilentlyContinue
}
Write-Host "Windows Copilot disabled via system policy. Pre-tweak state captured."
