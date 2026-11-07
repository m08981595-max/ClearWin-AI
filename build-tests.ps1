# Build and Run Tests for ClearWin
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($projectRoot)) { $projectRoot = (Get-Location).Path }
$srcDir = Join-Path $projectRoot "src"
$outDir = Join-Path $projectRoot "bin"
$testExe = Join-Path $outDir "ClearWinTests.exe"

Get-Process -Name "ClearWinTests" -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue

$roslynVersion = "4.8.0"
$roslynDir = Join-Path $projectRoot "tools\roslyn-$roslynVersion"
$roslynCsc = Get-ChildItem $roslynDir -Recurse -Filter "csc.exe" -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match "tasks\\net4(6|7|8)" } | Select-Object -First 1

if (-not $roslynCsc) {
    throw "Roslyn compiler not found in $roslynDir."
}
$csc = $roslynCsc.FullName

$frameworkDir = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
$refNames = @("WindowsBase", "PresentationCore", "PresentationFramework", "System.Xaml", "System.Management")
$refPaths = $refNames | ForEach-Object {
    $fwCopy = Join-Path $frameworkDir ("{0}.dll" -f $_)
    if (Test-Path $fwCopy) { $fwCopy } else { ([System.Reflection.Assembly]::LoadWithPartialName($_)).Location }
}
$refArgs = $refPaths | ForEach-Object { "/reference:$_" }

$wvVersion = "1.0.4191.47"
$wvCache = Join-Path $projectRoot "tools\webview2-$wvVersion"
$wvWpf = Get-ChildItem $wvCache -Recurse -Filter "Microsoft.Web.WebView2.Wpf.dll" | Select-Object -First 1
$wvCore = Get-ChildItem $wvCache -Recurse -Filter "Microsoft.Web.WebView2.Core.dll" | Select-Object -First 1

$csFiles = Get-ChildItem -Path $srcDir -Filter "*.cs" -Recurse |
    Where-Object { $_.Name -ne "AIDecisionDemo.cs" } |
    ForEach-Object { $_.FullName }

$compilerArgs = @(
    "/target:exe",
    "/platform:x64",
    "/langversion:7.3",
    "/define:DEBUG;TRACE",
    "/main:ClearWin.TestRunner",
    "/out:$testExe"
)
$compilerArgs += $refArgs
$compilerArgs += "/reference:$($wvCore.FullName)"
$compilerArgs += "/reference:$($wvWpf.FullName)"
$compilerArgs += $csFiles

Write-Host "Compiling test suite..." -ForegroundColor Cyan
& $csc @compilerArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "Test compilation failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

Write-Host "Compilation successful. Running test suite..." -ForegroundColor Cyan
$tweaksDest = Join-Path $outDir "Resources\tweaks"
if (Test-Path "$projectRoot\Resources\tweaks") {
    if (-not (Test-Path $tweaksDest)) { New-Item -ItemType Directory -Path $tweaksDest -Force | Out-Null }
    Copy-Item -Path "$projectRoot\Resources\tweaks\*" -Destination $tweaksDest -Recurse -Force -ErrorAction SilentlyContinue
}
& $testExe @args
exit $LASTEXITCODE

