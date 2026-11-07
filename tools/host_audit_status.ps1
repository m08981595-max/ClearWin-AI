# Host Audit Status for ClearWin 2.7.2
$projectRoot = Split-Path -Parent $PSScriptRoot

Write-Host "=== 1. Active ClearWin and Test Processes ==="
$activeProcs = Get-Process *ClearWin*, *Setup*, *TiWorker*, *dism* -ErrorAction SilentlyContinue | Select-Object Id, ProcessName, Path, WorkingSet64
if ($activeProcs) {
    $activeProcs | Format-Table -AutoSize
} else {
    Write-Host "No active ClearWin or setup processes running."
}

Write-Host ""
Write-Host "=== 2. System Restore Configuration & Existing Points ==="
try {
    $srService = Get-Service -Name "VSS", "swprv" -ErrorAction SilentlyContinue
    foreach ($s in $srService) {
        Write-Host "Service $($s.Name): $($s.Status) (Startup: $($s.StartType))"
    }

    $restorePoints = Get-ComputerRestorePoint -ErrorAction SilentlyContinue
    if ($restorePoints) {
        Write-Host "Existing Restore Points found: $($restorePoints.Count)"
        $restorePoints | Select-Object SequenceNumber, CreationTime, Description, RestorePointType | Format-Table -AutoSize
    } else {
        Write-Host "No restore points returned by Get-ComputerRestorePoint (or elevation required)."
    }
} catch {
    Write-Host "System restore query note: $($_.Exception.Message)"
}

Write-Host ""
Write-Host "=== 3. Release Artifacts & SHA-256 Fingerprints ==="
$filesToCheck = @(
    "release\ClearWin-Setup-2.7.2.exe",
    "release\ClearWin-Setup-2.7.2-Offline-Candidate.exe",
    "bin\ClearWin.exe"
)
foreach ($f in $filesToCheck) {
    $full = Join-Path $projectRoot $f
    if (Test-Path $full) {
        $item = Get-Item $full
        $h = (Get-FileHash -Path $full -Algorithm SHA256).Hash
        Write-Host "File : $f"
        Write-Host "Size : $($item.Length) bytes ($([math]::Round($item.Length/1MB, 2)) MB)"
        Write-Host "Hash : $h"
        Write-Host "Date : $($item.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))"
        Write-Host ""
    } else {
        Write-Host "File : $f (MISSING)"
    }
}

Write-Host "=== 4. Test Evidence Files Audit ==="
$evidenceFiles = @(
    "tools\evidence_results.json",
    "tools\local_installer_test_result.json",
    "tools\candidate_installer_test_result.json",
    "tools\smoke_test_installed_result.json",
    "tools\uninstaller_test_result.json",
    "D:\ClearWin_Local_Test\install.log",
    "D:\ClearWin_Local_Test\candidate_install.log"
)
foreach ($ef in $evidenceFiles) {
    $efPath = if ($ef -like "D:\*") { $ef } else { Join-Path $projectRoot $ef }
    if (Test-Path $efPath) {
        $item = Get-Item $efPath
        Write-Host "Evidence : $ef"
        Write-Host "Size     : $($item.Length) bytes"
        Write-Host "Modified : $($item.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))"
    } else {
        Write-Host "Evidence : $ef (NOT PRESENT)"
    }
}

Write-Host ""
Write-Host "=== 5. Host Memory and Disk Free Space ==="
$os = Get-CimInstance Win32_OperatingSystem
$freeMB = [math]::Round($os.FreePhysicalMemory / 1KB, 2)
$totalMB = [math]::Round($os.TotalVisibleMemorySize / 1KB, 2)
$driveC = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'"
$driveD = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='D:'"

Write-Host "Total RAM: $totalMB MB ($([math]::Round($totalMB/1024, 2)) GB)"
Write-Host "Free RAM : $freeMB MB ($([math]::Round($freeMB/1024, 2)) GB)"
Write-Host "Free C:  : $([math]::Round($driveC.FreeSpace/1GB, 2)) GB"
Write-Host "Free D:  : $(if ($driveD) { [math]::Round($driveD.FreeSpace/1GB, 2) } else { 0 }) GB"
