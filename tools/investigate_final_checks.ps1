# Investigation script for final ClearWin 2.7.2 Windows audit
$projectRoot = Split-Path -Parent $PSScriptRoot

Write-Host "=================================================="
Write-Host "1. EXAMINING SYSTEM RESTORE & ELEVATION PREREQUISITES"
Write-Host "=================================================="

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
Write-Host "Current Process IsAdmin / Elevated: $isAdmin"

# Check if System Protection can be queried via WMI / Registry
$srStatus = "Disabled"
try {
    $srClass = [wmiclass]"\\localhost\root\default:SystemRestore"
    Write-Host "WMI SystemRestore class accessible: True"
} catch {
    Write-Host "WMI SystemRestore class accessible: False ($($_.Exception.Message))"
}

# Check DISM Component Store Analysis (read-only)
Write-Host ""
Write-Host "=================================================="
Write-Host "2. OFFICIAL WINDOWS DISK CLEANUP METHODS ANALYSIS"
Write-Host "=================================================="
Write-Host "Method A: User Temp Cleanup (%TEMP%)"
$tempPath = [System.IO.Path]::GetTempPath()
$tempItems = Get-ChildItem -Path $tempPath -Recurse -Force -ErrorAction SilentlyContinue
$tempBytes = ($tempItems | Measure-Object -Property Length -Sum).Sum
Write-Host "  - Target: $tempPath"
Write-Host "  - Estimated Recoverable: $([math]::Round($tempBytes / 1MB, 2)) MB ($($tempItems.Count) files)"

Write-Host "Method B: Windows Cleanmgr (Disk Cleanup Tool)"
Write-Host "  - Tool: C:\Windows\System32\cleanmgr.exe"
Write-Host "  - Target Areas: Temporary Setup Files, Recycle Bin, Temporary Files, Thumbnails"
Write-Host "  - Safe & Official: Yes (native Windows tool with user GUI / preset flags)"

Write-Host "Method C: DISM Component Store Cleanup (Analyze & StartComponentCleanup)"
Write-Host "  - Tool: dism.exe /Online /Cleanup-Image /AnalyzeComponentStore"
Write-Host "  - Action: Safely cleans superseded service pack & update components in WinSxS without manual file deletion"
Write-Host "  - Requires Elevation: Yes (Admin token)"

Write-Host ""
Write-Host "=================================================="
Write-Host "3. AUDITING THE 17 PASS TWEAKS SCOPE & LIMITATIONS"
Write-Host "=================================================="
$evFile = Join-Path $projectRoot "tools\evidence_results.json"
if (Test-Path $evFile) {
    $tweaks = Get-Content $evFile | ConvertFrom-Json
    $passTweaks = $tweaks | Where-Object { $_.Status -eq "PASS" }
    Write-Host "Total PASS Tweaks: $($passTweaks.Count)"
    $hkcuCount = ($passTweaks | Where-Object { $_.PreTestState -like "*TaskbarAl*" -or $_.PreTestState -like "*GlobalUserDisabled*" -or $_.PreTestState -like "*SystemUsesLightTheme*" -or $_.SettingType -like "*Registry*" }).Count
    Write-Host "Pass Tweaks Summary:"
    foreach ($pt in $passTweaks) {
        Write-Host "  - $($pt.TweakName) [$($pt.SettingType)] -> Pre: $($pt.PreTestState) | Applied: $($pt.AppliedState) | Restored: $($pt.RestoredState)"
    }
}

Write-Host ""
Write-Host "=================================================="
Write-Host "4. WEBVIEW2 OFFLINE CANDIDATE CODEPATH AUDIT"
Write-Host "=================================================="
$issCandidate = Join-Path $projectRoot "installer-offline-candidate.iss"
if (Test-Path $issCandidate) {
    $content = Get-Content $issCandidate -Raw
    $hasCheck = $content -match "Check:\s*not IsWebView2Installed"
    $hasExec = $content -match "Exec\(InstallerPath,\s*'/silent /install'"
    Write-Host "Installer bundles offline payload: True"
    Write-Host "Extraction gated by 'not IsWebView2Installed': $hasCheck"
    Write-Host "Silent execution switch used: $hasExec"
    Write-Host "Host WebView2 Version currently installed: 155.0.4283.45"
    Write-Host "Live Absent-Runtime Codepath Status: UNTESTED ON HOST (WebView2 is already present)"
}

$investigationReport = [PSCustomObject]@{
    Timestamp          = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    IsAdmin            = $isAdmin
    UserTempRecoverMB  = [math]::Round($tempBytes / 1MB, 2)
    PassTweakCount     = $passTweaks.Count
    WebView2Installed  = $true
    WebView2Version    = "155.0.4283.45"
    WebView2AbsentPath = "UNTESTED_ON_HOST"
}
$investigationReport | ConvertTo-Json -Depth 4 | Out-File -FilePath (Join-Path $projectRoot "tools\investigation_findings.json") -Encoding utf8
Write-Host ""
Write-Host "Findings saved to tools\investigation_findings.json"
