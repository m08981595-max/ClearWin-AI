# ClearWin Build Script
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcDir = Join-Path $projectRoot "src"
$outDir = Join-Path $projectRoot "bin"
$iconPath = Join-Path $projectRoot "Resources\app.ico"
$exePath = Join-Path $outDir "ClearWin.exe"

if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }
Get-Process -Name "ClearWin", "ClearWin.AdminHelper" -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -like "$outDir\*" } |
    Stop-Process -Force -ErrorAction SilentlyContinue
# ---- C# compiler -------------------------------------------------------
# The sources use C# 6+ features (expression-bodied members, string
# interpolation, null-conditional operators, nameof, ...). The legacy
# .NET Framework csc.exe only understands up to C# 5 and fails with
# CS1002/CS1519 on the very first such construct. Roslyn is therefore the
# preferred compiler; we fall back to the legacy one only if Roslyn is
# genuinely unavailable (in that case the caller gets the real compile
# errors instead of a silent wrong-language-version failure).
$roslynVersion = "4.8.0"
$roslynDir = Join-Path $projectRoot "tools\roslyn-$roslynVersion"
$roslynCsc = Get-ChildItem $roslynDir -Recurse -Filter "csc.exe" -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match "tasks\\net4(6|7|8)" } | Select-Object -First 1
$roslynNupkg = Join-Path $projectRoot "tools\Microsoft.Net.Compilers.Toolset.$roslynVersion.nupkg"

if (-not $roslynCsc) {
    Write-Host "Roslyn compiler not found locally. Downloading Microsoft.Net.Compilers.Toolset $roslynVersion ..."
    try {
        if (-not (Test-Path $roslynDir)) { New-Item -ItemType Directory -Path $roslynDir -Force | Out-Null }
        if (-not (Test-Path $roslynNupkg)) {
            $ProgressPreference = "SilentlyContinue"
            Invoke-WebRequest -UseBasicParsing -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.net.compilers.toolset/$roslynVersion/microsoft.net.compilers.toolset.$roslynVersion.nupkg" -OutFile $roslynNupkg -ErrorAction Stop
        }

        $roslynZip = Join-Path $projectRoot "tools\Microsoft.Net.Compilers.Toolset.$roslynVersion.zip"
        Copy-Item -Path $roslynNupkg -Destination $roslynZip -Force
        try {
            Expand-Archive -Path $roslynZip -DestinationPath $roslynDir -Force
        } finally {
            Remove-Item -Path $roslynZip -Force -ErrorAction SilentlyContinue
        }
        $roslynCsc = Get-ChildItem $roslynDir -Recurse -Filter "csc.exe" -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "tasks\\net4(6|7|8)" } | Select-Object -First 1
    } catch {
        Write-Host "Warning: Roslyn download failed ($_). Falling back to the legacy C# 5 compiler."
        $roslynCsc = $null
    }
}

