# Test Inno Setup installer execution to isolated folder on D:
$projectRoot = Split-Path -Parent $PSScriptRoot
$setupExe = Join-Path $projectRoot "release\ClearWin-Setup-2.7.2.exe"
$targetDir = "D:\ClearWin_Local_Test\Install"
$logFile = "D:\ClearWin_Local_Test\install.log"
$resultJson = Join-Path $projectRoot "tools\local_installer_test_result.json"

if (-not (Test-Path "D:\ClearWin_Local_Test")) {
    New-Item -ItemType Directory -Path "D:\ClearWin_Local_Test" -Force | Out-Null
}
if (Test-Path $targetDir) {
    Remove-Item $targetDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Installer: $setupExe"
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
    Write-Host "Launching installer process..."
    $proc = Start-Process -FilePath $setupExe -ArgumentList $argList -Wait -PassThru
    Write-Host "Installer finished with ExitCode: $($proc.ExitCode)"

    # Inspect installed files
    $installedFiles = @()
    if (Test-Path $targetDir) {
        $installedFiles = Get-ChildItem -Path $targetDir -Recurse | Select-Object FullName, Length
        Write-Host "Total Installed Files found: $($installedFiles.Count)"
    } else {
        Write-Host "Target directory not created."
    }

    $mainExeInstalled = Test-Path (Join-Path $targetDir "ClearWin.exe")
    $uninstallerExists = Test-Path (Join-Path $targetDir "unins000.exe")
    Write-Host "ClearWin.exe installed: $mainExeInstalled"
    Write-Host "Uninstaller unins000.exe exists: $uninstallerExists"

    $res = [PSCustomObject]@{
        InstallerPath      = $setupExe
        ExitCode           = $proc.ExitCode
        TargetDirectory    = $targetDir
        MainExeInstalled   = $mainExeInstalled
        UninstallerExists  = $uninstallerExists
        InstalledFileCount = $installedFiles.Count
        Timestamp          = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    }
    $res | ConvertTo-Json -Depth 4 | Out-File -FilePath $resultJson -Encoding utf8
    Write-Host "Results saved to $resultJson"
} catch {
    Write-Error "Failed to execute installer: $($_.Exception.Message)"
}
