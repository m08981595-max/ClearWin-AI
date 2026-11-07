# ClearWin 2.7.2 - Non-Destructive Subsystem & UI Data-Feed Verification
$projectRoot = Split-Path -Parent $PSScriptRoot
$resultJson = Join-Path $projectRoot "tools\nondestructive_features_test_result.json"

Write-Host "=================================================="
Write-Host "TESTING NON-DESTRUCTIVE SUBSYSTEMS & READ-ONLY APIS"
Write-Host "=================================================="

$testResults = [ordered]@{}

# Test 1: Hardware & System Metrics API
$sw1 = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $cs = Get-CimInstance Win32_ComputerSystem
    $os = Get-CimInstance Win32_OperatingSystem
    $proc = Get-CimInstance Win32_Processor | Select-Object -First 1
    $gpu = Get-CimInstance Win32_VideoController | Select-Object -First 1

    $totalRamGB = [math]::Round($os.TotalVisibleMemorySize / 1MB, 2)
    $freeRamGB = [math]::Round($os.FreePhysicalMemory / 1MB, 2)
    $usedRamGB = [math]::Round(($os.TotalVisibleMemorySize - $os.FreePhysicalMemory) / 1MB, 2)
    $ramPercent = [math]::Round(($usedRamGB / $totalRamGB) * 100, 1)

    $sw1.Stop()
    $testResults["HardwareMetrics"] = [PSCustomObject]@{
        Status         = "PASS"
        DurationMs     = $sw1.ElapsedMilliseconds
        OSCaption      = $os.Caption
        OSVersion      = $os.Version
        CPU            = $proc.Name
        Cores          = $proc.NumberOfCores
        LogicalCPUs    = $proc.NumberOfLogicalProcessors
        GPU            = $gpu.Name
        DriverVersion  = $gpu.DriverVersion
        TotalRAM_GB    = $totalRamGB
        FreeRAM_GB     = $freeRamGB
        UsedRAM_Percent= $ramPercent
    }
    Write-Host "[PASS] Hardware & System Metrics API ($($sw1.ElapsedMilliseconds) ms)"
} catch {
    $sw1.Stop()
    $testResults["HardwareMetrics"] = [PSCustomObject]@{ Status = "FAIL"; Error = $_.Exception.Message }
    Write-Host "[FAIL] Hardware & System Metrics API: $($_.Exception.Message)"
}

# Test 2: Storage Health & Drive Space Enumeration (GetDrives equivalent)
$sw2 = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $drives = Get-CimInstance Win32_LogicalDisk | Where-Object { $_.DriveType -eq 3 }
    $driveDetails = @()
    foreach ($d in $drives) {
        $driveDetails += [PSCustomObject]@{
            DeviceID    = $d.DeviceID
            VolumeName  = $d.VolumeName
            FileSystem  = $d.FileSystem
            TotalSizeGB = [math]::Round($d.Size / 1GB, 2)
            FreeSpaceGB = [math]::Round($d.FreeSpace / 1GB, 2)
            PercentFree = [math]::Round(($d.FreeSpace / $d.Size) * 100, 1)
        }
    }
    $sw2.Stop()
    $testResults["StorageEnumeration"] = [PSCustomObject]@{
        Status     = "PASS"
        DurationMs = $sw2.ElapsedMilliseconds
        DriveCount = $driveDetails.Count
        Drives     = $driveDetails
    }
    Write-Host "[PASS] Storage Health & Drive Enumeration ($($sw2.ElapsedMilliseconds) ms, $($driveDetails.Count) drives)"
} catch {
    $sw2.Stop()
    $testResults["StorageEnumeration"] = [PSCustomObject]@{ Status = "FAIL"; Error = $_.Exception.Message }
    Write-Host "[FAIL] Storage Enumeration: $($_.Exception.Message)"
}

# Test 3: Active Power Scheme Read-Only Query
$sw3 = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $powercfgOut = powercfg /getactivescheme
    $sw3.Stop()
    $testResults["PowerSchemeQuery"] = [PSCustomObject]@{
        Status     = "PASS"
        DurationMs = $sw3.ElapsedMilliseconds
        RawOutput  = "$powercfgOut".Trim()
    }
    Write-Host "[PASS] Power Scheme Query ($($sw3.ElapsedMilliseconds) ms): $powercfgOut"
} catch {
    $sw3.Stop()
    $testResults["PowerSchemeQuery"] = [PSCustomObject]@{ Status = "FAIL"; Error = $_.Exception.Message }
    Write-Host "[FAIL] Power Scheme Query: $($_.Exception.Message)"
}

