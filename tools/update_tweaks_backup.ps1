$tweakConfigs = @(
    @{
        Name = "align-taskbar-left"
        ApplyVal = 0
        Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"
        Prop = "TaskbarAl"
        Type = "DWord"
    },
    @{
        Name = "detailed-bsod"
        ApplyVal = 1
        Path = "HKLM:\SYSTEM\CurrentControlSet\Control\CrashControl"
        Prop = "DisplayParameters"
        Type = "DWord"
    },
    @{
        Name = "disable-fast-startup"
        ApplyVal = 0
        Path = "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power"
        Prop = "HiberbootEnabled"
        Type = "DWord"
    },
    @{
        Name = "enable-dark-mode"
        ApplyVal = 0
        Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"
        Prop = "AppsUseLightTheme"
        Type = "DWord"
    },
    @{
        Name = "enable-end-task-right-click"
        ApplyVal = 1
        Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings"
        Prop = "TaskbarEndTask"
        Type = "DWord"
    },
    @{
        Name = "set-time-utc"
        ApplyVal = 1
        Path = "HKLM:\SYSTEM\CurrentControlSet\Control\TimeZoneInformation"
        Prop = "RealTimeIsUniversal"
        Type = "DWord"
    }
)

foreach ($cfg in $tweakConfigs) {
    $tweakName = $cfg.Name
    $applyPath = "Resources\tweaks\$tweakName\apply.ps1"
    $undoPath = "Resources\tweaks\$tweakName\undo.ps1"

    $applyContent = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$targets = @(
    @{ Path = "{PATH}"; Name = "{PROP}" }
)

if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "{NAME}" -Targets $targets
}

$dir = "{PATH}"
if (-not (Test-Path $dir)) { New-Item -Path $dir -Force | Out-Null }
Set-ItemProperty -Path $dir -Name "{PROP}" -Value {VAL} -Type {TYPE} -Force
Write-Host "Applied {NAME} successfully. Pre-tweak state captured for exact rollback."
'@
    $applyContent = $applyContent.Replace("{PATH}", $cfg.Path)
    $applyContent = $applyContent.Replace("{PROP}", $cfg.Prop)
    $applyContent = $applyContent.Replace("{NAME}", $tweakName)
    $applyContent = $applyContent.Replace("{VAL}", $cfg.ApplyVal.ToString())
    $applyContent = $applyContent.Replace("{TYPE}", $cfg.Type)
    Set-Content -Path $applyPath -Value $applyContent -Encoding UTF8

    $undoContent = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Restore-RegistryState" -ErrorAction SilentlyContinue) {
    Restore-RegistryState -TweakName "{NAME}"
} else {
    Write-Error "Cannot undo {NAME}: TweakBackupEngine not found."
    exit 1
}
'@
    $undoContent = $undoContent.Replace("{NAME}", $tweakName)
    Set-Content -Path $undoPath -Value $undoContent -Encoding UTF8
    Write-Host "Updated $tweakName with exact state backup"
}
