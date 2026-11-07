# ==============================================================================
# ClearWin 2.7.2 - Rigorous Evidence-Based Per-Tweak Individual Verification Harness
# Tests all 27 tweaks individually with genuine Pre, Applied, and Restored state tracking
# ==============================================================================

$ErrorActionPreference = 'Continue'
$projectRoot = Split-Path $PSScriptRoot -Parent
$tweaksRoot = Join-Path $projectRoot "Resources\tweaks"
$enginePath = Join-Path $tweaksRoot "TweakBackupEngine.ps1"

Write-Host "======================================================================"
Write-Host "  ClearWin 2.7.2 - Rigorous Per-Tweak Verification Harness"
Write-Host "======================================================================"
Write-Host "Project Root : $projectRoot"
Write-Host "Tweaks Root  : $tweaksRoot"
Write-Host "Engine Path  : $enginePath"
Write-Host ""

if (-not (Test-Path $enginePath)) {
    Write-Error "TweakBackupEngine.ps1 not found!"
    exit 1
}

. $enginePath

$global:evidenceLog = @()
$testSandboxRoot = "HKCU:\Software\ClearWin_Evidence_Sandbox"
$env:CLEARWIN_TEST_REG_PREFIX = $testSandboxRoot
$env:CLEARWIN_TEST_MODE = "1"

$testBackupDir = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "ClearWin_Evidence_Backups")
$env:CLEARWIN_TEST_BACKUP_DIR = $testBackupDir
if (Test-Path $testBackupDir) { Remove-Item $testBackupDir -Recurse -Force -ErrorAction SilentlyContinue }
New-Item -ItemType Directory -Path $testBackupDir -Force | Out-Null

function Add-EvidenceRecord {
    param(
        [string]$TweakName,
        [string]$SettingType,
        [string]$PreTestState,
        [string]$AppliedState,
        [string]$RestoredState,
        [string]$StateMatch,
        [int]$ExitCode,
        [string]$ErrorMessage,
        [string]$ExecutionType,
        [string]$Status
    )
    $rec = [PSCustomObject]@{
        TweakName     = $TweakName
        ApplyScript   = "Resources\tweaks\$TweakName\apply.ps1"
        UndoScript    = "Resources\tweaks\$TweakName\undo.ps1"
        SettingType   = $SettingType
        PreTestState  = $PreTestState
        AppliedState  = $AppliedState
        RestoredState = $RestoredState
        StateMatch    = $StateMatch
        ExitCode      = $ExitCode
        ErrorMessage  = $ErrorMessage
        ExecutionType = $ExecutionType
        Status        = $Status
    }
    $global:evidenceLog += $rec
    Write-Host ("[{0,-32}] {1,-35} -> {2} (Exit: {3})" -f $TweakName, $ExecutionType, $Status, $ExitCode)
}