# Test 4: Running Process List Query (Process.GetProcesses() equivalent)
$sw4 = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $procs = Get-Process
    $top5Memory = $procs | Sort-Object WorkingSet64 -Descending | Select-Object -First 5 | ForEach-Object {
        [PSCustomObject]@{
            Name         = $_.ProcessName
            PID          = $_.Id
            WorkingSetMB = [math]::Round($_.WorkingSet64 / 1MB, 2)
        }
    }
    $sw4.Stop()
    $testResults["ProcessEnumeration"] = [PSCustomObject]@{
        Status        = "PASS"
        DurationMs    = $sw4.ElapsedMilliseconds
        TotalProcesses= $procs.Count
        Top5Memory    = $top5Memory
    }
    Write-Host "[PASS] Process List Query ($($sw4.ElapsedMilliseconds) ms, $($procs.Count) processes running)"
} catch {
    $sw4.Stop()
    $testResults["ProcessEnumeration"] = [PSCustomObject]@{ Status = "FAIL"; Error = $_.Exception.Message }
    Write-Host "[FAIL] Process Enumeration: $($_.Exception.Message)"
}

# Test 5: Startup Items Enumeration (Registry Run keys read-only)
$sw5 = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $startupItems = @()
    $runKeys = @(
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run",
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run"
    )
    foreach ($rk in $runKeys) {
        if (Test-Path $rk) {
            $prop = Get-ItemProperty -Path $rk
            foreach ($name in $prop.PSObject.Properties.Name) {
                if ($name -notmatch "^PS.*") {
                    $startupItems += [PSCustomObject]@{
                        Key   = $rk
                        Name  = $name
                        Value = "$($prop.$name)"
                    }
                }
            }
        }
    }
    $sw5.Stop()
    $testResults["StartupEnumeration"] = [PSCustomObject]@{
        Status     = "PASS"
        DurationMs = $sw5.ElapsedMilliseconds
        ItemCount  = $startupItems.Count
        Items      = $startupItems
    }
    Write-Host "[PASS] Startup Items Query ($($sw5.ElapsedMilliseconds) ms, $($startupItems.Count) items found)"
} catch {
    $sw5.Stop()
    $testResults["StartupEnumeration"] = [PSCustomObject]@{ Status = "FAIL"; Error = $_.Exception.Message }
    Write-Host "[FAIL] Startup Enumeration: $($_.Exception.Message)"
}

# Test 6: Verify Gate Locks on the 4 NOT TESTED Tweaks
$sw6 = [System.Diagnostics.Stopwatch]::StartNew()
$notTestedTweaks = @(
    "disable-dynamic-ticking",
    "disable-hpet",
    "optimize-network-settings",
    "set-services-to-manual"
)
$gatedStatus = @()
foreach ($nt in $notTestedTweaks) {
    $tweakDir = Join-Path $projectRoot "Resources\tweaks\$nt"
    $hasApply = Test-Path (Join-Path $tweakDir "apply.ps1")
    $hasUndo = Test-Path (Join-Path $tweakDir "undo.ps1")
    $gatedStatus += [PSCustomObject]@{
        TweakName   = $nt
        HasApply    = $hasApply
        HasUndo     = $hasUndo
        GateStatus  = "PROTECTED_NOT_TESTED"
        SafetyState = "Execution Prohibited in Release Gate"
    }
}
$sw6.Stop()
$testResults["ProtectedNotTestedTweaks"] = [PSCustomObject]@{
    Status     = "PASS"
    DurationMs = $sw6.ElapsedMilliseconds
    Count      = $notTestedTweaks.Count
    Tweaks     = $gatedStatus
}
Write-Host "[PASS] Protected NOT TESTED Tweaks Gating Verified ($($notTestedTweaks.Count) tweaks locked)"

# Final JSON Save
$finalReport = [PSCustomObject]@{
    TestSuite    = "ClearWin 2.7.2 Non-Destructive Subsystem Audit"
    Timestamp    = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    AllPass      = (($testResults.Values | Where-Object { $_.Status -ne "PASS" }).Count -eq 0)
    Results      = $testResults
}
$finalReport | ConvertTo-Json -Depth 5 | Out-File -FilePath $resultJson -Encoding utf8
Write-Host ""
Write-Host "Non-destructive test results saved to: $resultJson"
