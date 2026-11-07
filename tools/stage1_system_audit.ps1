# Stage 1 Automated Read-Only Audit for ClearWin 2.7.2 Sandbox Gate
$projectRoot = Split-Path -Parent $PSScriptRoot

Write-Host "=================================================="
Write-Host "STAGE 1: READ-ONLY SYSTEM & ARTIFACT AUDIT"
Write-Host "=================================================="

# 1. Windows Sandbox & Feature Checks
$sandboxExe = "C:\Windows\System32\WindowsSandbox.exe"
$sandboxExeExists = Test-Path $sandboxExe
Write-Host "1. WindowsSandbox.exe exists: $sandboxExeExists"

$featureState = "Unknown"
$featureError = $null
try {
    $feature = Get-WindowsOptionalFeature -Online -FeatureName "Containers-DisposableClientVM" -ErrorAction Stop
    $featureState = "$($feature.State)"
    Write-Host "   Feature Containers-DisposableClientVM State: $featureState"
} catch {
    $featureError = $_.Exception.Message
    Write-Host "   Feature query note: $featureError"
    # Fallback registry query for CBS package
    $cbsKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\Packages"
    $cbsPackages = Get-ChildItem -Path $cbsKey -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -like "*Containers-DisposableClientVM*" }
    if ($cbsPackages) {
        $states = @()
        foreach ($p in $cbsPackages) {
            $prop = Get-ItemProperty -Path $p.PSPath -ErrorAction SilentlyContinue
            $states += "$($p.PSChildName): $($prop.CurrentState)"
        }
        $featureState = "CBS Staged (Count: $($cbsPackages.Count))"
        Write-Host "   CBS packages found: $($cbsPackages.Count)"
    } else {
        $featureState = "Not Found"
    }
}

# 2. Windows Version, Elevation, Virtualization
$os = Get-CimInstance Win32_OperatingSystem
$cs = Get-CimInstance Win32_ComputerSystem
$winVersion = "$($os.Caption) (Version: $($os.Version), Build: $($os.BuildNumber))"
Write-Host "2. OS: $winVersion"

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
Write-Host "   User Identity: $($identity.Name)"
Write-Host "   Is Administrator / Elevated: $isAdmin"

$proc = Get-CimInstance Win32_Processor | Select-Object -First 1
$virtFirmware = $proc.VirtualizationFirmwareEnabled
Write-Host "   CPU: $($proc.Name)"
Write-Host "   Virtualization Firmware Enabled: $virtFirmware"

# 3. Memory, Disk Space, Pending Reboot
$totalRAM_MB = [math]::Round($os.TotalVisibleMemorySize / 1KB, 2)
$freeRAM_MB = [math]::Round($os.FreePhysicalMemory / 1KB, 2)
$usedPercent = [math]::Round((($totalRAM_MB - $freeRAM_MB) / $totalRAM_MB) * 100, 2)
Write-Host "3. Total RAM: $totalRAM_MB MB ($([math]::Round($totalRAM_MB/1024, 2)) GB)"
Write-Host "   Free RAM : $freeRAM_MB MB ($([math]::Round($freeRAM_MB/1024, 2)) GB - $usedPercent% used)"
Write-Host "   Target Memory Safety Threshold (>= 3072 MB): $(if ($freeRAM_MB -ge 3072) { 'MET' } else { 'FAILED' })"

$driveC = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'"
$freeC_GB = [math]::Round($driveC.FreeSpace / 1GB, 2)
$driveD = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='D:'"
$freeD_GB = if ($driveD) { [math]::Round($driveD.FreeSpace / 1GB, 2) } else { 0 }
Write-Host "   Free Disk C: $freeC_GB GB"
Write-Host "   Free Disk D: $freeD_GB GB"

