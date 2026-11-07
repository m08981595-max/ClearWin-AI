$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($projectRoot)) { $projectRoot = (Get-Location).Path }

$isccPaths = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\iscc.exe",
    "C:\Users\maher\AppData\Local\Programs\Inno Setup 6\iscc.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\iscc.exe",
    "${env:ProgramFiles}\Inno Setup 6\iscc.exe"
)
$iscc = $isccPaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Error "Inno Setup compiler (iscc.exe) not found."
    exit 1
}

$issFile = Join-Path $projectRoot "installer-2.7.3-offline.iss"
if (-not (Test-Path $issFile)) {
    Write-Error "Script not found: $issFile"
    exit 1
}

Write-Host "Compiling ClearWin 2.7.3 Offline Standalone Installer..." -ForegroundColor Cyan
& $iscc $issFile
if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup compilation failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

$setupExe = Join-Path $projectRoot "release\v2.7.3\ClearWin-Setup-2.7.3-Offline.exe"
if (Test-Path $setupExe) {
    $item = Get-Item $setupExe
    $setupSize = [Math]::Round($item.Length / 1MB, 2)
    $hash = (Get-FileHash -Path $setupExe -Algorithm SHA256).Hash
    Write-Host "Offline release installer ready: $setupExe" -ForegroundColor Green
    Write-Host "Size: $setupSize MB ($($item.Length) bytes)" -ForegroundColor Green
    Write-Host "SHA256: $hash" -ForegroundColor Green
} else {
    Write-Error "Output installer not found at $setupExe"
    exit 1
}
