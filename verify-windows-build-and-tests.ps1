#Requires -Version 5.1
<#
.SYNOPSIS
    ClearWin AI v2.7.7 — End-to-End Windows Build, Test & SHA-256 Verification Script
.DESCRIPTION
    Compiles ClearWin.AdminHelper.exe, ClearWin.exe, and ClearWin.Tests.exe from the repaired
    v2.7.7 C# source tree on Windows using the bundled Roslyn 4.8.0 compiler (tools\roslyn-4.8.0\tasks\net472\csc.exe),
    executes the full automated test suite (including Phase 1–5 regression suites), and generates
    a verified SHA-256 manifest of the compiled binaries.
#>

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ProjectRoot

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " ClearWin AI v2.7.7 — Windows Build & Verification Pipeline" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

# 1. Build Release Binaries (ClearWin.AdminHelper.exe + ClearWin.exe)
Write-Host "`n[1/4] Building ClearWin.AdminHelper.exe and ClearWin.exe via build-release.ps1..." -ForegroundColor Yellow
& "$ProjectRoot\build-release.ps1"
if ($LASTEXITCODE -ne 0) {
    throw "build-release.ps1 failed with exit code $LASTEXITCODE"
}

# 2. Build & Run Full Unit Test Suite (ClearWin.Tests.exe)
Write-Host "`n[2/4] Building and executing ClearWin.Tests.exe via build-tests.ps1..." -ForegroundColor Yellow
$env:CLEARWIN_TEST_REG_PREFIX = "SOFTWARE\ClearWin\TestSandbox"
& "$ProjectRoot\build-tests.ps1"
if ($LASTEXITCODE -ne 0) {
    throw "ClearWin.Tests.exe failed with exit code $LASTEXITCODE"
}

# 3. Run Targeted v2.7.7 Phase 1-5 Verification Filter
Write-Host "`n[3/4] Running targeted v2.7.7 Phase 1-5 regression suites..." -ForegroundColor Yellow
& "$ProjectRoot\bin\ClearWin.Tests.exe" "v2.7.7"
if ($LASTEXITCODE -ne 0) {
    throw "Targeted v2.7.7 regression suite failed with exit code $LASTEXITCODE"
}

# 4. Record Post-Build Binary SHA-256 Hashes
Write-Host "`n[4/4] Computing SHA-256 hashes for freshly built Windows binaries..." -ForegroundColor Yellow
$Binaries = @(
    "bin\ClearWin.exe",
    "bin\ClearWin.AdminHelper.exe",
    "bin\ClearWin.Tests.exe"
)
foreach ($bin in $Binaries) {
    if (Test-Path $bin) {
        $hash = (Get-FileHash -Path $bin -Algorithm SHA256).Hash
        $item = Get-Item $bin
        Write-Host ("  {0}  {1}  ({2} bytes, LastWriteTime: {3})" -f $hash, $bin, $item.Length, $item.LastWriteTimeUtc.ToString("o")) -ForegroundColor Green
    } else {
        throw "Expected binary missing after build: $bin"
    }
}

Write-Host "`n[SUCCESS] ClearWin AI v2.7.7 built and verified on Windows!" -ForegroundColor Green
