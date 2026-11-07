# Test Inno Setup Offline Candidate installer execution
$projectRoot = Split-Path -Parent $PSScriptRoot
$candidateExe = Join-Path $projectRoot "release\ClearWin-Setup-2.7.2-Offline-Candidate.exe"
$targetDir = "D:\ClearWin_Local_Test\CandidateInstall"
$logFile = "D:\ClearWin_Local_Test\candidate_install.log"
$resultJson = Join-Path $projectRoot "tools\candidate_installer_test_result.json"

if (Test-Path $targetDir) {
    Remove-Item $targetDir -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $targetDir -Force | Out-Null

Write-Host "Candidate Installer: $candidateExe"
Write-Host "Target Directory: $targetDir"
Write-Host "Log: $logFile"

$argList = @(
    "/DIR=""$targetDir""",
    "/VERYSILENT",
    "/SUPPRESSMSGBOXES",
    "/NORESTART",
    "/LOG=""$logFile"""
)

try {
    Write-Host "Launching candidate installer process..."
    $proc = Start-Process -FilePath $candidateExe -ArgumentList $argList -Wait -PassThru
    Write-Host "Candidate Installer finished with ExitCode: $($proc.ExitCode)"

    # Inspect installed files
    $installedFiles = @()
    if (Test-Path $targetDir) {
        $installedFiles = Get-ChildItem -Path $targetDir -Recurse | Select-Object FullName, Length
        Write-Host "Total Installed Files found: $($installedFiles.Count)"
    }

    $mainExeInstalled = Test-Path (Join-Path $targetDir "ClearWin.exe")
    $uninstallerExists = Test-Path (Join-Path $targetDir "unins000.exe")
    Write-Host "ClearWin.exe installed: $mainExeInstalled"
    Write-Host "Uninstaller unins000.exe exists: $uninstallerExists"

    # Test launching installed candidate ClearWin.exe
    $smokeOk = $false
    if ($mainExeInstalled) {
        Write-Host "Testing candidate ClearWin.exe process launch..."
        $appProc = Start-Process -FilePath (Join-Path $targetDir "ClearWin.exe") -PassThru
        Start-Sleep -Seconds 6
        $appProc.Refresh()
        $smokeOk = (-not $appProc.HasExited)
        Write-Host "Candidate ClearWin running: $smokeOk (WorkingSet: $([math]::Round($appProc.WorkingSet64/1MB, 2)) MB)"
        $appProc.Kill()
    }

    # Test candidate uninstaller
    $uninsOk = $false
    if ($uninstallerExists) {
        Write-Host "Testing candidate uninstaller..."
        $uninsProc = Start-Process -FilePath (Join-Path $targetDir "unins000.exe") -ArgumentList @("/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART") -Wait -PassThru
        Start-Sleep -Seconds 4
        $uninsOk = (-not (Test-Path (Join-Path $targetDir "ClearWin.exe")))
        Write-Host "Candidate uninstalled cleanly: $uninsOk"
    }

    $res = [PSCustomObject]@{
        CandidatePath      = $candidateExe
        InstallExitCode    = $proc.ExitCode
        MainExeInstalled   = $mainExeInstalled
        InstalledFileCount = $installedFiles.Count
        SmokeLaunchStable  = $smokeOk
        CleanUninstall     = $uninsOk
        Timestamp          = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    }
    $res | ConvertTo-Json -Depth 4 | Out-File -FilePath $resultJson -Encoding utf8
    Write-Host "Candidate lifecycle results saved to $resultJson"
} catch {
    Write-Error "Candidate lifecycle test failed: $($_.Exception.Message)"
}
