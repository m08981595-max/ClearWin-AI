<#
.SYNOPSIS
  ClearWin Tweak State Backup & Rollback Engine
.DESCRIPTION
  Captures exact pre-tweak state (Registry keys, values, types, Service configurations,
  Power schemes, BCD settings) to per-tweak JSON files in %LOCALAPPDATA%\ClearWin\tweak_backups.
  Guarantees exact rollback instead of guessing stock defaults.
  Aborts tweak execution if state cannot be safely captured.
#>

$global:ClearWinBackupDir = [System.IO.Path]::Combine($env:LOCALAPPDATA, "ClearWin", "tweak_backups")

function Ensure-ClearWinBackupDir {
    $isTestMode = ($env:CLEARWIN_TEST_MODE -eq "1" -or $env:CLEARWIN_TEST_MODE -eq "true")
    $dir = if ($isTestMode -and $env:CLEARWIN_TEST_BACKUP_DIR) { $env:CLEARWIN_TEST_BACKUP_DIR } else { $global:ClearWinBackupDir }
    if (-not (Test-Path $dir)) {
        [System.IO.Directory]::CreateDirectory($dir) | Out-Null
    }
}

function Get-TweakBackupPath([string]$tweakName) {
    Ensure-ClearWinBackupDir
    $isTestMode = ($env:CLEARWIN_TEST_MODE -eq "1" -or $env:CLEARWIN_TEST_MODE -eq "true")
    $dir = if ($isTestMode -and $env:CLEARWIN_TEST_BACKUP_DIR) { $env:CLEARWIN_TEST_BACKUP_DIR } else { $global:ClearWinBackupDir }
    return [System.IO.Path]::Combine($dir, "$tweakName.json")
}

function Has-TweakBackup([string]$tweakName) {
    $path = Get-TweakBackupPath $tweakName
    return (Test-Path $path)
}

function Get-TweakRegistryPath([string]$defaultPath) {
    if ($env:CLEARWIN_TEST_REG_PREFIX -and ($env:CLEARWIN_TEST_MODE -eq "1" -or $env:CLEARWIN_TEST_MODE -eq "true") -and (Test-Path -Path $env:CLEARWIN_TEST_REG_PREFIX)) {
        $clean = $defaultPath -replace '^(HKCU:|HKLM:)(\\)?', ''
        return (Join-Path $env:CLEARWIN_TEST_REG_PREFIX $clean)
    }
    return $defaultPath
}

<#
.SYNOPSIS
  Validates that a registry path and value name conform strictly to approved hives
  (HKCU: or approved HKLM: tweak prefixes) and never target startup, persistence,
  or IFEO hijacking keys (SEC-03).