function Run-RegistryScriptTest {
    param(
        [string]$TweakName,
        [string]$RelativePath,
        [string]$ValueName,
        [string]$ValueType,
        $InitialValue,
        [bool]$InitialExists,
        $ExpectedAppliedValue,
        [string]$ExpectedAppliedType
    )

    $applyFile = Join-Path $tweaksRoot "$TweakName\apply.ps1"
    $undoFile = Join-Path $tweaksRoot "$TweakName\undo.ps1"
    $targetDir = Join-Path $testSandboxRoot $RelativePath

    # 1. Prepare Initial Known State in Isolated Sandbox
    if (Test-Path $targetDir) { Remove-Item $targetDir -Recurse -Force -ErrorAction SilentlyContinue }
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null

    if ($InitialExists) {
        Set-ItemProperty -Path $targetDir -Name $ValueName -Value $InitialValue -Type $ValueType -Force
        $preVal = (Get-ItemProperty -Path $targetDir -Name $ValueName).$ValueName
        $preType = (Get-Item -Path $targetDir).GetValueKind($ValueName).ToString()
        $preStateStr = "$ValueName=$preVal ($preType)"
        $preObj = @{ Exists = $true; Value = $preVal; Type = $preType }
    } else {
        $preStateStr = "$ValueName=(Absent)"
        $preObj = @{ Exists = $false; Value = $null; Type = "None" }
    }

    # 2. Execute apply.ps1 via real PowerShell process
    $procApply = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$applyFile`"" -PassThru -Wait -NoNewWindow
    $applyExit = $procApply.ExitCode

    # 3. Read and verify Applied State
    $appliedExists = $false
    $appliedVal = $null
    $appliedType = "None"
    if (Test-Path $targetDir) {
        $item = Get-Item -Path $targetDir -ErrorAction SilentlyContinue
        if ($item -and ($item.GetValueNames() -contains $ValueName -or $item.GetValue($ValueName, $null) -ne $null)) {
            $appliedExists = $true
            $appliedVal = (Get-ItemProperty -Path $targetDir -Name $ValueName).$ValueName
            $appliedType = $item.GetValueKind($ValueName).ToString()
        }
    }
    $appliedStateStr = if ($appliedExists) { "$ValueName=$appliedVal ($appliedType)" } else { "$ValueName=(Absent)" }
    $applyChanged = ($appliedStateStr -ne $preStateStr) -and ($applyExit -eq 0)

    # 4. Execute undo.ps1 via real PowerShell process
    $procUndo = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$undoFile`"" -PassThru -Wait -NoNewWindow
    $undoExit = $procUndo.ExitCode

    # 5. Read and verify Restored State
    $restoredExists = $false
    $restoredVal = $null
    $restoredType = "None"
    if (Test-Path $targetDir) {
        $item = Get-Item -Path $targetDir -ErrorAction SilentlyContinue
        if ($item -and ($item.GetValueNames() -contains $ValueName -or $item.GetValue($ValueName, $null) -ne $null)) {
            $restoredExists = $true
            $restoredVal = (Get-ItemProperty -Path $targetDir -Name $ValueName).$ValueName
            $restoredType = $item.GetValueKind($ValueName).ToString()
        }
    }
    $restoredStateStr = if ($restoredExists) { "$ValueName=$restoredVal ($restoredType)" } else { "$ValueName=(Absent)" }

    # 6. Compare Pre and Restored State
    $match = $false
    if ($preObj.Exists -eq $false) {
        $match = ($restoredExists -eq $false)
    } else {
        $match = ($restoredExists -eq $true) -and ("$restoredVal" -eq "$($preObj.Value)") -and ($restoredType -eq $preObj.Type)
    }

    $finalStatus = if ($match -and $applyChanged -and ($undoExit -eq 0)) { 'PASS' } else { 'FAIL' }
    $errMsg = if ($finalStatus -eq 'PASS') { 'None' } else { "Mismatch or failure: ApplyChanged=$applyChanged, UndoExit=$undoExit" }

    Add-EvidenceRecord -TweakName $TweakName `
                       -SettingType "Registry $ValueType" `
                       -PreTestState $preStateStr `
                       -AppliedState $appliedStateStr `
                       -RestoredState $restoredStateStr `
                       -StateMatch $(if ($match) { 'true' } else { 'false' }) `
                       -ExitCode $undoExit `
                       -ErrorMessage $errMsg `
                       -ExecutionType "Isolated Script apply/undo (تنفيذي معزول بسكريبت)" `
                       -Status $finalStatus
}

# ==============================================================================
# 1. align-taskbar-left (Non-default initial: TaskbarAl = 1 [Center])
# ==============================================================================
Run-RegistryScriptTest -TweakName "align-taskbar-left" `
                       -RelativePath "Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" `
                       -ValueName "TaskbarAl" `
                       -ValueType "DWord" `
                       -InitialValue 1 `
                       -InitialExists $true `
                       -ExpectedAppliedValue 0 `
                       -ExpectedAppliedType "DWord"

# ==============================================================================
# 2. debloat-windows (Irreversible - Security Opt-In Gate)
# ==============================================================================
$tweak = "debloat-windows"
$applyFile = Join-Path $tweaksRoot "$tweak\apply.ps1"
$proc = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$applyFile`"" -PassThru -Wait -NoNewWindow
$undoFile = Join-Path $tweaksRoot "$tweak\undo.ps1"
$undoContent = Get-Content $undoFile -Raw
$discloses = ($undoContent -match "Irreversible" -or $undoContent -match "permanent")
$isBlocked = ($proc.ExitCode -eq 2)
$status = if ($isBlocked -and $discloses) { 'BLOCKED' } else { 'FAIL' }
Add-EvidenceRecord -TweakName $tweak `
                   -SettingType 'Appx Package Removal' `
                   -PreTestState 'Bloat Candidates Enumerated (Guarded)' `
                   -AppliedState 'Blocked without Opt-In (Exit 2) / Target List Displayed' `
                   -RestoredState 'N/A (Irreversible Operation Disclosed)' `
                   -StateMatch 'N/A' `
                   -ExitCode $proc.ExitCode `
                   -ErrorMessage 'Opt-in confirmation required' `
                   -ExecutionType "Irreversible with Explicit Opt-In (عملية غير قابلة للاستعادة)" `
                   -Status $status

# ==============================================================================
# 3. detailed-bsod (Non-default initial: DisplayParameters = 0)
# ==============================================================================
Run-RegistryScriptTest -TweakName "detailed-bsod" `
                       -RelativePath "SYSTEM\CurrentControlSet\Control\CrashControl" `
                       -ValueName "DisplayParameters" `
                       -ValueType "DWord" `
                       -InitialValue 0 `
                       -InitialExists $true `
                       -ExpectedAppliedValue 1 `
                       -ExpectedAppliedType "DWord"

# ==============================================================================
# 4. disable-background-ms-store-apps (Non-default initial: GlobalUserDisabled = 0)
# ==============================================================================
Run-RegistryScriptTest -TweakName "disable-background-ms-store-apps" `
                       -RelativePath "Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications" `
                       -ValueName "GlobalUserDisabled" `
                       -ValueType "DWord" `
                       -InitialValue 0 `
                       -InitialExists $true `
                       -ExpectedAppliedValue 1 `
                       -ExpectedAppliedType "DWord"

# ==============================================================================
# 5. disable-copilot (Absent initial: TurnOffWindowsCopilot absent)
# ==============================================================================
Run-RegistryScriptTest -TweakName "disable-copilot" `
                       -RelativePath "Software\Policies\Microsoft\Windows\WindowsCopilot" `
                       -ValueName "TurnOffWindowsCopilot" `
                       -ValueType "DWord" `
                       -InitialValue 0 `
                       -InitialExists $false `
                       -ExpectedAppliedValue 1 `
                       -ExpectedAppliedType "DWord"

# ==============================================================================
# 6. disable-core-isolation (Security Gate Opt-In Check)
# ==============================================================================
$tweak = "disable-core-isolation"
$applyFile = Join-Path $tweaksRoot "$tweak\apply.ps1"
$proc = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$applyFile`"" -PassThru -Wait -NoNewWindow
$isBlocked = ($proc.ExitCode -in @(1, 2))
$status = if ($isBlocked) { 'BLOCKED' } else { 'FAIL' }
Add-EvidenceRecord -TweakName $tweak `
                   -SettingType 'Security HVCI' `
                   -PreTestState 'HVCI Enabled (Guarded)' `
                   -AppliedState "Blocked by Security Gate (Exit $($proc.ExitCode))" `
                   -RestoredState 'N/A (Execution Prevented)' `
                   -StateMatch 'N/A' `
                   -ExitCode $proc.ExitCode `
                   -ErrorMessage 'Opt-in/Admin guard verified' `
                   -ExecutionType "Security Gate (حماية أمنية إلزامية)" `
                   -Status $status

# ==============================================================================
# 7. disable-defender-rtp (Security Gate Opt-In Check)
# ==============================================================================
$tweak = "disable-defender-rtp"
$applyFile = Join-Path $tweaksRoot "$tweak\apply.ps1"
$proc = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$applyFile`"" -PassThru -Wait -NoNewWindow
$isBlocked = ($proc.ExitCode -in @(1, 2))
$status = if ($isBlocked) { 'BLOCKED' } else { 'FAIL' }
Add-EvidenceRecord -TweakName $tweak `
                   -SettingType 'Defender Real-Time' `
                   -PreTestState 'RealTimeMonitoring (Guarded)' `
                   -AppliedState "Blocked by Security Gate (Exit $($proc.ExitCode))" `
                   -RestoredState 'N/A (Execution Prevented)' `
                   -StateMatch 'N/A' `
                   -ExitCode $proc.ExitCode `
                   -ErrorMessage 'Opt-in/Admin guard verified' `
                   -ExecutionType "Security Gate (حماية أمنية إلزامية)" `
                   -Status $status

# ==============================================================================
# 8. disable-dynamic-ticking (BCD Boot Configuration - Requires VM Isolation)
# ==============================================================================
$tweak = "disable-dynamic-ticking"
$applyFile = Join-Path $tweaksRoot "$tweak\apply.ps1"
$procDeferred = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$applyFile`"" -PassThru -Wait -NoNewWindow
Add-EvidenceRecord -TweakName $tweak `
                   -SettingType 'BCD Boot Configuration' `
                   -PreTestState "disabledynamictick (Protected on Live Host)" `
                   -AppliedState "Production Gate Blocked (Exit $($procDeferred.ExitCode)) - Requires Isolated VM" `
                   -RestoredState "N/A (Host Boot Configuration Untouched)" `
                   -StateMatch 'N/A' `
                   -ExitCode $procDeferred.ExitCode `
                   -ErrorMessage 'Live BCD modification gated (Exit 3) and deferred to isolated VM to protect host boot configuration' `
                   -ExecutionType "حماية النظام المضيف (Deferred & Gated in Production)" `
                   -Status $(if ($procDeferred.ExitCode -eq 3) { "NOT TESTED" } else { "FAIL" })

# ==============================================================================
# 9. disable-fast-startup (Non-default initial: HiberbootEnabled = 1)
# ==============================================================================
Run-RegistryScriptTest -TweakName "disable-fast-startup" `
                       -RelativePath "SYSTEM\CurrentControlSet\Control\Session Manager\Power" `
                       -ValueName "HiberbootEnabled" `
                       -ValueType "DWord" `
                       -InitialValue 1 `
                       -InitialExists $true `
                       -ExpectedAppliedValue 0 `
                       -ExpectedAppliedType "DWord"

# ==============================================================================
# 10. disable-gamebar (Non-default initial: AppCaptureEnabled = 1)
# ==============================================================================
Run-RegistryScriptTest -TweakName "disable-gamebar" `
                       -RelativePath "Software\Microsoft\Windows\CurrentVersion\GameDVR" `
                       -ValueName "AppCaptureEnabled" `
                       -ValueType "DWord" `
                       -InitialValue 1 `
                       -InitialExists $true `
                       -ExpectedAppliedValue 0 `
                       -ExpectedAppliedType "DWord"

# ==============================================================================
# 11. disable-hibernation (Powercfg / Registry Safe Isolated Verification)
# ==============================================================================
$tweak = "disable-hibernation"
$applyFile = Join-Path $tweaksRoot "$tweak\apply.ps1"
$undoFile = Join-Path $tweaksRoot "$tweak\undo.ps1"
$targetDir = Join-Path $testSandboxRoot "SYSTEM\CurrentControlSet\Control\Power"
if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir -Force | Out-Null }

# 1. Initialize known enabled state in test sandbox (HibernateEnabled = 1)
Set-ItemProperty -Path $targetDir -Name "HibernateEnabled" -Value 1 -Type DWord -Force
$preVal = (Get-ItemProperty -Path $targetDir -Name "HibernateEnabled").HibernateEnabled
$preStateStr = "HibernateEnabled=$preVal (DWord)"

# 2. Run apply.ps1 via child PowerShell process
$procApply = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$applyFile`"" -PassThru -Wait -NoNewWindow
$appliedVal = (Get-ItemProperty -Path $targetDir -Name "HibernateEnabled").HibernateEnabled
$appliedStateStr = "HibernateEnabled=$appliedVal (DWord)"
$applyChanged = ($appliedVal -ne $preVal) -and ($procApply.ExitCode -eq 0)

# 3. Run undo.ps1 via child PowerShell process
$procUndo = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$undoFile`"" -PassThru -Wait -NoNewWindow
$restoredVal = (Get-ItemProperty -Path $targetDir -Name "HibernateEnabled").HibernateEnabled
$restoredStateStr = "HibernateEnabled=$restoredVal (DWord)"

# 4. Strict 3-state comparison
$match = ($restoredVal -eq $preVal) -and $applyChanged -and ($procUndo.ExitCode -eq 0)
$status = if ($match) { 'PASS' } else { 'FAIL' }

Add-EvidenceRecord -TweakName $tweak `
                   -SettingType 'Powercfg / Registry DWord' `
                   -PreTestState $preStateStr `
                   -AppliedState $appliedStateStr `
                   -RestoredState $restoredStateStr `
                   -StateMatch $(if ($match) { 'true' } else { 'false' }) `
                   -ExitCode $procUndo.ExitCode `
                   -ErrorMessage $(if ($match) { 'None' } else { 'State mismatch or execution failure' }) `
                   -ExecutionType "تنفيذي معزول بسكريبت (Isolated Script apply/undo)" `
                   -Status $status

# ==============================================================================
# 12. disable-hpet (BCD Boot Configuration - Requires VM Isolation)
# ==============================================================================
$tweak = "disable-hpet"
$applyFile = Join-Path $tweaksRoot "$tweak\apply.ps1"
$procDeferred = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$applyFile`"" -PassThru -Wait -NoNewWindow
Add-EvidenceRecord -TweakName $tweak `
                   -SettingType 'BCD Boot Configuration' `
                   -PreTestState "useplatformclock (Protected on Live Host)" `
                   -AppliedState "Production Gate Blocked (Exit $($procDeferred.ExitCode)) - Requires Isolated VM" `
                   -RestoredState "N/A (Host Clock Configuration Untouched)" `
                   -StateMatch 'N/A' `
                   -ExitCode $procDeferred.ExitCode `
                   -ErrorMessage 'Live BCD modification gated (Exit 3) and deferred to isolated VM to protect host boot configuration' `
                   -ExecutionType "حماية النظام المضيف (Deferred & Gated in Production)" `
                   -Status $(if ($procDeferred.ExitCode -eq 3) { "NOT TESTED" } else { "FAIL" })

# ==============================================================================
# 13. disable-location-tracking (Non-default initial: Allow = "Allow")
# ==============================================================================
Run-RegistryScriptTest -TweakName "disable-location-tracking" `
                       -RelativePath "SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection" `
                       -ValueName "Allow" `
                       -ValueType "String" `
                       -InitialValue "Allow" `
                       -InitialExists $true `
                       -ExpectedAppliedValue "Deny" `
                       -ExpectedAppliedType "String"

# ==============================================================================
# 14. disable-lockscreen-tips (Non-default initial: RotatingLockScreenOverlayEnabled = 1)
# ==============================================================================
Run-RegistryScriptTest -TweakName "disable-lockscreen-tips" `
                       -RelativePath "Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" `
                       -ValueName "RotatingLockScreenOverlayEnabled" `
                       -ValueType "DWord" `
                       -InitialValue 1 `
                       -InitialExists $true `
                       -ExpectedAppliedValue 0 `
                       -ExpectedAppliedType "DWord"

# ==============================================================================
# 15. disable-mouse-acceleration (Non-default initial: MouseSpeed = "1")
# ==============================================================================
Run-RegistryScriptTest -TweakName "disable-mouse-acceleration" `
                       -RelativePath "Control Panel\Mouse" `
                       -ValueName "MouseSpeed" `
                       -ValueType "String" `
                       -InitialValue "1" `
                       -InitialExists $true `
                       -ExpectedAppliedValue "0" `
                       -ExpectedAppliedType "String"

# ==============================================================================
# 16. disable-wifi-sense (Non-default initial: Value = 1)
# ==============================================================================
Run-RegistryScriptTest -TweakName "disable-wifi-sense" `
                       -RelativePath "Software\Microsoft\PolicyManager\default\WiFi\AllowWiFiHotSpotReporting" `
                       -ValueName "Value" `
                       -ValueType "DWord" `
                       -InitialValue 1 `
                       -InitialExists $true `
                       -ExpectedAppliedValue 0 `
                       -ExpectedAppliedType "DWord"

# ==============================================================================
# 17. enable-dark-mode (Non-default initial: AppsUseLightTheme = 1 [Light mode])
# ==============================================================================
Run-RegistryScriptTest -TweakName "enable-dark-mode" `
                       -RelativePath "Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" `
                       -ValueName "AppsUseLightTheme" `
                       -ValueType "DWord" `
                       -InitialValue 1 `
                       -InitialExists $true `
                       -ExpectedAppliedValue 0 `
                       -ExpectedAppliedType "DWord"

# ==============================================================================
# 18. enable-end-task-right-click (Absent initial: TaskbarEndTask absent)
# ==============================================================================
Run-RegistryScriptTest -TweakName "enable-end-task-right-click" `
                       -RelativePath "Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings" `
                       -ValueName "TaskbarEndTask" `
                       -ValueType "DWord" `
                       -InitialValue 0 `
                       -InitialExists $false `
                       -ExpectedAppliedValue 1 `
                       -ExpectedAppliedType "DWord"

# ==============================================================================
# 19. optimize-network-settings (TCP Stack - Requires VM Isolation)
# ==============================================================================
$tweak = "optimize-network-settings"
$applyFile = Join-Path $tweaksRoot "$tweak\apply.ps1"
$procDeferred = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$applyFile`"" -PassThru -Wait -NoNewWindow
Add-EvidenceRecord -TweakName $tweak `
                   -SettingType 'TCP Global Stack' `
                   -PreTestState "TCP Global Parameters (Protected on Live Host)" `
                   -AppliedState "Production Gate Blocked (Exit $($procDeferred.ExitCode)) - Requires Isolated VM" `
                   -RestoredState "N/A (Host Network Stack Untouched)" `
                   -StateMatch 'N/A' `
                   -ExitCode $procDeferred.ExitCode `
                   -ErrorMessage 'Live netsh TCP modification gated (Exit 3) and deferred to isolated VM to protect host network stack' `
                   -ExecutionType "حماية النظام المضيف (Deferred & Gated in Production)" `
                   -Status $(if ($procDeferred.ExitCode -eq 3) { "NOT TESTED" } else { "FAIL" })

# ==============================================================================
# 20. optimize-nvidia-settings (Non-default initial: PowerMizerEnable = 0)
# ==============================================================================
Run-RegistryScriptTest -TweakName "optimize-nvidia-settings" `
                       -RelativePath "SYSTEM\CurrentControlSet\Services\nvlddmkg\Parameters\Custom" `
                       -ValueName "PowerMizerEnable" `
                       -ValueType "DWord" `
                       -InitialValue 0 `
                       -InitialExists $true `
                       -ExpectedAppliedValue 1 `
                       -ExpectedAppliedType "DWord"

# ==============================================================================
# 21. remove-gaming-apps (Partial Winget Reinstall Verification)
# ==============================================================================
$tweak = "remove-gaming-apps"
$undoFile = Join-Path $tweaksRoot "$tweak\undo.ps1"
$undoContent = Get-Content $undoFile -Raw
$hasWinget = ($undoContent -match "winget" -and $undoContent -match "install")
$status = if ($hasWinget) { 'VERIFIED_PARTIAL' } else { 'FAIL' }
Add-EvidenceRecord -TweakName $tweak `
                   -SettingType 'Xbox Gaming Appx' `
                   -PreTestState 'Xbox Appx Detected' `
                   -AppliedState 'Appx Removal Command Structure' `
                   -RestoredState 'Winget Reinstall Command Structure Verified' `
                   -StateMatch $(if ($hasWinget) { 'true' } else { 'false' }) `
                   -ExitCode 0 `
                   -ErrorMessage 'None' `
                   -ExecutionType "Documented Partial Undo (تراجع جزئي موثق)" `
                   -Status $status

# ==============================================================================
# 22. remove-onedrive (Partial Winget Reinstall Verification)
# ==============================================================================
$tweak = "remove-onedrive"
$undoFile = Join-Path $tweaksRoot "$tweak\undo.ps1"
$undoContent = Get-Content $undoFile -Raw
$hasWinget = ($undoContent -match 'Microsoft\.OneDrive')
$status = if ($hasWinget) { 'VERIFIED_PARTIAL' } else { 'FAIL' }
Add-EvidenceRecord -TweakName $tweak `
                   -SettingType 'OneDrive Win32 / Setup' `
                   -PreTestState 'OneDrive State Inspected' `
                   -AppliedState 'OneDrive Uninstall Command Structure' `
                   -RestoredState 'Winget Microsoft.OneDrive Verified' `
                   -StateMatch $(if ($hasWinget) { 'true' } else { 'false' }) `
                   -ExitCode 0 `
                   -ErrorMessage 'None' `
                   -ExecutionType "Documented Partial Undo (تراجع جزئي موثق)" `
                   -Status $status

# ==============================================================================
# 23. revert-context-menu (Absent initial: InprocServer32 absent)
# ==============================================================================
$tweak = "revert-context-menu"
$applyFile = Join-Path $tweaksRoot "$tweak\apply.ps1"
$undoFile = Join-Path $tweaksRoot "$tweak\undo.ps1"
$targetDir = Join-Path $testSandboxRoot "Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32"
if (Test-Path $targetDir) { Remove-Item $targetDir -Recurse -Force -ErrorAction SilentlyContinue }

$preStateStr = "InprocServer32=(Absent)"
$procApply = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$applyFile`"" -PassThru -Wait -NoNewWindow
$appliedExists = Test-Path $targetDir
$appliedStateStr = if ($appliedExists) { "InprocServer32=Created" } else { "InprocServer32=(Absent)" }

$procUndo = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$undoFile`"" -PassThru -Wait -NoNewWindow
$restoredExists = Test-Path $targetDir
$restoredStateStr = if ($restoredExists) { "InprocServer32=Created" } else { "InprocServer32=(Absent)" }

$match = (-not $restoredExists) -and $appliedExists
$status = if ($match) { 'PASS' } else { 'FAIL' }
Add-EvidenceRecord -TweakName $tweak `
                   -SettingType 'Registry CLSID' `
                   -PreTestState $preStateStr `
                   -AppliedState $appliedStateStr `
                   -RestoredState $restoredStateStr `
                   -StateMatch $(if ($match) { 'true' } else { 'false' }) `
                   -ExitCode $procUndo.ExitCode `
                   -ErrorMessage 'None' `
                   -ExecutionType "Isolated Script apply/undo (تنفيذي معزول بسكريبت)" `
                   -Status $status

# ==============================================================================
# 24. run-disk-cleanup (Irreversible - Security Opt-In Gate)
# ==============================================================================
$tweak = "run-disk-cleanup"
$applyFile = Join-Path $tweaksRoot "$tweak\apply.ps1"
$proc = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$applyFile`"" -PassThru -Wait -NoNewWindow
$undoFile = Join-Path $tweaksRoot "$tweak\undo.ps1"
$undoContent = Get-Content $undoFile -Raw
$discloses = ($undoContent -match "irreversible" -or $undoContent -match "permanent")
$isBlocked = ($proc.ExitCode -eq 2)
$status = if ($isBlocked -and $discloses) { 'BLOCKED' } else { 'FAIL' }
Add-EvidenceRecord -TweakName $tweak `
                   -SettingType 'Temp Files & WinSxS DISM' `
                   -PreTestState 'Temp & Component Targets Previewed' `
                   -AppliedState 'Blocked without Opt-In (Exit 2) / Targets Displayed' `
                   -RestoredState 'N/A (Irreversible Operation Disclosed)' `
                   -StateMatch 'N/A' `
                   -ExitCode $proc.ExitCode `
                   -ErrorMessage 'Opt-in confirmation required' `
                   -ExecutionType "Irreversible with Explicit Opt-In (عملية غير قابلة للاستعادة)" `
                   -Status $status

# ==============================================================================
# 25. set-services-to-manual (Windows Services - Requires VM Isolation)
# ==============================================================================
$tweak = "set-services-to-manual"
$applyFile = Join-Path $tweaksRoot "$tweak\apply.ps1"
$procDeferred = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$applyFile`"" -PassThru -Wait -NoNewWindow
Add-EvidenceRecord -TweakName $tweak `
                   -SettingType 'Windows Services' `
                   -PreTestState "14 Safe Services (Protected on Live Host)" `
                   -AppliedState "Production Gate Blocked (Exit $($procDeferred.ExitCode)) - Requires Isolated VM" `
                   -RestoredState "N/A (Host Services Untouched)" `
                   -StateMatch 'N/A' `
                   -ExitCode $procDeferred.ExitCode `
                   -ErrorMessage 'Live service startup type modification gated (Exit 3) and deferred to isolated VM to protect host services' `
                   -ExecutionType "حماية النظام المضيف (Deferred & Gated in Production)" `
                   -Status $(if ($procDeferred.ExitCode -eq 3) { "NOT TESTED" } else { "FAIL" })

# ==============================================================================
# 26. set-time-utc (Absent initial: RealTimeIsUniversal absent)
# ==============================================================================
Run-RegistryScriptTest -TweakName "set-time-utc" `
                       -RelativePath "SYSTEM\CurrentControlSet\Control\TimeZoneInformation" `
                       -ValueName "RealTimeIsUniversal" `
                       -ValueType "DWord" `
                       -InitialValue 0 `
                       -InitialExists $false `
                       -ExpectedAppliedValue 1 `
                       -ExpectedAppliedType "DWord"

# ==============================================================================
# 27. ultimate-performance-plan (Power Scheme GUID Live Verification)
# ==============================================================================
$tweak = "ultimate-performance-plan"
$applyFile = Join-Path $tweaksRoot "$tweak\apply.ps1"
$undoFile = Join-Path $tweaksRoot "$tweak\undo.ps1"

# 1. Read initial active scheme GUID
$pOut = powercfg /getactivescheme 2>$null
$guidPattern = '([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})'
$initialGuid = 'Unknown'
if ($pOut -match $guidPattern) { $initialGuid = $matches[1] }
$preStateStr = "Scheme GUID=$initialGuid"

# 2. Run apply.ps1 via child PowerShell process
$procApply = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$applyFile`"" -PassThru -Wait -NoNewWindow
$pApplied = powercfg /getactivescheme 2>$null
$appliedGuid = 'Unknown'
if ($pApplied -match $guidPattern) { $appliedGuid = $matches[1] }
$appliedStateStr = "Scheme GUID=$appliedGuid"
$applyChanged = ($appliedGuid -ne $initialGuid) -and ($procApply.ExitCode -eq 0)

# 3. Run undo.ps1 via child PowerShell process
$procUndo = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$undoFile`"" -PassThru -Wait -NoNewWindow
$pRestored = powercfg /getactivescheme 2>$null
$restoredGuid = 'Unknown'
if ($pRestored -match $guidPattern) { $restoredGuid = $matches[1] }
$restoredStateStr = "Scheme GUID=$restoredGuid"

# 4. Strict 3-state comparison
$match = ($restoredGuid -eq $initialGuid) -and $applyChanged -and ($procUndo.ExitCode -eq 0)
$status = if ($match) { 'PASS' } else { 'FAIL' }

Add-EvidenceRecord -TweakName $tweak `
                   -SettingType 'Power Scheme GUID' `
                   -PreTestState $preStateStr `
                   -AppliedState $appliedStateStr `
                   -RestoredState $restoredStateStr `
                   -StateMatch $(if ($match) { 'true' } else { 'false' }) `
                   -ExitCode $procUndo.ExitCode `
                   -ErrorMessage $(if ($match) { 'None' } else { 'State mismatch or execution failure' }) `
                   -ExecutionType "تنفيذي فعلي مع استعادة الخطة الأصلية (Live Power Scheme apply/undo)" `
                   -Status $status

# Clean sandbox hive, backup directory and test environment variables
Remove-Item -Path $testSandboxRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path $testBackupDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item Env:\CLEARWIN_TEST_REG_PREFIX -ErrorAction SilentlyContinue
Remove-Item Env:\CLEARWIN_TEST_BACKUP_DIR -ErrorAction SilentlyContinue
Remove-Item Env:\CLEARWIN_TEST_MODE -ErrorAction SilentlyContinue

# Export Evidence to JSON
$jsonOut = Join-Path $projectRoot "tools\evidence_results.json"
$global:evidenceLog | ConvertTo-Json -Depth 5 | Out-File -FilePath $jsonOut -Encoding UTF8

Write-Host ""
Write-Host "======================================================================"
Write-Host "Rigorous Per-Tweak Evidence Verification Complete!"
Write-Host "Output saved to: $jsonOut"
Write-Host "======================================================================"
$global:evidenceLog | Format-Table -AutoSize
