# Verify Candidate Installer and Host Sandbox State
$candidatePath = "release\ClearWin-Setup-2.7.2-Offline-Candidate.exe"
$expectedHash = "8AB33ED83873CD6363E6BE52E436BEED5D3734F5D46AE2C0C9C176FAC862FAFF"
$expectedSize = 217355785

Write-Host "=== 1. Candidate Hash & Size Verification ==="
if (-not (Test-Path $candidatePath)) {
    Write-Error "Candidate file missing: $candidatePath"
    exit 1
}

$fileItem = Get-Item $candidatePath
$actualSize = $fileItem.Length
$actualHash = (Get-FileHash -Path $candidatePath -Algorithm SHA256).Hash

Write-Host "Candidate Path: $($fileItem.FullName)"
Write-Host "Actual Size   : $actualSize bytes ($([math]::Round($actualSize/1MB, 2)) MB)"
Write-Host "Expected Size : $expectedSize bytes"
Write-Host "Actual Hash   : $actualHash"
Write-Host "Expected Hash : $expectedHash"

$sizeMatch = ($actualSize -eq $expectedSize)
$hashMatch = ($actualHash -eq $expectedHash)

Write-Host "Size Match: $sizeMatch"
Write-Host "Hash Match: $hashMatch"

if (-not $hashMatch -or -not $sizeMatch) {
    Write-Error "Integrity check failed: Hash or size mismatch!"
    exit 2
}

Write-Host ""
Write-Host "=== 2. Read-Only Windows Sandbox Feature Audit ==="
$sandboxExe = "C:\Windows\System32\WindowsSandbox.exe"
$exeExists = Test-Path $sandboxExe
Write-Host "WindowsSandbox.exe exists: $exeExists"

# Check CBS Package state in registry
$cbsKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\Packages"
$sandboxPackages = Get-ChildItem -Path $cbsKey -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -like "*Containers-DisposableClientVM*" }

if ($sandboxPackages) {
    foreach ($pkg in $sandboxPackages) {
        $props = Get-ItemProperty -Path $pkg.PSPath -ErrorAction SilentlyContinue
        Write-Host "Package: $($pkg.PSChildName) | CurrentState: $($props.CurrentState)"
    }
} else {
    Write-Host "No DisposableClientVM CBS package found."
}

# Check Hyper-V / Virtualization services
$vmcompute = Get-Service -Name "vmcompute" -ErrorAction SilentlyContinue
$cmimce = Get-Service -Name "cmimce" -ErrorAction SilentlyContinue
Write-Host "vmcompute Service Status: $(if ($vmcompute) { $vmcompute.Status } else { 'Not Found' })"
Write-Host "cmimce Service Status   : $(if ($cmimce) { $cmimce.Status } else { 'Not Found' })"

Write-Host ""
Write-Host "=== 3. Host Memory Audit ==="
$os = Get-CimInstance Win32_OperatingSystem
$totalMB = [math]::Round($os.TotalVisibleMemorySize / 1KB, 2)
$freeMB = [math]::Round($os.FreePhysicalMemory / 1KB, 2)
$usedMB = [math]::Round(($os.TotalVisibleMemorySize - $os.FreePhysicalMemory) / 1KB, 2)
$usedPercent = [math]::Round(($usedMB / $totalMB) * 100, 2)

Write-Host "Total RAM   : $totalMB MB ($([math]::Round($totalMB/1024, 2)) GB)"
Write-Host "Used RAM    : $usedMB MB ($([math]::Round($usedMB/1024, 2)) GB - $usedPercent%)"
Write-Host "Free RAM    : $freeMB MB ($([math]::Round($freeMB/1024, 2)) GB)"

$minRequiredMB = 3072
$hasMinRam = ($freeMB -ge $minRequiredMB)
Write-Host "Safety Rule Requirement: >= 3072 MB (3 GB)"
Write-Host "RAM Threshold Met       : $hasMinRam"

# Save audit results to JSON
$auditResult = [PSCustomObject]@{
    CandidatePath     = $fileItem.FullName
    ActualSize        = $actualSize
    ActualHash        = $actualHash
    IntegrityMatch    = ($hashMatch -and $sizeMatch)
    SandboxExeExists  = $exeExists
    VmcomputeStatus   = if ($vmcompute) { "$($vmcompute.Status)" } else { "Not Found" }
    TotalRamMB        = $totalMB
    FreeRamMB         = $freeMB
    UsedPercent       = $usedPercent
    MinRequiredMB     = $minRequiredMB
    RamThresholdMet   = $hasMinRam
    Timestamp         = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
}
$auditResult | ConvertTo-Json -Depth 4 | Out-File -FilePath "tools\sandbox_gate_recovery_audit.json" -Encoding utf8
Write-Host "Audit results saved to tools\sandbox_gate_recovery_audit.json"