$legacyCsc = if (Test-Path "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe") {
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
} else {
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if ($roslynCsc) {
    $csc = $roslynCsc.FullName
    $langVersion = "7.3"
} else {
    if (-not (Test-Path $legacyCsc)) {
        throw "No usable C# compiler found (Roslyn missing and legacy csc.exe absent)."
    }
    $csc = $legacyCsc
    $langVersion = "5"
}
# Resolve assembly paths
$refNames = @("WindowsBase", "PresentationCore", "PresentationFramework", "System.Xaml", "System.Management")
$refPaths = $refNames | ForEach-Object {
    $a = [System.Reflection.Assembly]::LoadWithPartialName($_)
    if ($a) { $a.Location }
}

# The .NET 4 csc auto-imports framework reference assemblies from the framework
# directory; referencing the GAC copy of a framework assembly (e.g. via
# LoadWithPartialName) therefore yields CS1703 (duplicate identity). Prefer the
# framework copy when it exists.
$frameworkDir = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
$refPaths = $refPaths | ForEach-Object {
    $fwCopy = Join-Path $frameworkDir ([System.IO.Path]::GetFileName($_))
    if (Test-Path $fwCopy) { $fwCopy } else { $_ }
}

$refArgs = $refPaths | ForEach-Object { "/reference:$_" }

# WebView2 SDK (stable release as of the current build)
$wvVersion = "1.0.4191.47"
$wvCache = Join-Path $projectRoot "tools\webview2-$wvVersion"
$wvNupkg = Join-Path $projectRoot "tools\Microsoft.Web.WebView2.$wvVersion.nupkg"
$wvWpf = $null
$wvCore = $null
$wvLoader = $null
if (-not (Test-Path $wvCache)) { New-Item -ItemType Directory -Path $wvCache -Force | Out-Null }
$wvWpf = Get-ChildItem $wvCache -Recurse -Filter "Microsoft.Web.WebView2.Wpf.dll" -ErrorAction SilentlyContinue | Select-Object -First 1
$wvCore = Get-ChildItem $wvCache -Recurse -Filter "Microsoft.Web.WebView2.Core.dll" -ErrorAction SilentlyContinue | Select-Object -First 1
$wvLoader = Get-ChildItem $wvCache -Recurse -Filter "WebView2Loader.dll" -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match "win-x64[\\/]native" } | Select-Object -First 1
if (-not $wvWpf -or -not $wvCore -or -not $wvLoader) {
    Write-Host "WebView2 SDK not found locally. Downloading Microsoft.Web.WebView2 $wvVersion ..."
    try {
        if (-not (Test-Path $wvNupkg)) {
            Invoke-WebRequest -UseBasicParsing -Uri "https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2/$wvVersion" -OutFile $wvNupkg -ErrorAction Stop
        }
        Get-ChildItem -Path $wvCache -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

        # NuGet packages (.nupkg) are ZIP containers, but Expand-Archive only
        # accepts files with a .zip extension. Extract through a temporary
        # .zip copy so Windows PowerShell 5.1 can handle the package reliably.
        $wvZip = Join-Path $projectRoot "tools\Microsoft.Web.WebView2.$wvVersion.zip"
        Copy-Item -Path $wvNupkg -Destination $wvZip -Force
        try {
            Expand-Archive -Path $wvZip -DestinationPath $wvCache -Force
        } finally {
            Remove-Item -Path $wvZip -Force -ErrorAction SilentlyContinue
        }
        $wvWpf = Get-ChildItem $wvCache -Recurse -Filter "Microsoft.Web.WebView2.Wpf.dll" | Select-Object -First 1
        $wvCore = Get-ChildItem $wvCache -Recurse -Filter "Microsoft.Web.WebView2.Core.dll" | Select-Object -First 1
        $wvLoader = Get-ChildItem $wvCache -Recurse -Filter "WebView2Loader.dll" | Where-Object { $_.FullName -match "win-x64[\\/]native" } | Select-Object -First 1
    } catch {
        Write-Error "Could not obtain the WebView2 SDK. $($_.Exception.Message)"
        exit 1
    }
}
if (-not $wvWpf -or -not $wvCore -or -not $wvLoader) {
    Write-Error "WebView2 SDK files are incomplete in $wvCache"
    exit 1
}
Write-Host "WebView2 WPF: $($wvWpf.FullName)"
Write-Host "WebView2 Core: $($wvCore.FullName)"
Write-Host "WebView2 Loader: $($wvLoader.FullName)"


# Find all .cs files (exclude test files and AdminHelperProgram)
$csFiles = Get-ChildItem -Path $srcDir -Filter "*.cs" -Recurse | Where-Object { $_.Name -ne "TestEntry.cs" -and $_.Name -ne "UnitTests.cs" -and $_.Name -ne "AIDecisionDemo.cs" -and $_.Name -ne "NeuralNetworkTest.cs" -and $_.Name -ne "OptimizationAITest.cs" -and $_.Name -ne "AdminHelperProgram.cs" } | ForEach-Object { $_.FullName }

$iconArg = if (Test-Path $iconPath) { "/win32icon:$iconPath" } else { $null }

$version = "2.7.7.0"

Write-Host "=== ClearWin Build ==="
Write-Host "Compiler: $csc"
Write-Host "Output: $exePath"
Write-Host "Version: $version"
Write-Host ""

$compilerArgs = @("/target:winexe", "/platform:x64", "/langversion:$langVersion", "/out:$exePath", "/win32manifest:$srcDir\app.manifest")
if ($iconArg) { $compilerArgs += $iconArg }
$compilerArgs += $refArgs
$compilerArgs += "/reference:$($wvCore.FullName)"
$compilerArgs += "/reference:$($wvWpf.FullName)"
$compilerArgs += $csFiles
Write-Host "Compiling ClearWin.exe..."
& $csc @compilerArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with compiler exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

