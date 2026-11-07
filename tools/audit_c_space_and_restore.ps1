# Audit C: Drive Space Drop and System Protection Status
$ErrorActionPreference = 'SilentlyContinue'

Write-Host "=================================================="
Write-Host "1. AUDITING C: DRIVE SPACE DROP (16.71 GB -> 13.99 GB)"
Write-Host "=================================================="

$driveC = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'"
$totalC_GB = [math]::Round($driveC.Size / 1GB, 2)
$freeC_GB = [math]::Round($driveC.FreeSpace / 1GB, 2)
Write-Host "Drive C: Total Size = $totalC_GB GB | Current Free = $freeC_GB GB"

# 1.1 User Temp folder
$userTemp = [System.IO.Path]::GetTempPath()
Write-Host "Checking User Temp: $userTemp"
$userTempItems = Get-ChildItem -Path $userTemp -Recurse -Force -ErrorAction SilentlyContinue
$userTempSize = ($userTempItems | Measure-Object -Property Length -Sum).Sum
Write-Host "User Temp Total Size: $([math]::Round($userTempSize / 1MB, 2)) MB ($([math]::Round($userTempSize / 1GB, 2)) GB) - Files: $($userTempItems.Count)"

# Top large items in User Temp
$topUserTemp = $userTempItems | Where-Object { -not $_.PSIsContainer } | Sort-Object Length -Descending | Select-Object -First 10
Write-Host "Top items in User Temp:"
foreach ($item in $topUserTemp) {
    Write-Host "  - $([math]::Round($item.Length / 1MB, 2)) MB: $($item.FullName)"
}

# Inno Setup / DISM specific temp directories
$isTempDirs = Get-ChildItem -Path $userTemp -Filter "is-*.tmp" -Directory
Write-Host "Inno Setup temp directories in User Temp: $($isTempDirs.Count)"
foreach ($d in $isTempDirs) {
    $dSize = (Get-ChildItem $d.FullName -Recurse | Measure-Object -Property Length -Sum).Sum
    Write-Host "  - $($d.Name): $([math]::Round($dSize/1MB, 2)) MB"
}

# 1.2 Windows Temp folder
$winTemp = "C:\Windows\Temp"
if (Test-Path $winTemp) {
    $winTempItems = Get-ChildItem -Path $winTemp -Recurse -Force -ErrorAction SilentlyContinue
    $winTempSize = ($winTempItems | Measure-Object -Property Length -Sum).Sum
    Write-Host "Windows Temp Total Size: $([math]::Round($winTempSize / 1MB, 2)) MB"
}

# 1.3 Windows Servicing and Logs
$cbsLog = "C:\Windows\Logs\CBS"
if (Test-Path $cbsLog) {
    $cbsSize = (Get-ChildItem $cbsLog -Recurse -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    Write-Host "C:\Windows\Logs\CBS Size: $([math]::Round($cbsSize / 1MB, 2)) MB"
}

$dismLog = "C:\Windows\Logs\DISM"
if (Test-Path $dismLog) {
    $dismSize = (Get-ChildItem $dismLog -Recurse -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    Write-Host "C:\Windows\Logs\DISM Size: $([math]::Round($dismSize / 1MB, 2)) MB"
}

# 1.4 Windows Servicing / WinSxS staged package additions
Write-Host ""
Write-Host "1.4 Servicing & Feature Packages Impact:"
$cbsKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\Packages"
$sandboxPkgs = Get-ChildItem -Path $cbsKey -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -like "*Containers-DisposableClientVM*" }
Write-Host "Containers-DisposableClientVM CBS Packages registered: $($sandboxPkgs.Count)"
Write-Host "Note: Staging feature packages via DISM unpacked hypervisor container components into WinSxS."

Write-Host ""
Write-Host "=================================================="
Write-Host "2. AUDITING SYSTEM PROTECTION & RESTORE POINTS"
Write-Host "=================================================="

$srPoints = Get-ComputerRestorePoint -ErrorAction SilentlyContinue
if ($srPoints) {
    Write-Host "Restore points detected: $($srPoints.Count)"
    $srPoints | Format-Table -AutoSize
} else {
    Write-Host "Restore points detected: 0 (None)"
}

# Check System Restore Registry Settings
$srReg = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore" -ErrorAction SilentlyContinue
Write-Host "SystemRestore Registry Settings:"
Write-Host "  - RPSessionInterval : $($srReg.RPSessionInterval)"
Write-Host "  - DisableSR         : $($srReg.DisableSR)"

# Check SystemProtection state on drives
$spConfig = Get-WmiObject -Namespace root\default -Class SystemRestore -List -ErrorAction SilentlyContinue
Write-Host "SystemRestore WMI Class Available: $([bool]$spConfig)"

# Summary Report Output
$out = [PSCustomObject]@{
    CurrentFreeC_GB       = $freeC_GB
    UserTempSizeMB        = [math]::Round($userTempSize / 1MB, 2)
    InnoTempDirCount      = $isTempDirs.Count
    CbsLogsSizeMB         = if ($cbsSize) { [math]::Round($cbsSize / 1MB, 2) } else { 0 }
    DismLogsSizeMB        = if ($dismSize) { [math]::Round($dismSize / 1MB, 2) } else { 0 }
    RestorePointsCount    = if ($srPoints) { $srPoints.Count } else { 0 }
    SystemRestoreEnabled  = ($srPoints.Count -gt 0)
    EstimatedRecoverableMB= [math]::Round(($userTempSize + $cbsSize + $dismSize) / 1MB, 2)
    Timestamp             = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
}

$out | ConvertTo-Json -Depth 4 | Out-File -FilePath "tools\c_space_and_restore_audit.json" -Encoding utf8
Write-Host ""
Write-Host "Results saved to tools\c_space_and_restore_audit.json"
