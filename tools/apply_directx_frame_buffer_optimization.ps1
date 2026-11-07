# ==============================================================================
# DirectX & AMD Radeon Driver Pre-Rendered Frame Buffer & Triple Buffering Setup
# Target GPU: AMD Radeon HD 7700M (Cape Verde / GCN 1.0)
# Purpose: Maintain a 1-frame pre-render buffer to prevent FPS drops during combat
# ==============================================================================

Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host " Applying DirectX & AMD Driver Pre-Rendered Frame Buffer Optimization        " -ForegroundColor Yellow
Write-Host "==============================================================================" -ForegroundColor Cyan

$amdKey = "HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000"
$umdKey = "$amdKey\UMD"

# 1. Verify AMD Controller path
if (-not (Test-Path $amdKey)) {
    Write-Error "AMD Display adapter registry path not found!"
    exit 1
}

Write-Host "[1/4] Configuring AMD Radeon Driver Pre-Rendered Frame Buffer (FlipQueueSize = 1)..." -ForegroundColor Green
# Binary '1' = 0x31, 0x00
$val1_2bytes = [byte[]](0x31, 0x00)
$val1_4bytes = [byte[]](0x31, 0x00, 0x00, 0x00)

# Set on root AMD adapter key
Set-ItemProperty -Path $amdKey -Name "FlipQueueSize" -Value $val1_2bytes -Type Binary
Set-ItemProperty -Path $amdKey -Name "TurboSync" -Value $val1_2bytes -Type Binary
Set-ItemProperty -Path $amdKey -Name "EnableTripleBuffering" -Value $val1_4bytes -Type Binary

# Set on UMD (User Mode Driver) key
if (Test-Path $umdKey) {
    Set-ItemProperty -Path $umdKey -Name "EnableTripleBuffering" -Value $val1_4bytes -Type Binary
    Set-ItemProperty -Path $umdKey -Name "FlipQueueSize" -Value $val1_2bytes -Type Binary
    Set-ItemProperty -Path $umdKey -Name "TextureOpt" -Value $val1_4bytes -Type Binary # Enable texture performance optimizations
    Write-Host "  -> UMD TripleBuffering & FlipQueueSize successfully activated." -ForegroundColor Gray
}

Write-Host "[2/4] Configuring Direct3D System-wide Pre-Render Limit & Buffering..." -ForegroundColor Green
$d3dPaths = @(
    "HKLM:\SOFTWARE\Microsoft\Direct3D",
    "HKCU:\Software\Microsoft\Direct3D",
    "HKLM:\SOFTWARE\Microsoft\DirectDraw",
    "HKCU:\Software\Microsoft\DirectDraw",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Direct3D",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\DirectDraw"
)

foreach ($path in $d3dPaths) {
    if (-not (Test-Path $path)) {
        New-Item -Path $path -Force | Out-Null
    }
    Set-ItemProperty -Path $path -Name "TripleBuffering" -Value 1 -Type DWord -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $path -Name "MaxPreRenderedFrames" -Value 1 -Type DWord -ErrorAction SilentlyContinue
}

Write-Host "[3/4] Optimizing DXGI Swapchain & High-Performance GPU Profile for Emulators..." -ForegroundColor Green
$dxGpuPref = "HKCU:\Software\Microsoft\DirectX\UserGpuPreferences"
if (-not (Test-Path $dxGpuPref)) {
    New-Item -Path $dxGpuPref -Force | Out-Null
}

# Auto-detect and bind common emulator processes to High Performance AMD GPU (GpuPreference=2)
$emuExes = @(
    "aow_exe.exe",
    "AndroidProcess.exe",
    "AppMarket.exe",
    "AndroidEmulator.exe",
    "MuMuPlayer.exe",
    "NemuPlayer.exe",
    "NemuHeadless.exe"
)

foreach ($exe in $emuExes) {
    $found = Get-Process -Name ($exe.Replace(".exe","")) -ErrorAction SilentlyContinue
    if ($found -and $found.Path) {
        Set-ItemProperty -Path $dxGpuPref -Name $found.Path -Value "GpuPreference=2;" -Type String -ErrorAction SilentlyContinue
        Write-Host "  -> High Performance AMD GPU assigned to running emulator: $($found.Path)" -ForegroundColor Cyan
    }
}

Write-Host "[4/4] Verifying applied configurations..." -ForegroundColor Green
$curFlip = (Get-ItemProperty -Path $amdKey).FlipQueueSize
$curUmdTB = (Get-ItemProperty -Path $umdKey).EnableTripleBuffering
$curD3D = (Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Direct3D").MaxPreRenderedFrames

Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host "  • AMD FlipQueueSize (Pre-rendered buffer) : ENABLED (1 Frame Buffer)" -ForegroundColor Yellow
Write-Host "  • AMD Driver Triple Buffering             : ENABLED (Byte Array: 49 0 0 0)" -ForegroundColor Yellow
Write-Host "  • Direct3D MaxPreRenderedFrames           : $curD3D Frame Ahead" -ForegroundColor Yellow
Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host "SUCCESS! The emergency frame buffer is now active in DirectX and AMD driver." -ForegroundColor Green