# Compile ClearWin.AdminHelper.exe (Least-privilege dedicated elevated helper)
$helperExe = Join-Path $outDir "ClearWin.AdminHelper.exe"
$helperSrc = Join-Path $srcDir "AdminHelper\AdminHelperProgram.cs"
$helperManifest = Join-Path $srcDir "AdminHelper\app.manifest"
$helperArgs = @("/target:exe", "/platform:x64", "/langversion:$langVersion", "/out:$helperExe", "/win32manifest:$helperManifest", $helperSrc)
if ($iconArg) { $helperArgs += $iconArg }
Write-Host "Compiling ClearWin.AdminHelper.exe..."
& $csc @helperArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "Admin Helper compilation failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

if (Test-Path $exePath) {
    $size = (Get-Item $exePath).Length
    # Copy icon + WebView2 native loader + web assets + tweaks to output
    Copy-Item -Path "$projectRoot\Resources\app.ico" -Destination $outDir -Force -ErrorAction SilentlyContinue
    Copy-Item -Path $wvCore.FullName -Destination $outDir -Force -ErrorAction SilentlyContinue
    Copy-Item -Path $wvWpf.FullName -Destination $outDir -Force -ErrorAction SilentlyContinue
    $loaderDest = Join-Path $outDir "runtimes\win-x64\native"
    if (-not (Test-Path $loaderDest)) { New-Item -ItemType Directory -Path $loaderDest -Force | Out-Null }
    Copy-Item -Path $wvLoader.FullName -Destination (Join-Path $loaderDest "WebView2Loader.dll") -Force -ErrorAction SilentlyContinue
    $webDest = Join-Path $outDir "Resources\WebUI"
    if (Test-Path $webDest) { Remove-Item $webDest -Recurse -Force }
    New-Item -ItemType Directory -Path $webDest -Force | Out-Null
    Copy-Item -Path "$projectRoot\Resources\WebUI\*" -Destination $webDest -Recurse -Force
    $tweaksDest = Join-Path $outDir "Resources\tweaks"
    if (Test-Path "$projectRoot\Resources\tweaks") {
        if (-not (Test-Path $tweaksDest)) { New-Item -ItemType Directory -Path $tweaksDest -Force | Out-Null }
        Copy-Item -Path "$projectRoot\Resources\tweaks\*" -Destination $tweaksDest -Recurse -Force -ErrorAction SilentlyContinue
    }
    # Windows Optimization AI is in-app (neural ensemble) - no external model files
    # Update Desktop Shortcut
    try {
        $desktopPath = [System.IO.Path]::Combine($env:USERPROFILE, "Desktop")
        if (Test-Path $desktopPath) {
            $wshell = New-Object -ComObject WScript.Shell
            $shortcut = $wshell.CreateShortcut("$desktopPath\ClearWin.lnk")
            $shortcut.TargetPath = $exePath
            $shortcut.WorkingDirectory = $outDir
            $shortcut.IconLocation = Join-Path $outDir "app.ico"
            $shortcut.Save()
            try {
                $lnkPath = "$desktopPath\ClearWin.lnk"
                $bytes = [System.IO.File]::ReadAllBytes($lnkPath)
                $bytes[0x15] = ($bytes[0x15] -band (-bnot 0x20))
                [System.IO.File]::WriteAllBytes($lnkPath, $bytes)
            } catch { }
            Write-Host "Desktop shortcut updated successfully (Standard User / AsInvoker)!"
        }
    } catch {
        Write-Host "Warning: Could not update desktop shortcut: $_"
    }
    Write-Host "Build successful! ($([Math]::Round($size/1KB, 1)) KB)"

    # Compile Inno Setup Installer if compiler is available
    $isccPaths = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\iscc.exe",
        "C:\Users\maher\AppData\Local\Programs\Inno Setup 6\iscc.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\iscc.exe",
        "${env:ProgramFiles}\Inno Setup 6\iscc.exe"
    )
    $iscc = $isccPaths | Where-Object { Test-Path $_ } | Select-Object -First 1
    $issFile = Join-Path $projectRoot "installer.iss"
    if ($iscc -and (Test-Path $issFile)) {
        Write-Host "Compiling Inno Setup release installer..." -ForegroundColor Cyan
        & $iscc $issFile | Out-Null
        $setupExe = Join-Path $projectRoot "release\v2.7.7\ClearWin-Setup-2.7.7.exe"
        if (Test-Path $setupExe) {
            $setupSize = [Math]::Round((Get-Item $setupExe).Length / 1MB, 2)
            Write-Host "Release installer ready: $setupExe ($setupSize MB)" -ForegroundColor Green
        }
    }
} else {
    Write-Host "Build failed."
}
