# ClearWin 2.7.3 Local Windows Host Lifecycle Test
$ErrorActionPreference = "Continue"

$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($projectRoot)) { $projectRoot = (Get-Location).Path }

$installerPath = Join-Path $projectRoot "release\v2.7.3\ClearWin-Setup-2.7.3.exe"
$testDir = "D:\ClearWin_Local_Test\Install273"
$resultJsonPath = Join-Path $projectRoot "tools\v273_installer_lifecycle_result.json"

$log = [System.Collections.Generic.List[string]]::new()
function Log-Step($msg) {
    $entry = "[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $msg
    Write-Host $entry
    $log.Add($entry)
}

Log-Step "Starting ClearWin 2.7.3 Lifecycle Verification"

# Step 0: Ensure clean test directory
if (Test-Path $testDir) {
    Log-Step "Cleaning existing test dir: $testDir"
    Remove-Item $testDir -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $testDir -Force | Out-Null

# Step 1: Install
Log-Step "Executing silent install to $testDir"
$installArgs = "/DIR=""$testDir"" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-"
$p = Start-Process -FilePath $installerPath -ArgumentList $installArgs -PassThru -Wait
$installExitCode = $p.ExitCode
Log-Step "Installer exited with code: $installExitCode"

# Step 2: Verify installed files
$installedFiles = Get-ChildItem -Path $testDir -Recurse -File
$fileCount = $installedFiles.Count
Log-Step "Installed file count: $fileCount"

$mainExe = Join-Path $testDir "ClearWin.exe"
$mainExeExists = Test-Path $mainExe
$uninstaller = Join-Path $testDir "unins000.exe"
$uninstallerExists = Test-Path $uninstaller
$runtimeCheck = Join-Path $testDir "runtime-check.ps1"
$runtimeCheckExists = Test-Path $runtimeCheck
$webUiIndex = Join-Path $testDir "Resources\WebUI\index.html"
$webUiExists = Test-Path $webUiIndex
$tweaksDir = Join-Path $testDir "Resources\tweaks"
$tweakCount = if (Test-Path $tweaksDir) { (Get-ChildItem -Path $tweaksDir -Directory).Count } else { 0 }

Log-Step "Files verification: ClearWin.exe=$mainExeExists, unins000.exe=$uninstallerExists, runtime-check=$runtimeCheckExists, WebUI=$webUiExists, TweakDirs=$tweakCount"

# Step 3: Test runtime-check.ps1 in installed directory
$runtimeCheckExitCode = -1
if ($runtimeCheckExists) {
    Log-Step "Running runtime-check.ps1..."
    $rcProcess = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File ""$runtimeCheck""" -PassThru -Wait
    $runtimeCheckExitCode = $rcProcess.ExitCode
    Log-Step "runtime-check.ps1 exit code: $runtimeCheckExitCode"
}

# Step 4: Smoke launch ClearWin.exe
$smokeLaunchSuccess = $false
$smokePid = $null
$smokeWorkingSetMB = 0
$smokeError = $null

if ($mainExeExists) {
    Log-Step "Launching installed ClearWin.exe for smoke test..."
    try {
        $appProcess = Start-Process -FilePath $mainExe -PassThru
        $smokePid = $appProcess.Id
        Log-Step "ClearWin.exe launched with PID: $smokePid"
        
        # Monitor stability for 6 seconds
        Start-Sleep -Seconds 6
        $appProcess.Refresh()
        if (-not $appProcess.HasExited) {
            $smokeLaunchSuccess = $true
            $smokeWorkingSetMB = [Math]::Round($appProcess.WorkingSet64 / 1MB, 2)
            Log-Step "Process is alive and stable. WorkingSet: $smokeWorkingSetMB MB"
            
            # Graceful termination
            $appProcess.CloseMainWindow() | Out-Null
            Start-Sleep -Seconds 2
            $appProcess.Refresh()
            if (-not $appProcess.HasExited) {
                $appProcess.Kill()
                $appProcess.WaitForExit(3000)
            }
            Log-Step "Process closed successfully."
        } else {
            $smokeLaunchSuccess = $false
            $smokeError = "Process exited prematurely with exit code: $($appProcess.ExitCode)"
            Log-Step "ERROR: $smokeError"
        }
    } catch {
        $smokeLaunchSuccess = $false
        $smokeError = $_.Exception.Message
        Log-Step "ERROR launching ClearWin.exe: $smokeError"
    }
}

# Step 5: Test Uninstaller
$uninstallExitCode = -1
$leftoverCount = -1
if ($uninstallerExists) {
    Log-Step "Executing uninstaller: $uninstaller /VERYSILENT /SUPPRESSMSGBOXES /NORESTART"
    $unProcess = Start-Process -FilePath $uninstaller -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART" -PassThru -Wait
    $uninstallExitCode = $unProcess.ExitCode
    Log-Step "Uninstaller exited with code: $uninstallExitCode"
    
    # Wait 2 seconds for locks to release
    Start-Sleep -Seconds 2
    if (Test-Path $testDir) {
        $leftovers = Get-ChildItem -Path $testDir -Recurse -File -ErrorAction SilentlyContinue
        $leftoverCount = if ($leftovers) { $leftovers.Count } else { 0 }
    } else {
        $leftoverCount = 0
    }
    Log-Step "Leftover file count: $leftoverCount"
}

# Final Assessment
$lifecyclePassed = ($installExitCode -eq 0) -and $mainExeExists -and $uninstallerExists -and ($runtimeCheckExitCode -eq 0) -and $smokeLaunchSuccess -and ($uninstallExitCode -eq 0) -and ($leftoverCount -eq 0)

$result = [PSCustomObject]@{
    Version = "2.7.3"
    Timestamp = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
    InstallerPath = $installerPath
    InstallerSHA256 = (Get-FileHash -Path $installerPath -Algorithm SHA256).Hash
    InstallDir = $testDir
    InstallExitCode = $installExitCode
    InstalledFileCount = $fileCount
    MainExePresent = $mainExeExists
    UninstallerPresent = $uninstallerExists
    RuntimeCheckExitCode = $runtimeCheckExitCode
    TweakDirectoryCount = $tweakCount
    SmokeLaunchSuccess = $smokeLaunchSuccess
    SmokeProcessPid = $smokePid
    SmokeWorkingSetMB = $smokeWorkingSetMB
    SmokeError = $smokeError
    UninstallExitCode = $uninstallExitCode
    LeftoverFileCount = $leftoverCount
    LifecycleVerdict = if ($lifecyclePassed) { "PASS" } else { "FAIL" }
    Logs = $log
}

$result | ConvertTo-Json -Depth 4 | Out-File -FilePath $resultJsonPath -Encoding utf8
Log-Step "Lifecycle Verification Completed: Verdict = $($result.LifecycleVerdict)"
$result | Select-Object Version, InstallExitCode, InstalledFileCount, RuntimeCheckExitCode, SmokeLaunchSuccess, SmokeWorkingSetMB, UninstallExitCode, LeftoverFileCount, LifecycleVerdict | Format-List
