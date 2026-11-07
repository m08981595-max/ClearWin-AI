# ClearWin 2.7.2 - Sandbox Feature Diagnostic & Recovery Tool
# tools\recover_sandbox_feature.ps1

$projectRoot = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $projectRoot "tools"
$logFile = Join-Path $outDir "sandbox_recovery_diagnostic.log"
$resultJson = Join-Path $outDir "sandbox_recovery_diagnostic.json"

Write-Host "=================================================="
Write-Host "CLEARWIN SANDBOX RECOVERY & DIAGNOSTIC AUDIT"
Write-Host "=================================================="

# 1. Check WindowsSandbox.exe
$exePath = "C:\Windows\System32\WindowsSandbox.exe"
$exeExists = Test-Path $exePath
Write-Host "1. WindowsSandbox.exe present: $exeExists"

# 2. Query CBS Package states from registry
$cbsPackages = @()
$cbsKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\Packages"
$matchedPackages = Get-ChildItem -Path $cbsKey -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -like "*Containers-DisposableClientVM*" }
foreach ($p in $matchedPackages) {
    $prop = Get-ItemProperty -Path $p.PSPath -ErrorAction SilentlyContinue
    $cbsPackages += [PSCustomObject]@{
        Name  = $p.PSChildName
        State = "$($prop.CurrentState)"
    }
}
Write-Host "2. CBS Packages found: $($cbsPackages.Count)"

# 3. Inspect DISM Log for previous errors or attempts
$dismLogPath = "C:\Windows\Logs\DISM\dism.log"
$dismMatches = @()
if (Test-Path $dismLogPath) {
    try {
        $lines = Get-Content -Path $dismLogPath -Tail 500 -ErrorAction SilentlyContinue
        $dismMatches = $lines | Where-Object { $_ -match "Containers-DisposableClientVM|DisposableClientVM|Error\s+0x|740" } | Select-Object -Last 10
    } catch {
        $dismMatches = @("Could not read DISM log: $($_.Exception.Message)")
    }
}
Write-Host "3. DISM Log relevant matches found: $($dismMatches.Count)"

# 4. Inspect CBS Log for previous errors or attempts
$cbsLogPath = "C:\Windows\Logs\CBS\CBS.log"
$cbsMatches = @()
if (Test-Path $cbsLogPath) {
    try {
        $lines = Get-Content -Path $cbsLogPath -Tail 500 -ErrorAction SilentlyContinue
        $cbsMatches = $lines | Where-Object { $_ -match "Containers-DisposableClientVM" } | Select-Object -Last 10
    } catch {
        $cbsMatches = @("Could not read CBS log: $($_.Exception.Message)")
    }
}
Write-Host "4. CBS Log relevant matches found: $($cbsMatches.Count)"

# 5. Check Memory and Environment
$os = Get-CimInstance Win32_OperatingSystem
$freeMB = [math]::Round($os.FreePhysicalMemory / 1KB, 2)
$totalMB = [math]::Round($os.TotalVisibleMemorySize / 1KB, 2)
Write-Host "5. Free RAM: $freeMB MB (Threshold >= 3072 MB: $($freeMB -ge 3072))"

# 6. Check Current Session Elevation
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
Write-Host "6. Current Session Elevation (IsAdmin): $isAdmin"

# Compile diagnostic object
$diag = [PSCustomObject]@{
    Timestamp          = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    WindowsSandboxExe  = $exeExists
    CbsPackageCount    = $cbsPackages.Count
    CbsPackages        = $cbsPackages
    DismLogMatches     = $dismMatches
    CbsLogMatches      = $cbsMatches
    TotalRAM_MB        = $totalMB
    FreeRAM_MB         = $freeMB
    MemoryThresholdMet = ($freeMB -ge 3072)
    IsAdmin            = $isAdmin
}

$diag | ConvertTo-Json -Depth 5 | Out-File -FilePath $resultJson -Encoding utf8
Write-Host "Diagnostic saved to: $resultJson"