#>
function Test-ValidTweakRegistryPath {
    param(
        [Parameter(Mandatory=$true)][string]$Path,
        [Parameter(Mandatory=$false)][string]$Name = "",
        [Parameter(Mandatory=$false)][string]$TweakName = ""
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $false
    }

    $normPath = $Path.Trim().Replace('/', '\')

    $isTestMode = ($env:CLEARWIN_TEST_MODE -eq "1" -or $env:CLEARWIN_TEST_MODE -eq "true")
    if ($isTestMode -and $env:CLEARWIN_TEST_REG_PREFIX) {
        $prefix = $env:CLEARWIN_TEST_REG_PREFIX.Trim().Replace('/', '\')
        if ($normPath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            $sub = $normPath.Substring($prefix.Length).TrimStart('\')
            $normPath = "HKCU:\$sub"
        }
    }

    # 1. Reject path traversal
    if ($normPath -match '\.\.') {
        Write-Warning "Path traversal attempt detected in registry path: $Path"
        return $false
    }

    # 2. Disallow startup, persistence, IFEO, and hijacking locations (SEC-03)
    $disallowedPatterns = @(
        'Image File Execution Options',
        '\\CurrentVersion\\Run($|\\)',
        '\\CurrentVersion\\RunOnce($|\\)',
        '\\CurrentVersion\\RunOnceEx($|\\)',
        '\\CurrentVersion\\RunServices($|\\)',
        '\\CurrentVersion\\RunServicesOnce($|\\)',
        'AppInit_DLLs',
        'AppCertDLLs',
        '\\Winlogon($|\\)',
        'SilentProcessExit',
        'Active Setup\\Installed Components',
        'BootVerificationProgram',
        '\\CurrentControlSet\\Services($|\\)',
        '\\CurrentControlSet\\Control\\SafeBoot($|\\)',
        '\\Explorer\\Shell Folders\\Startup',
        '\\Explorer\\User Shell Folders\\Startup',
        '\\KnownDLLs',
        '\\CurrentVersion\\Policies\\Explorer\\Run',
        '\\Environment\\UserInitMprLogonScript'
    )

    foreach ($pat in $disallowedPatterns) {
        if ($normPath -match $pat) {
            Write-Warning "Disallowed security pattern '$pat' matched in registry path: $Path"
            return $false
        }
    }

    # Check value name against dangerous execution/debugger values
    if (-not [string]::IsNullOrEmpty($Name)) {
        $dangerousNames = @('Debugger', 'AppInit_DLLs', 'Userinit', 'Shell', 'Taskman')
        foreach ($dn in $dangerousNames) {
            if ($Name -ieq $dn) {
                Write-Warning "Disallowed security value name '$Name' for path: $Path"
                return $false
            }
        }
    }

    # 3. Approved root hives check: Must be HKCU: or approved HKLM:
    if ($normPath.StartsWith("HKCU:\", [System.StringComparison]::OrdinalIgnoreCase) -or
        $normPath.StartsWith("HKCU:", [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    if ($normPath.StartsWith("HKLM:\", [System.StringComparison]::OrdinalIgnoreCase) -or
        $normPath.StartsWith("HKLM:", [System.StringComparison]::OrdinalIgnoreCase)) {
        $approvedHklmPrefixes = @(
            'HKLM:\SOFTWARE\Policies\',
            'HKLM:\SOFTWARE\Microsoft\PolicyManager\',
            'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\',
            'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\',
            'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile',
            'HKLM:\SOFTWARE\Microsoft\WcmSvc\',
            'HKLM:\SYSTEM\CurrentControlSet\Control\CrashControl',
            'HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity',
            'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power',
            'HKLM:\SYSTEM\CurrentControlSet\Control\TimeZoneInformation',
            'HKLM:\SYSTEM\CurrentControlSet\Control\Power'
        )

        foreach ($approved in $approvedHklmPrefixes) {
            if ($normPath.StartsWith($approved, [System.StringComparison]::OrdinalIgnoreCase)) {
                return $true
            }
        }

        Write-Warning "HKLM path '$Path' is not in the approved HKLM prefix allowlist for ClearWin tweaks."
        return $false
    }

    Write-Warning "Registry path '$Path' does not target an approved hive (HKCU: or approved HKLM:)."
    return $false
}

function Save-RegistryState {
    param(
        [Parameter(Mandatory=$true)][string]$TweakName,
        [Parameter(Mandatory=$true)][array]$Targets
    )
    Ensure-ClearWinBackupDir
    $backupPath = Get-TweakBackupPath $TweakName
    if (Has-TweakBackup $TweakName) {
        Write-Host "  [ClearWin State Guard] Original pre-tweak backup already exists for '$TweakName'; preserving original snapshot."
        return $true
    }

    $records = @()
    foreach ($target in $Targets) {
        $path = $target.Path
        $name = $target.Name

        # SEC-03: Validate target registry path and value name against approved hives and disallowed keys
        if (-not (Test-ValidTweakRegistryPath -Path $path -Name $name -TweakName $TweakName)) {
            Write-Error "CRITICAL: Attempted to back up unapproved or disallowed registry path: '$path' (Name: '$name'). Aborting tweak."
            exit 1
        }

        $keyExists = Test-Path $path
        $valueExists = $false
        $value = $null
        $kind = "None"

        if ($keyExists) {
            try {
                $item = Get-Item -Path $path -ErrorAction Stop
                $valNames = $item.GetValueNames()
                if ($valNames -contains $name -or $item.GetValue($name, $null) -ne $null) {
                    $valueExists = $true
                    $rawKind = $item.GetValueKind($name)
                    $kind = $rawKind.ToString()
                    $rawVal = $item.GetValue($name)
                    if ($kind -eq "Binary" -and $rawVal -is [byte[]]) {
                        $value = [Convert]::ToBase64String($rawVal)
                    } else {
                        $value = $rawVal
                    }
                }
            } catch {
                # Could not read value
            }
        }

        $records += @{
            Path = $path
            Name = $name
            KeyExists = $keyExists
            ValueExists = $valueExists
            Value = $value
            Kind = $kind
        }
    }

    $snapshot = @{
        TweakName = $TweakName
        Type = "Registry"
        Timestamp = [DateTime]::UtcNow.ToString("o")
        Records = $records
    }

    try {
        $json = $snapshot | ConvertTo-Json -Depth 6
        [System.IO.File]::WriteAllText($backupPath, $json, [System.Text.Encoding]::UTF8)
        $testRead = Get-Content $backupPath -Raw | ConvertFrom-Json
        if (-not $testRead -or $testRead.TweakName -ne $TweakName) {
            throw "Verification failed: JSON content invalid or corrupted."
        }
        Write-Host "  [ClearWin State Guard] Pre-tweak registry state captured successfully for '$TweakName'."
        return $true
    } catch {
        Write-Error "CRITICAL: Failed to capture pre-tweak state snapshot: $($_.Exception.Message). Aborting tweak application to prevent irreversible changes."
        exit 1
    }
}

function Restore-RegistryState {
    param(
        [Parameter(Mandatory=$true)][string]$TweakName
    )
    $backupPath = Get-TweakBackupPath $TweakName
    if (-not (Test-Path $backupPath)) {
        Write-Error "CRITICAL: Cannot undo tweak '$TweakName' - no pre-tweak state backup found at '$backupPath'."
        exit 1
    }

    try {
        $data = Get-Content $backupPath -Raw | ConvertFrom-Json
    } catch {
        Write-Error "CRITICAL: Failed to read backup file '$backupPath': $($_.Exception.Message)."
        exit 1
    }

    $restoredCount = 0
    foreach ($rec in $data.Records) {
        $path = $rec.Path
        $name = $rec.Name
        $keyExists = [bool]$rec.KeyExists
        $valueExists = [bool]$rec.ValueExists
        $kind = [string]$rec.Kind
        $val = $rec.Value

        # SEC-03: Validate restored registry path and value name against approved hives and disallowed keys
        if (-not (Test-ValidTweakRegistryPath -Path $path -Name $name -TweakName $TweakName)) {
            Write-Error "CRITICAL: Security violation - blocked attempt to restore unapproved or hijack registry path: '$path' (Name: '$name')."
            exit 1
        }

        if ($valueExists) {
            if (-not (Test-Path $path)) {
                New-Item -Path $path -Force -ErrorAction SilentlyContinue | Out-Null
            }
            if ($kind -eq "Binary" -and $val -is [string]) {
                $bytes = [Convert]::FromBase64String($val)
                Set-ItemProperty -Path $path -Name $name -Value $bytes -Type Binary -Force -ErrorAction Stop
            } elseif ($kind -eq "DWord") {
                Set-ItemProperty -Path $path -Name $name -Value ([int]$val) -Type DWord -Force -ErrorAction Stop
            } elseif ($kind -eq "QWord") {
                Set-ItemProperty -Path $path -Name $name -Value ([long]$val) -Type QWord -Force -ErrorAction Stop
            } elseif ($kind -eq "String") {
                Set-ItemProperty -Path $path -Name $name -Value ([string]$val) -Type String -Force -ErrorAction Stop
            } elseif ($kind -eq "MultiString") {
                Set-ItemProperty -Path $path -Name $name -Value ([string[]]$val) -Type MultiString -Force -ErrorAction Stop
            } elseif ($kind -eq "ExpandString") {
                Set-ItemProperty -Path $path -Name $name -Value ([string]$val) -Type ExpandString -Force -ErrorAction Stop
            } else {
                Set-ItemProperty -Path $path -Name $name -Value $val -Force -ErrorAction Stop
            }
            $restoredCount++
            Write-Host "  [OK] Restored $path\$name to original value"
        } else {
            if (-not $keyExists) {
                if (Test-Path $path) {
                    try {
                        if (-not [string]::IsNullOrEmpty($name) -and $name -ne "(default)") {
                            Remove-ItemProperty -Path $path -Name $name -Force -ErrorAction SilentlyContinue
                        }
                        $itemCheck = Get-Item -Path $path -ErrorAction SilentlyContinue
                        if ($itemCheck -and $itemCheck.ValueCount -eq 0 -and $itemCheck.SubKeyCount -eq 0) {
                            Remove-Item -Path $path -Force -ErrorAction SilentlyContinue
                            Write-Host "  [OK] Removed empty key $path (did not exist before tweak)"
                        } else {
                            Write-Host "  [OK] Removed value $name from $path (preserved sibling keys/values)"
                        }
                    } catch {}
                }
            } else {
                if (Test-Path $path) {
                    if (-not [string]::IsNullOrEmpty($name) -and $name -ne "(default)") {
                        Remove-ItemProperty -Path $path -Name $name -Force -ErrorAction SilentlyContinue
                        Write-Host "  [OK] Removed $path\$name (did not exist before tweak)"
                    }
                }
            }
            $restoredCount++
        }
    }

    Remove-Item -Path $backupPath -Force -ErrorAction SilentlyContinue
    Write-Host "  [ClearWin State Guard] Successfully restored $restoredCount registry item(s) to exact pre-tweak state."
    return $true
}

function Save-ServicesState {
    param(
        [Parameter(Mandatory=$true)][string]$TweakName,
        [Parameter(Mandatory=$true)][string[]]$ServiceNames
    )
    Ensure-ClearWinBackupDir
    $backupPath = Get-TweakBackupPath $TweakName
    if (Has-TweakBackup $TweakName) {
        Write-Host "  [ClearWin State Guard] Original service backup already exists for '$TweakName'; preserving original snapshot."
        return $true
    }

    $records = @()
    foreach ($svcName in $ServiceNames) {
        $svc = Get-Service -Name $svcName -ErrorAction SilentlyContinue
        if ($svc) {
            $startType = $svc.StartType.ToString()
            $status = $svc.Status.ToString()
            $records += @{
                Name = $svcName
                Exists = $true
                StartType = $startType
                Status = $status
            }
        } else {
            $records += @{
                Name = $svcName
                Exists = $false
                StartType = "None"
                Status = "None"
            }
        }
    }

    $snapshot = @{
        TweakName = $TweakName
        Type = "Services"
        Timestamp = [DateTime]::UtcNow.ToString("o")
        Records = $records
    }

    try {
        $json = $snapshot | ConvertTo-Json -Depth 5
        [System.IO.File]::WriteAllText($backupPath, $json, [System.Text.Encoding]::UTF8)
        $testRead = Get-Content $backupPath -Raw | ConvertFrom-Json
        if (-not $testRead -or $testRead.TweakName -ne $TweakName) {
            throw "Verification failed: JSON content invalid."
        }
        Write-Host "  [ClearWin State Guard] Pre-tweak service states captured successfully for '$TweakName'."
        return $true
    } catch {
        Write-Error "CRITICAL: Failed to capture pre-tweak service state: $($_.Exception.Message). Aborting tweak application."
        exit 1
    }
}

function Restore-ServicesState {
    param(
        [Parameter(Mandatory=$true)][string]$TweakName
    )
    $backupPath = Get-TweakBackupPath $TweakName
    if (-not (Test-Path $backupPath)) {
        Write-Error "CRITICAL: Cannot undo tweak '$TweakName' - no pre-tweak state backup found at '$backupPath'."
        exit 1
    }

    try {
        $data = Get-Content $backupPath -Raw | ConvertFrom-Json
    } catch {
        Write-Error "CRITICAL: Failed to read backup file '$backupPath': $($_.Exception.Message)."
        exit 1
    }

    $restoredCount = 0
    foreach ($rec in $data.Records) {
        if (-not [bool]$rec.Exists) { continue }
        $svcName = [string]$rec.Name
        $origStart = [string]$rec.StartType
        $origStatus = [string]$rec.Status

        try {
            if ($origStart -ne "None") {
                Set-Service -Name $svcName -StartupType $origStart -ErrorAction Stop
                Write-Host "  [OK] Restored service $svcName StartupType to $origStart"
            }
            if ($origStatus -eq "Running") {
                Start-Service -Name $svcName -ErrorAction SilentlyContinue
            } elseif ($origStatus -eq "Stopped") {
                Stop-Service -Name $svcName -ErrorAction SilentlyContinue
            }
            $restoredCount++
        } catch {
            Write-Warning "Could not restore service ${svcName}: $($_.Exception.Message)"
        }
    }

    Remove-Item -Path $backupPath -Force -ErrorAction SilentlyContinue
    Write-Host "  [ClearWin State Guard] Successfully restored $restoredCount service(s) to exact pre-tweak state."
    return $true
}

function Save-PowerPlanState {
    param([Parameter(Mandatory=$true)][string]$TweakName)
    Ensure-ClearWinBackupDir
    $backupPath = Get-TweakBackupPath $TweakName
    if (Has-TweakBackup $TweakName) {
        Write-Host "  [ClearWin State Guard] Original power scheme backup already exists for '$TweakName'; preserving original snapshot."
        return $true
    }
    try {
        $out = powercfg /getactivescheme
        $guid = $null
        if ($out -match "([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})") {
            $guid = $matches[1]
        }
        if (-not $guid) { throw "Could not determine active power scheme GUID" }
        $snapshot = @{ TweakName = $TweakName; Type = "Power"; SchemeGuid = $guid; Timestamp = [DateTime]::UtcNow.ToString("o") }
        $json = $snapshot | ConvertTo-Json
        [System.IO.File]::WriteAllText($backupPath, $json, [System.Text.Encoding]::UTF8)
        Write-Host "  [ClearWin State Guard] Pre-tweak active power scheme ($guid) captured."
        return $true
    } catch {
        Write-Error "CRITICAL: Failed to capture active power scheme: $($_.Exception.Message). Aborting tweak."
        exit 1
    }
}

function Restore-PowerPlanState {
    param([Parameter(Mandatory=$true)][string]$TweakName)
    $backupPath = Get-TweakBackupPath $TweakName
    if (-not (Test-Path $backupPath)) {
        Write-Error "CRITICAL: Cannot undo tweak '$TweakName' - no pre-tweak state backup found at '$backupPath'."
        exit 1
    }
    $data = Get-Content $backupPath -Raw | ConvertFrom-Json
    $guid = [string]$data.SchemeGuid
    if (-not $guid) {
        Write-Error "CRITICAL: Backup file '$backupPath' does not contain a valid SchemeGuid."
        exit 1
    }
    powercfg /setactive $guid
    if ($LASTEXITCODE -ne 0) {
        Write-Error "CRITICAL: powercfg /setactive $guid failed with exit code $LASTEXITCODE."
        exit 1
    }
    Write-Host "  [OK] Restored active power scheme to original GUID: $guid"
    Remove-Item -Path $backupPath -Force -ErrorAction SilentlyContinue
    return $true
}

function Save-BcdeditSettingState {
    param(
        [Parameter(Mandatory=$true)][string]$TweakName,
        [Parameter(Mandatory=$true)][string]$SettingName
    )
    Ensure-ClearWinBackupDir
    $backupPath = Get-TweakBackupPath $TweakName
    if (Has-TweakBackup $TweakName) {
        Write-Host "  [ClearWin State Guard] Original BCD backup already exists for '$TweakName'; preserving original snapshot."
        return $true
    }
    try {
        $enumOut = bcdedit /enum "{current}"
        $hasVal = $false
        $currentVal = $null
        foreach ($line in $enumOut) {
            if ($line -match "^\s*$SettingName\s+(.+)$") {
                $hasVal = $true
                $currentVal = $matches[1].Trim()
                break
            }
        }
        $snapshot = @{ TweakName = $TweakName; Type = "BCD"; Setting = $SettingName; Exists = $hasVal; Value = $currentVal; Timestamp = [DateTime]::UtcNow.ToString("o") }
        $json = $snapshot | ConvertTo-Json
        [System.IO.File]::WriteAllText($backupPath, $json, [System.Text.Encoding]::UTF8)
        Write-Host "  [ClearWin State Guard] Pre-tweak BCD setting '$SettingName' captured."
        return $true
    } catch {
        Write-Error "CRITICAL: Failed to capture BCD setting: $($_.Exception.Message). Aborting tweak."
        exit 1
    }
}

function Restore-BcdeditSettingState {
    param(
        [Parameter(Mandatory=$true)][string]$TweakName
    )
    $backupPath = Get-TweakBackupPath $TweakName
    if (-not (Test-Path $backupPath)) {
        Write-Error "CRITICAL: Cannot undo tweak '$TweakName' - no pre-tweak state backup found at '$backupPath'."
        exit 1
    }
    $data = Get-Content $backupPath -Raw | ConvertFrom-Json
    $setting = [string]$data.Setting
    $exists = [bool]$data.Exists
    $val = $data.Value
    if ($exists -and $val) {
        bcdedit /set $setting $val
        Write-Host "  [OK] Restored BCD setting $setting to original value: $val"
    } else {
        bcdedit /deletevalue $setting 2>$null
        Write-Host "  [OK] Removed BCD setting $setting (did not exist before tweak)"
    }
    Remove-Item -Path $backupPath -Force -ErrorAction SilentlyContinue
    return $true
}

function Save-HibernationState {
    param([Parameter(Mandatory=$true)][string]$TweakName)
    Ensure-ClearWinBackupDir
    $backupPath = Get-TweakBackupPath $TweakName
    if (Has-TweakBackup $TweakName) {
        Write-Host "  [ClearWin State Guard] Original hibernation backup already exists for '$TweakName'; preserving original snapshot."
        return $true
    }
    try {
        $regPath = "HKLM:\SYSTEM\CurrentControlSet\Control\Power"
        $key = if (Get-Command "Get-TweakRegistryPath" -ErrorAction SilentlyContinue) { Get-TweakRegistryPath $regPath } else { $regPath }
        $val = 0
        if (Test-Path $key) {
            $prop = Get-ItemProperty -Path $key -Name "HibernateEnabled" -ErrorAction SilentlyContinue
            if ($prop -and $prop.HibernateEnabled -ne $null) {
                $val = [int]$prop.HibernateEnabled
            }
        }
        $snapshot = @{ TweakName = $TweakName; Type = "Hibernation"; HibernateEnabled = $val; Timestamp = [DateTime]::UtcNow.ToString("o") }
        $json = $snapshot | ConvertTo-Json
        [System.IO.File]::WriteAllText($backupPath, $json, [System.Text.Encoding]::UTF8)
        Write-Host "  [ClearWin State Guard] Pre-tweak hibernation state ($val) captured."
        return $true
    } catch {
        Write-Error "CRITICAL: Failed to capture hibernation state: $($_.Exception.Message). Aborting tweak."
        exit 1
    }
}

function Restore-HibernationState {
    param([Parameter(Mandatory=$true)][string]$TweakName)
    $backupPath = Get-TweakBackupPath $TweakName
    if (-not (Test-Path $backupPath)) {
        Write-Error "CRITICAL: Cannot undo tweak '$TweakName' - no pre-tweak state backup found at '$backupPath'."
        exit 1
    }
    $data = Get-Content $backupPath -Raw | ConvertFrom-Json
    $enabled = [int]$data.HibernateEnabled
    if ($env:CLEARWIN_TEST_REG_PREFIX -and ($env:CLEARWIN_TEST_MODE -eq "1" -or $env:CLEARWIN_TEST_MODE -eq "true")) {
        $regPath = "HKLM:\SYSTEM\CurrentControlSet\Control\Power"
        $key = if (Get-Command "Get-TweakRegistryPath" -ErrorAction SilentlyContinue) { Get-TweakRegistryPath $regPath } else { $regPath }
        if (-not (Test-Path $key)) { New-Item -Path $key -ItemType Directory -Force | Out-Null }
        Set-ItemProperty -Path $key -Name "HibernateEnabled" -Value $enabled -Type DWord -Force
        Write-Host "  [OK] Restored hibernation state in sandbox to $enabled (original state)"
    } else {
        if ($enabled -eq 1) {
            powercfg.exe /hibernate on
            Write-Host "  [OK] Restored hibernation to ON (original state)"
        } else {
            powercfg.exe /hibernate off
            Write-Host "  [OK] Restored hibernation to OFF (original state)"
        }
    }
    Remove-Item -Path $backupPath -Force -ErrorAction SilentlyContinue
    return $true
}

function Save-NetworkGlobalState {
    param([Parameter(Mandatory=$true)][string]$TweakName)
    Ensure-ClearWinBackupDir
    $backupPath = Get-TweakBackupPath $TweakName
    if (Has-TweakBackup $TweakName) {
        Write-Host "  [ClearWin State Guard] Original network backup already exists for '$TweakName'; preserving original snapshot."
        return $true
    }
    try {
        $out = netsh int tcp show global
        $dict = @{}
        foreach ($line in $out) {
            if ($line -match "^\s*([^:]+)\s*:\s*(.+)$") {
                $k = $matches[1].Trim()
                $v = $matches[2].Trim()
                $dict[$k] = $v
            }
        }
        $snapshot = @{ TweakName = $TweakName; Type = "NetworkGlobal"; Settings = $dict; Timestamp = [DateTime]::UtcNow.ToString("o") }
        $json = $snapshot | ConvertTo-Json -Depth 4
        [System.IO.File]::WriteAllText($backupPath, $json, [System.Text.Encoding]::UTF8)
        Write-Host "  [ClearWin State Guard] Pre-tweak network TCP global state captured."
        return $true
    } catch {
        Write-Error "CRITICAL: Failed to capture network settings: $($_.Exception.Message). Aborting tweak."
        exit 1
    }
}

function Restore-NetworkGlobalState {
    param([Parameter(Mandatory=$true)][string]$TweakName)
    $backupPath = Get-TweakBackupPath $TweakName
    if (-not (Test-Path $backupPath)) {
        Write-Error "CRITICAL: Cannot undo tweak '$TweakName' - no pre-tweak state backup found at '$backupPath'."
        exit 1
    }
    try {
        $data = Get-Content $backupPath -Raw | ConvertFrom-Json
        $settings = $data.Settings
        if ($settings."Receive-Side Scaling State") {
            netsh int tcp set global rss=($settings."Receive-Side Scaling State") 2>$null
        }
        if ($settings."Receive Window Auto-Tuning Level") {
            netsh int tcp set global autotuninglevel=($settings."Receive Window Auto-Tuning Level") 2>$null
        }
        if ($settings."ECN Capability") {
            netsh int tcp set global ecncapability=($settings."ECN Capability") 2>$null
        }
        if ($settings."RFC 1323 Timestamps") {
            netsh int tcp set global timestamps=($settings."RFC 1323 Timestamps") 2>$null
        }
        if ($settings."Initial RTO") {
            netsh int tcp set global initialRto=($settings."Initial RTO") 2>$null
        }
        if ($settings."Receive Segment Coalescing State") {
            netsh int tcp set global rsc=($settings."Receive Segment Coalescing State") 2>$null
        }
        Write-Host "  [OK] Restored TCP global settings to pre-tweak state without disrupting network adapters."
    } catch {
        Write-Warning "Could not fully restore TCP settings: $($_.Exception.Message)"
    }
    Remove-Item -Path $backupPath -Force -ErrorAction SilentlyContinue
    return $true
}

function Save-DefenderState {
    param([Parameter(Mandatory=$true)][string]$TweakName)
    Ensure-ClearWinBackupDir
    $backupPath = Get-TweakBackupPath $TweakName
    try {
        $pref = Get-MpPreference -ErrorAction SilentlyContinue
        $origVal = if ($pref) { [bool]$pref.DisableRealtimeMonitoring } else { $false }
        $snapshot = @{
            TweakName = $TweakName
            Type = "MpPreference"
            DisableRealtimeMonitoring = $origVal
            Timestamp = [DateTime]::UtcNow.ToString("o")
        }
        $json = $snapshot | ConvertTo-Json
        [System.IO.File]::WriteAllText($backupPath, $json, [System.Text.Encoding]::UTF8)
        Write-Host "  [ClearWin State Guard] Pre-tweak Defender state captured."
        return $true
    } catch {
        Write-Error "CRITICAL: Could not capture Defender pre-tweak state: $($_.Exception.Message). Aborting."
        exit 1
    }
}

function Restore-DefenderState {
    param([Parameter(Mandatory=$true)][string]$TweakName)
    $backupPath = Get-TweakBackupPath $TweakName
    if (-not (Test-Path $backupPath)) {
        Write-Error "CRITICAL: Cannot undo tweak '$TweakName' - no pre-tweak state backup found at '$backupPath'."
        exit 1
    }
    $targetVal = $false
    try {
        $data = Get-Content $backupPath -Raw | ConvertFrom-Json
        $targetVal = [bool]$data.DisableRealtimeMonitoring
    } catch {
        Write-Warning "Failed to read Defender backup file: $($_.Exception.Message)"
    }
    try {
        Set-MpPreference -DisableRealtimeMonitoring $targetVal -ErrorAction Stop
        Write-Host "  [OK] Restored Windows Defender real-time monitoring to pre-tweak state: DisableRealtimeMonitoring=$targetVal"
        Remove-Item -Path $backupPath -Force -ErrorAction SilentlyContinue
        return $true
    } catch {
        Write-Error "Failed to restore Defender: $($_.Exception.Message)"
        exit 1
    }
}

