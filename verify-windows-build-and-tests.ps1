#Requires -Version 5.1
<#
.SYNOPSIS
    ClearWin AI v2.7.7 - End-to-End Windows Build, Test & SHA-256 Verification Script
.DESCRIPTION
    Drives the REAL build system in this folder:
      * build.ps1       -> bin\ClearWin.exe, bin\ClearWin.AdminHelper.exe
      * build-tests.ps1 -> bin\ClearWinTests.exe (compiles and runs the full suite)
      * bin\ClearWinTests.exe "v2.7.7" -> targeted regression filter
    Finally records SHA-256 hashes of the binaries actually produced.

    History: this script used to call a non-existent build-release.ps1 and looked
    for bin\ClearWin.Tests.exe (the real output name is ClearWinTests.exe).
    Fixed to match the real build.ps1 / build-tests.ps1 pipeline.
#>

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location -LiteralPath $ProjectRoot

function Assert-BuildScript {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required build script not found: $Path"
    }
}

function Assert-Artifact {
    param([string]$Path, [string]$Purpose)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Expected $Purpose missing after build: $Path"
    }
    $item = Get-Item -LiteralPath $Path
    if ($item.Length -le 0) {
        throw "Expected $Purpose is empty after build: $Path"
    }
    Write-Host ("  verified: {0} ({1} bytes)" -f $Path, $item.Length) -ForegroundColor Green
}

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " ClearWin AI v2.7.7 - Windows Build & Verification Pipeline" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

$buildScript = Join-Path $ProjectRoot "build.ps1"
$testBuildScript = Join-Path $ProjectRoot "build-tests.ps1"
$binDir = Join-Path $ProjectRoot "bin"
$mainExe = Join-Path $binDir "ClearWin.exe"
$helperExe = Join-Path $binDir "ClearWin.AdminHelper.exe"
$testExe = Join-Path $binDir "ClearWinTests.exe"

Assert-BuildScript -Path $buildScript
Assert-BuildScript -Path $testBuildScript

# 1. Build release binaries with the real build.ps1.
Write-Host "`n[1/4] Building ClearWin.exe + ClearWin.AdminHelper.exe via build.ps1..." -ForegroundColor Yellow
try {
    & $buildScript
} catch {
    throw "build.ps1 failed: $($_.Exception.Message)"
}
if ($LASTEXITCODE -ne 0) {
    throw "build.ps1 reported exit code $LASTEXITCODE"
}
Assert-Artifact -Path $mainExe -Purpose "main executable"
Assert-Artifact -Path $helperExe -Purpose "admin helper executable"

# 2. Compile and run the full unit-test suite with the real build-tests.ps1.
#    build-tests.ps1 compiles all sources (main entry: ClearWin.TestRunner),
#    writes bin\ClearWinTests.exe and then runs it, forwarding @args.
Write-Host "`n[2/4] Compiling and running full suite via build-tests.ps1..." -ForegroundColor Yellow
$env:CLEARWIN_TEST_REG_PREFIX = "SOFTWARE\ClearWin\TestSandbox"
try {
    & $testBuildScript
} catch {
    throw "build-tests.ps1 failed: $($_.Exception.Message)"
}
if ($LASTEXITCODE -ne 0) {
    throw "Test suite failed with exit code $LASTEXITCODE"
}
Assert-Artifact -Path $testExe -Purpose "test runner"

# 3. Targeted v2.7.7 regression filter. TestRunner accepts the filter as args[0]
#    (case-insensitive substring of the test name, e.g. 'v2.7.7').
Write-Host "`n[3/4] Running targeted v2.7.7 regression suites..." -ForegroundColor Yellow
& $testExe "v2.7.7"
if ($LASTEXITCODE -ne 0) {
    throw "Targeted v2.7.7 regression suite failed with exit code $LASTEXITCODE"
}

# 4. Record SHA-256 hashes of the binaries actually produced.
Write-Host "`n[4/4] Computing SHA-256 hashes for freshly built Windows binaries..." -ForegroundColor Yellow
$Binaries = @(
    $mainExe,
    $helperExe,
    $testExe
)
foreach ($bin in $Binaries) {
    Assert-Artifact -Path $bin -Purpose "binary"
    $hash = (Get-FileHash -LiteralPath $bin -Algorithm SHA256).Hash
    $item = Get-Item -LiteralPath $bin
    Write-Host ("  {0}  {1}  ({2} bytes, LastWriteTime: {3})" -f $hash, $bin, $item.Length, $item.LastWriteTimeUtc.ToString("o")) -ForegroundColor Green
}

Write-Host "`n[SUCCESS] ClearWin AI v2.7.7 built and verified on Windows!" -ForegroundColor Green
