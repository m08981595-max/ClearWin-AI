$ErrorActionPreference = "Continue"

$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($projectRoot)) { $projectRoot = (Get-Location).Path }

$binDir = Join-Path $projectRoot "bin"
$exePath = Join-Path $binDir "ClearWin.exe"
$icoPath = Join-Path $binDir "app.ico"

if (-not (Test-Path $exePath)) {
    Write-Error "ClearWin.exe not found at $exePath"
    exit 1
}

# 1. Update Desktop Shortcut
$desktopPath = [System.IO.Path]::Combine($env:USERPROFILE, "Desktop")
$shortcutFile = Join-Path $desktopPath "ClearWin.lnk"

try {
    if (-not (Test-Path $desktopPath)) {
        New-Item -ItemType Directory -Path $desktopPath -Force | Out-Null
    }
    
    $wshell = New-Object -ComObject WScript.Shell
    $shortcut = $wshell.CreateShortcut($shortcutFile)
    $shortcut.TargetPath = $exePath
    $shortcut.WorkingDirectory = $binDir
    if (Test-Path $icoPath) {
        $shortcut.IconLocation = "$icoPath,0"
    } else {
        $shortcut.IconLocation = "$exePath,0"
    }
    $shortcut.Description = "ClearWin 2.7.3 - Smart Windows Optimizer"
    $shortcut.Save()
    Write-Host "[OK] Desktop shortcut updated successfully: $shortcutFile"
} catch {
    Write-Warning "Could not update user desktop shortcut: $($_.Exception.Message)"
}

# 2. Check executable metadata
$fileInfo = Get-Item $exePath
$versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exePath)
Write-Host "Target Executable: $exePath"
Write-Host "File Version: $($versionInfo.FileVersion)"
Write-Host "Product Version: $($versionInfo.ProductVersion)"

# 3. Launch application
Write-Host "Launching ClearWin 2.7.3..."
try {
    $proc = Start-Process -FilePath $exePath -WorkingDirectory $binDir -PassThru
    Start-Sleep -Seconds 1
    if (-not $proc.HasExited) {
        Write-Host "[OK] ClearWin started successfully! (PID: $($proc.Id))"
    } else {
        Write-Warning "Process exited with code: $($proc.ExitCode)"
    }
} catch {
    Write-Host "Direct launch required elevation: $($_.Exception.Message)"
    try {
        $proc = Start-Process -FilePath $exePath -WorkingDirectory $binDir -Verb RunAs -PassThru
        Write-Host "[OK] ClearWin launched with elevated UAC prompt (PID: $($proc.Id))"
    } catch {
        Write-Error "Failed to launch ClearWin: $($_.Exception.Message)"
    }
}