$rebootPending = $false
$rebootReasons = @()
if (Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending") {
    $rebootPending = $true; $rebootReasons += "CBS RebootPending"
}
if (Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") {
    $rebootPending = $true; $rebootReasons += "WindowsUpdate RebootRequired"
}
$pendingRename = (Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager" -Name "PendingFileRenameOperations" -ErrorAction SilentlyContinue).PendingFileRenameOperations
if ($pendingRename) {
    $rebootPending = $true; $rebootReasons += "PendingFileRenameOperations ($($pendingRename.Count) items)"
}
Write-Host "   Pending Reboot Required: $rebootPending $(if ($rebootPending) { "($($rebootReasons -join ', '))" })"

# 4. Candidate Installer Verification
$candidatePath = Join-Path $projectRoot "release\ClearWin-Setup-2.7.2-Offline-Candidate.exe"
$candidateExists = Test-Path $candidatePath
Write-Host "4. Candidate Installer Exists: $candidateExists ($candidatePath)"

# 5. SHA-256 Comparison
$expectedHash = "8AB33ED83873CD6363E6BE52E436BEED5D3734F5D46AE2C0C9C176FAC862FAFF"
$actualHash = $null
$hashMatch = $false
$candidateSize = 0
if ($candidateExists) {
    $candidateItem = Get-Item $candidatePath
    $candidateSize = $candidateItem.Length
    $actualHash = (Get-FileHash -Path $candidatePath -Algorithm SHA256).Hash
    $hashMatch = ($actualHash -eq $expectedHash)
    Write-Host "5. Expected Hash: $expectedHash"
    Write-Host "   Actual Hash  : $actualHash"
    Write-Host "   Size         : $candidateSize bytes ($([math]::Round($candidateSize/1MB, 2)) MB)"
    Write-Host "   Hash Match   : $hashMatch"
} else {
    Write-Host "5. Hash comparison skipped (file missing)."
}

# 6. WSB Configuration Inspection
$wsbPath = Join-Path $projectRoot "ClearWin-Restricted-Test.wsb"
$wsbExists = Test-Path $wsbPath
Write-Host "6. WSB Configuration File Exists: $wsbExists"
$wsbNetworkingDisabled = $false
$wsbClipboardDisabled = $false
$wsbReadOnly = $false
$wsbNoLogonCommand = $false
$wsbMemoryMB = 0

if ($wsbExists) {
    [xml]$wsbXml = Get-Content $wsbPath
    $wsbNetworkingDisabled = ($wsbXml.Configuration.Networking -eq "Disable")
    $wsbClipboardDisabled = ($wsbXml.Configuration.ClipboardRedirection -eq "Disable")
    $wsbMemoryMB = [int]$wsbXml.Configuration.MemoryInMB
    $mapped = $wsbXml.Configuration.MappedFolders.MappedFolder
    $wsbReadOnly = ($mapped.ReadOnly -eq "true")
    $wsbNoLogonCommand = ($null -eq $wsbXml.Configuration.LogonCommand)

    Write-Host "   Networking Disabled    : $wsbNetworkingDisabled (Value: $($wsbXml.Configuration.Networking))"
    Write-Host "   Clipboard Disabled     : $wsbClipboardDisabled (Value: $($wsbXml.Configuration.ClipboardRedirection))"
    Write-Host "   Memory Limit           : $wsbMemoryMB MB"
    Write-Host "   Mapped Host Folder     : $($mapped.HostFolder)"
    Write-Host "   Mapped Sandbox Folder  : $($mapped.SandboxFolder)"
    Write-Host "   Folder ReadOnly Flag   : $wsbReadOnly (Value: $($mapped.ReadOnly))"
    Write-Host "   No LogonCommand (Safe) : $wsbNoLogonCommand"
}

# Compile Full Results JSON
$results = [PSCustomObject]@{
    Timestamp              = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    WindowsSandboxExe      = $sandboxExeExists
    OptionalFeatureState   = $featureState
    FeatureQueryError      = $featureError
    OS                     = $winVersion
    User                   = $identity.Name
    IsAdmin                = $isAdmin
    VirtualizationFirmware = $virtFirmware
    TotalRAM_MB            = $totalRAM_MB
    FreeRAM_MB             = $freeRAM_MB
    MemoryThresholdMet     = ($freeRAM_MB -ge 3072)
    FreeDiskC_GB           = $freeC_GB
    FreeDiskD_GB           = $freeD_GB
    RebootPending          = $rebootPending
    RebootReasons          = $rebootReasons
    CandidateExists        = $candidateExists
    CandidateSizeBytes     = $candidateSize
    ExpectedHash           = $expectedHash
    ActualHash             = $actualHash
    HashMatch              = $hashMatch
    WsbExists              = $wsbExists
    WsbNetworkingDisabled  = $wsbNetworkingDisabled
    WsbClipboardDisabled   = $wsbClipboardDisabled
    WsbMemoryMB            = $wsbMemoryMB
    WsbReadOnly            = $wsbReadOnly
    WsbNoLogonCommand      = $wsbNoLogonCommand
}

$jsonPath = Join-Path $projectRoot "tools\stage1_audit_results.json"
$results | ConvertTo-Json -Depth 5 | Out-File -FilePath $jsonPath -Encoding utf8
Write-Host ""
Write-Host "Audit results successfully saved to: $jsonPath"
