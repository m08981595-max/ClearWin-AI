#Requires -Version 5.1
<#
.SYNOPSIS
    ClearWin AI v2.7.7 — Official Windows Graphical Setup Wizard & Installer
.DESCRIPTION
    Provides a modern Windows Setup Wizard GUI (with installation path selector, disk space check,
    live progress bar, percentage & remaining time indicator, automatic Roslyn source compilation,
    desktop/start-menu shortcut creation, Uninstaller GUI deployment, and Windows Installed Apps
    registration), as well as a -Silent mode for automated scripts.
#>
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'ClearWin'),
    [switch]$Gui,
    [switch]$Silent
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($projectRoot)) {
    $projectRoot = (Get-Location).Path
}
# If launched from wrapper parent folder, resolve actual engine folder if needed
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'src') -PathType Container)) {
    $subEngine = Join-Path $projectRoot '_ClearWin_Engine'
    if (Test-Path -LiteralPath (Join-Path $subEngine 'src') -PathType Container) {
        $projectRoot = $subEngine
    }
}

# Reusable core installation logic invoked by both Silent mode and GUI background worker
$InstallCoreScriptBlock = {
    param(
        [string]$ProjectRoot,
        [string]$TargetDir,
        [bool]$CreateDesktopShortcut,
        [bool]$CreateUninstallShortcut,
        [bool]$RegisterInWindows,
        [hashtable]$SyncState
    )

    function Update-Progress([int]$Percent, [string]$StepTitle, [string]$Detail, [int]$RemainingSeconds) {
        if ($null -ne $SyncState) {
            $SyncState.Percent = $Percent
            $SyncState.StepTitle = $StepTitle
            $SyncState.Detail = $Detail
            $SyncState.RemainingSeconds = $RemainingSeconds
            if (-not [string]::IsNullOrWhiteSpace($Detail)) {
                $SyncState.Logs.Enqueue(("[" + (Get-Date -Format "HH:mm:ss") + "] " + $Detail))
            }
        } else {
            Write-Host ("[{0,3}%] {1} - {2}" -f $Percent, $StepTitle, $Detail)
        }
    }

    try {
        $ErrorActionPreference = 'Stop'
        Update-Progress 5 "Step 1/6: Checking system requirements..." "Verifying Microsoft Edge WebView2 Runtime..." 18

        $runtimeCheck = Join-Path $ProjectRoot 'runtime-check.ps1'
        if (Test-Path -LiteralPath $runtimeCheck -PathType Leaf) {
            $runtimeProcess = Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-WindowStyle', 'Hidden', '-File', ('"{0}"' -f $runtimeCheck)) -Wait -PassThru -WindowStyle Hidden
            if ($runtimeProcess.ExitCode -ne 0) {
                throw "Microsoft Edge WebView2 Runtime is unavailable (Exit code: $($runtimeProcess.ExitCode))."
            }
        }

        Update-Progress 18 "Step 2/6: Building ClearWin AI v2.7.7..." "Compiling latest repaired C# source code via Roslyn..." 14
        $buildScript = Join-Path $ProjectRoot 'build.ps1'
        $srcDirCheck = Join-Path $ProjectRoot 'src'
        if ((Test-Path -LiteralPath $buildScript -PathType Leaf) -and (Test-Path -LiteralPath $srcDirCheck -PathType Container)) {
            $buildProc = Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-WindowStyle', 'Hidden', '-File', ('"{0}"' -f $buildScript)) -Wait -PassThru -WindowStyle Hidden
            if ($buildProc.ExitCode -ne 0) {
                Update-Progress 30 "Step 2/6: Using pre-built binaries..." "Build script returned $($buildProc.ExitCode); checking existing bin\ output..." 11
            } else {
                Update-Progress 38 "Step 2/6: Compilation complete!" "Compiled ClearWin.exe and ClearWin.AdminHelper.exe (v2.7.7) successfully." 10
            }
        }

        $srcExe = Join-Path $ProjectRoot 'bin\ClearWin.exe'
        $srcAdminHelper = Join-Path $ProjectRoot 'bin\ClearWin.AdminHelper.exe'
        $srcIco = Join-Path $ProjectRoot 'Resources\app.ico'
        $srcTweaks = Join-Path $ProjectRoot 'Resources\tweaks'
        $srcWeb = Join-Path $ProjectRoot 'Resources\WebUI'
        $srcCore = Join-Path $ProjectRoot 'bin\Microsoft.Web.WebView2.Core.dll'
        $srcWpf = Join-Path $ProjectRoot 'bin\Microsoft.Web.WebView2.Wpf.dll'
        $srcLoader = Join-Path $ProjectRoot 'bin\runtimes\win-x64\native\WebView2Loader.dll'

        $sourceRequired = @(
            $srcExe,
            $srcAdminHelper,
            $srcCore,
            $srcWpf,
            $srcLoader,
            (Join-Path $srcWeb 'index.html')
        )
        foreach ($req in $sourceRequired) {
            if (-not (Test-Path -LiteralPath $req -PathType Leaf)) {
                throw "Required build file is missing: $req"
            }
        }

        Update-Progress 45 "Step 3/6: Preparing installation folder..." ("Target directory: " + $TargetDir) 8
        if (-not (Test-Path -LiteralPath $TargetDir -PathType Container)) {
            New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
        }

        # Stop any running ClearWin instance before replacing files
        $running = @(Get-Process -Name 'ClearWin','ClearWin.AdminHelper' -ErrorAction SilentlyContinue)
        $clearWinPids = @($running | ForEach-Object { $_.Id })
        if ($running.Count -gt 0) {
            Update-Progress 50 "Step 3/6: Stopping active ClearWin processes..." "Closing running ClearWin instance for safe update..." 7
            foreach ($proc in $running) {
                try { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue } catch {}
            }
            Start-Sleep -Milliseconds 700
            try {
                $wvChildren = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
                    Where-Object { $_.Name -in @('msedgewebview2.exe','msedge.exe') -and $clearWinPids -contains ([int]$_.ParentProcessId) }
                foreach ($child in @($wvChildren)) {
                    try { Stop-Process -Id ([int]$child.ProcessId) -Force -ErrorAction SilentlyContinue } catch {}
                }
            } catch {}
            Start-Sleep -Milliseconds 400
        }

        function Copy-WithRetry([string]$Src, [string]$Dst) {
            $parent = Split-Path -Parent $Dst
            if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
                New-Item -ItemType Directory -Path $parent -Force | Out-Null
            }
            for ($attempt = 1; $attempt -le 8; $attempt++) {
                try {
                    Copy-Item -LiteralPath $Src -Destination $Dst -Force -ErrorAction Stop
                    return
                } catch {
                    if ($attempt -eq 8) { throw }
                    Start-Sleep -Milliseconds 500
                }
            }
        }

        # Collect all files to copy so we can report accurate per-file progress and remaining time
        $copyQueue = New-Object System.Collections.Generic.List[object]
        $copyQueue.Add(@{ Src = $srcExe; Dst = (Join-Path $TargetDir 'ClearWin.exe'); Label = 'ClearWin.exe' })
        $copyQueue.Add(@{ Src = $srcAdminHelper; Dst = (Join-Path $TargetDir 'ClearWin.AdminHelper.exe'); Label = 'ClearWin.AdminHelper.exe' })
        $copyQueue.Add(@{ Src = $srcCore; Dst = (Join-Path $TargetDir 'Microsoft.Web.WebView2.Core.dll'); Label = 'Microsoft.Web.WebView2.Core.dll' })
        $copyQueue.Add(@{ Src = $srcWpf; Dst = (Join-Path $TargetDir 'Microsoft.Web.WebView2.Wpf.dll'); Label = 'Microsoft.Web.WebView2.Wpf.dll' })
        $copyQueue.Add(@{ Src = $srcLoader; Dst = (Join-Path $TargetDir 'runtimes\win-x64\native\WebView2Loader.dll'); Label = 'WebView2Loader.dll' })
        if (Test-Path -LiteralPath $srcIco -PathType Leaf) {
            $copyQueue.Add(@{ Src = $srcIco; Dst = (Join-Path $TargetDir 'app.ico'); Label = 'app.ico' })
        }

        foreach ($uFile in @('Uninstall-ClearWin.exe', 'Uninstall-ClearWin.ps1', 'Uninstall-ClearWin.cmd', 'runtime-check.ps1')) {
            $sU = Join-Path $ProjectRoot $uFile
            if (Test-Path -LiteralPath $sU -PathType Leaf) {
                $copyQueue.Add(@{ Src = $sU; Dst = (Join-Path $TargetDir $uFile); Label = $uFile })
            }
        }

        if (Test-Path -LiteralPath $srcWeb -PathType Container) {
            foreach ($wf in (Get-ChildItem -LiteralPath $srcWeb -Recurse -File)) {
                $rel = $wf.FullName.Substring($srcWeb.Length).TrimStart('\','/')
                $copyQueue.Add(@{ Src = $wf.FullName; Dst = (Join-Path $TargetDir (Join-Path 'Resources\WebUI' $rel)); Label = ("WebUI\" + $rel) })
            }
        }

        if (Test-Path -LiteralPath $srcTweaks -PathType Container) {
            foreach ($tf in (Get-ChildItem -LiteralPath $srcTweaks -Recurse -File)) {
                $rel = $tf.FullName.Substring($srcTweaks.Length).TrimStart('\','/')
                $copyQueue.Add(@{ Src = $tf.FullName; Dst = (Join-Path $TargetDir (Join-Path 'Resources\tweaks' $rel)); Label = ("tweaks\" + $rel) })
            }
        }

        $totalFiles = [Math]::Max(1, $copyQueue.Count)
        for ($idx = 0; $idx -lt $copyQueue.Count; $idx++) {
            $item = $copyQueue[$idx]
            Copy-WithRetry -Src $item.Src -Dst $item.Dst
            $pct = 52 + [int]([Math]::Floor((($idx + 1) / [double]$totalFiles) * 32.0))
            $remSec = [Math]::Max(2, [int]([Math]::Ceiling((($totalFiles - ($idx + 1)) / [double]$totalFiles) * 5.0)) + 2)
            if (($idx % 4 -eq 0) -or ($idx -eq $copyQueue.Count - 1)) {
                Update-Progress $pct ("Step 4/6: Installing files (" + ($idx + 1) + "/" + $totalFiles + ")...") ("Copying: " + $item.Label) $remSec
                Start-Sleep -Milliseconds 35
            }
        }

        # Record installed location marker so Uninstaller knows exact approved install path
        Set-Content -LiteralPath (Join-Path $TargetDir 'install-location.txt') -Value $TargetDir -Encoding UTF8 -Force

        Update-Progress 88 "Step 5/6: Creating shortcuts & Uninstaller icon..." "Creating Desktop and Start Menu icons..." 2
        $exeInstalled = Join-Path $TargetDir 'ClearWin.exe'
        $icoInstalled = Join-Path $TargetDir 'app.ico'
        $uninstExeInstalled = Join-Path $TargetDir 'Uninstall-ClearWin.exe'
        $uninstTarget = if (Test-Path -LiteralPath $uninstExeInstalled -PathType Leaf) { $uninstExeInstalled } else { (Join-Path $TargetDir 'Uninstall-ClearWin.cmd') }

        $ws = New-Object -ComObject WScript.Shell

        # Start Menu folder & shortcuts
        $startMenuPrograms = Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs'
        $clearWinMenuDir = Join-Path $startMenuPrograms 'ClearWin AI'
        if (-not (Test-Path -LiteralPath $clearWinMenuDir -PathType Container)) {
            New-Item -ItemType Directory -Path $clearWinMenuDir -Force | Out-Null
        }

        foreach ($smAppPath in @((Join-Path $startMenuPrograms 'ClearWin.lnk'), (Join-Path $clearWinMenuDir 'ClearWin AI.lnk'))) {
            $sc = $ws.CreateShortcut($smAppPath)
            $sc.TargetPath = $exeInstalled
            $sc.WorkingDirectory = $TargetDir
            $sc.IconLocation = "$icoInstalled,0"
            $sc.Description = 'ClearWin Windows AI Optimizer v2.7.7'
            $sc.Save()
        }

        foreach ($smUninstPath in @((Join-Path $startMenuPrograms 'Uninstall ClearWin.lnk'), (Join-Path $clearWinMenuDir 'Uninstall ClearWin.lnk'))) {
            $scU = $ws.CreateShortcut($smUninstPath)
            $scU.TargetPath = $uninstTarget
            $scU.WorkingDirectory = $TargetDir
            $scU.IconLocation = "$icoInstalled,0"
            $scU.Description = 'Uninstall ClearWin AI v2.7.7'
            $scU.Save()
        }

        # Desktop shortcuts (App icon + Uninstaller icon)
        $desktop = [Environment]::GetFolderPath('Desktop')
        if (-not [string]::IsNullOrWhiteSpace($desktop)) {
            if ($CreateDesktopShortcut) {
                $deskApp = Join-Path $desktop 'ClearWin.lnk'
                $scD = $ws.CreateShortcut($deskApp)
                $scD.TargetPath = $exeInstalled
                $scD.WorkingDirectory = $TargetDir
                $scD.IconLocation = "$icoInstalled,0"
                $scD.Description = 'ClearWin Windows AI Optimizer v2.7.7'
                $scD.Save()
            }
            if ($CreateUninstallShortcut) {
                $deskUninst = Join-Path $desktop 'Uninstall ClearWin.lnk'
                $scDU = $ws.CreateShortcut($deskUninst)
                $scDU.TargetPath = $uninstTarget
                $scDU.WorkingDirectory = $TargetDir
                $scDU.IconLocation = "$icoInstalled,0"
                $scDU.Description = 'Uninstall ClearWin AI v2.7.7'
                $scDU.Save()
            }
        }

        # Register in Windows Installed Apps (HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\ClearWin)
        if ($RegisterInWindows) {
            Update-Progress 94 "Step 5/6: Registering in Windows Installed Apps..." "Writing Windows Uninstall registry key..." 1
            $uninstRegPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ClearWin'
            if (-not (Test-Path -LiteralPath $uninstRegPath)) {
                New-Item -Path $uninstRegPath -Force | Out-Null
            }
            New-ItemProperty -Path $uninstRegPath -Name 'DisplayName' -Value 'ClearWin AI v2.7.7' -PropertyType String -Force | Out-Null
            New-ItemProperty -Path $uninstRegPath -Name 'DisplayVersion' -Value '2.7.7' -PropertyType String -Force | Out-Null
            New-ItemProperty -Path $uninstRegPath -Name 'Publisher' -Value 'ClearWin AI Engineering' -PropertyType String -Force | Out-Null
            New-ItemProperty -Path $uninstRegPath -Name 'DisplayIcon' -Value "$icoInstalled,0" -PropertyType String -Force | Out-Null
            New-ItemProperty -Path $uninstRegPath -Name 'InstallLocation' -Value $TargetDir -PropertyType String -Force | Out-Null
            New-ItemProperty -Path $uninstRegPath -Name 'UninstallString' -Value ('"{0}"' -f $uninstTarget) -PropertyType String -Force | Out-Null
            New-ItemProperty -Path $uninstRegPath -Name 'NoModify' -Value 1 -PropertyType DWord -Force | Out-Null
            New-ItemProperty -Path $uninstRegPath -Name 'NoRepair' -Value 1 -PropertyType DWord -Force | Out-Null
            New-ItemProperty -Path $uninstRegPath -Name 'EstimatedSize' -Value 48500 -PropertyType DWord -Force | Out-Null
        }

        Update-Progress 97 "Step 6/6: Verifying installation integrity..." "Checking SHA-256 hash of installed ClearWin.exe..." 1
        $srcHash = (Get-FileHash -LiteralPath $srcExe -Algorithm SHA256).Hash
        $dstHash = (Get-FileHash -LiteralPath $exeInstalled -Algorithm SHA256).Hash
        if ($srcHash -ne $dstHash) {
            throw "Installed ClearWin.exe SHA-256 mismatch."
        }

        Update-Progress 100 "Installation Complete!" ("ClearWin AI v2.7.7 is installed at: " + $TargetDir) 0
        if ($null -ne $SyncState) {
            $SyncState.Completed = $true
            $SyncState.Success = $true
            $SyncState.InstalledExe = $exeInstalled
        }
    } catch {
        if ($null -ne $SyncState) {
            $SyncState.ErrorMessage = $_.Exception.Message
            $SyncState.Logs.Enqueue(("[ERROR] " + $_.Exception.Message))
            $SyncState.Completed = $true
            $SyncState.Success = $false
        } else {
            throw
        }
    }
}

if ($Silent) {
    & $InstallCoreScriptBlock -ProjectRoot $projectRoot -TargetDir $InstallDir -CreateDesktopShortcut $true -CreateUninstallShortcut $true -RegisterInWindows $true -SyncState $null
    exit 0
}

# =========================================================================
# Graphical Windows Setup Wizard Mode (WPF Fluent Dark/Modern Windows UI)
# =========================================================================
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Windows.Forms, System.Drawing

[xml]$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="معالج تثبيت ClearWin AI v2.7.7 — Setup Wizard"
        Width="700" Height="520"
        WindowStartupLocation="CenterScreen"
        ResizeMode="CanMinimize"
        Background="#0F172A"
        Foreground="#F8FAFC"
        FontFamily="Segoe UI, Tahoma"
        FlowDirection="RightToLeft">
    <Grid Margin="24">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Header Banner -->
        <Border Grid.Row="0" Background="#1E293B" CornerRadius="12" Padding="18,14" BorderBrush="#334155" BorderThickness="1" Margin="0,0,0,18">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <Image x:Name="LogoImg" Grid.Column="0" Width="48" Height="48" Margin="0,0,14,0" VerticalAlignment="Center"/>
                <StackPanel Grid.Column="1" VerticalAlignment="Center">
                    <TextBlock Text="تثبيت ClearWin AI — الإصدار v2.7.7" FontSize="20" FontWeight="Bold" Foreground="#38BDF8"/>
                    <TextBlock Text="محرك تحسين وتسريع نظام Windows الذكي (نسخة مهندسة ومحققة بالكامل)" FontSize="12.5" Foreground="#94A3B8" Margin="0,3,0,0"/>
                </StackPanel>
                <Border Grid.Column="2" Background="#0284C7" CornerRadius="6" Padding="10,4" VerticalAlignment="Top">
                    <TextBlock Text="v2.7.7 Official" FontSize="11" FontWeight="SemiBold" Foreground="White" FlowDirection="LeftToRight"/>
                </Border>
            </Grid>
        </Border>

        <!-- Page 1: Configuration & Install Location -->
        <StackPanel x:Name="PageConfig" Grid.Row="1" Visibility="Visible">
            <TextBlock Text="اختر مكان التثبيت وخيارات الاختصارات:" FontSize="14.5" FontWeight="SemiBold" Foreground="#E2E8F0" Margin="0,0,0,10"/>

            <Border Background="#1E293B" CornerRadius="10" Padding="16" BorderBrush="#334155" BorderThickness="1" Margin="0,0,0,14">
                <StackPanel>
                    <TextBlock Text="مكان التثبيت على القرص (Installation Folder):" FontSize="12.5" Foreground="#CBD5E1" Margin="0,0,0,8"/>
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>
                        <TextBox x:Name="TxtInstallPath" Grid.Column="0" Height="36" VerticalContentAlignment="Center"
                                 Background="#0F172A" Foreground="#F8FAFC" BorderBrush="#475569" BorderThickness="1"
                                 Padding="10,0" FontSize="13" FlowDirection="LeftToRight"/>
                        <Button x:Name="BtnBrowse" Grid.Column="1" Content="تصفح..." Width="100" Height="36" Margin="10,0,0,0"
                                Background="#334155" Foreground="White" BorderBrush="#475569" FontWeight="SemiBold" Cursor="Hand"/>
                    </Grid>
                    <TextBlock x:Name="TxtDiskSpace" Text="المساحة المطلوبة: ~48.5 ميجابايت | المساحة المتوفرة: جاري الحساب..."
                               FontSize="12" Foreground="#38BDF8" Margin="0,10,0,0"/>
                </StackPanel>
            </Border>

            <Border Background="#1E293B" CornerRadius="10" Padding="16" BorderBrush="#334155" BorderThickness="1">
                <StackPanel>
                    <TextBlock Text="خيارات الأيقونات والتكامل مع نظام Windows:" FontSize="12.5" Foreground="#CBD5E1" Margin="0,0,0,10"/>
                    <CheckBox x:Name="ChkDesktopIcon" IsChecked="True" Foreground="#F1F5F9" FontSize="13" Margin="0,0,0,10"
                              Content="إنشاء أيقونة تشغيل ClearWin AI على سطح المكتب وقائمة ابدأ"/>
                    <CheckBox x:Name="ChkUninstallIcon" IsChecked="True" Foreground="#F1F5F9" FontSize="13" Margin="0,0,0,10"
                              Content="إنشاء أيقونة إلغاء التثبيت (Uninstall ClearWin) بنفس ستايل ويندوز على سطح المكتب وقائمة ابدأ"/>
                    <CheckBox x:Name="ChkRegisterWin" IsChecked="True" Foreground="#F1F5F9" FontSize="13"
                              Content="تسجيل البرنامج في قائمة التطبيقات المثبتة في إعدادات Windows (Apps &amp; Features)"/>
                </StackPanel>
            </Border>
        </StackPanel>

        <!-- Page 2: Installing Progress (ProgressBar + Remaining Time & Percentage) -->
        <Grid x:Name="PageProgress" Grid.Row="1" Visibility="Collapsed">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
            </Grid.RowDefinitions>

            <Grid Grid.Row="0" Margin="0,4,0,8">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <TextBlock x:Name="TxtStepTitle" Grid.Column="0" Text="جاري تجهيز التثبيت..." FontSize="15" FontWeight="Bold" Foreground="#F8FAFC"/>
                <TextBlock x:Name="TxtPercent" Grid.Column="1" Text="0%" FontSize="18" FontWeight="Bold" Foreground="#38BDF8" FlowDirection="LeftToRight"/>
            </Grid>

            <ProgressBar x:Name="ProgBar" Grid.Row="1" Height="22" Minimum="0" Maximum="100" Value="0"
                         Background="#1E293B" Foreground="#0284C7" BorderBrush="#334155" BorderThickness="1"/>

            <Grid Grid.Row="2" Margin="0,8,0,12">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <TextBlock x:Name="TxtDetail" Grid.Column="0" Text="جاري التحميل..." FontSize="12.5" Foreground="#94A3B8" TextTrimming="CharacterEllipsis"/>
                <TextBlock x:Name="TxtRemaining" Grid.Column="1" Text="المتبقي: 100% (~18 ثانية)" FontSize="12.5" FontWeight="SemiBold" Foreground="#FBBF24"/>
            </Grid>

            <Border Grid.Row="3" Background="#090D16" CornerRadius="8" BorderBrush="#1E293B" BorderThickness="1" Padding="10">
                <TextBox x:Name="TxtLog" Background="Transparent" Foreground="#A7F3D0" BorderThickness="0"
                         IsReadOnly="True" TextWrapping="Wrap" VerticalScrollBarVisibility="Auto"
                         FontFamily="Consolas" FontSize="11.5" FlowDirection="LeftToRight"/>
            </Border>
        </Grid>

        <!-- Page 3: Completion Screen -->
        <StackPanel x:Name="PageDone" Grid.Row="1" Visibility="Collapsed" VerticalAlignment="Center" HorizontalAlignment="Center">
            <Border Width="68" Height="68" CornerRadius="34" Background="#065F46" HorizontalAlignment="Center" Margin="0,0,0,14">
                <TextBlock Text="✓" FontSize="36" FontWeight="Bold" Foreground="#34D399" HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Border>
            <TextBlock Text="اكتمل تثبيت ClearWin AI v2.7.7 بنجاح!" FontSize="20" FontWeight="Bold" Foreground="#34D399" HorizontalAlignment="Center"/>
            <TextBlock x:Name="TxtDonePath" Text="" FontSize="12.5" Foreground="#94A3B8" HorizontalAlignment="Center" Margin="0,8,0,18" FlowDirection="LeftToRight"/>
            <CheckBox x:Name="ChkLaunchNow" IsChecked="True" Content="تشغيل برنامج ClearWin AI الآن عند إغلاق المعالج"
                      Foreground="#F8FAFC" FontSize="14" FontWeight="SemiBold" HorizontalAlignment="Center"/>
        </StackPanel>

        <!-- Footer Buttons -->
        <Border Grid.Row="2" BorderBrush="#1E293B" BorderThickness="0,1,0,0" Padding="0,14,0,0" Margin="0,14,0,0">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Text="ClearWin AI Setup Wizard • Windows 10 / 11 (x64)" VerticalAlignment="Center" FontSize="11.5" Foreground="#64748B"/>
                <Button x:Name="BtnCancel" Grid.Column="1" Content="إلغاء" Width="110" Height="38" Margin="0,0,10,0"
                        Background="#1E293B" Foreground="#E2E8F0" BorderBrush="#334155" FontSize="13.5" Cursor="Hand"/>
                <Button x:Name="BtnPrimary" Grid.Column="2" Content="تثبيت الآن (Install)" Width="165" Height="38"
                        Background="#0284C7" Foreground="White" BorderBrush="#38BDF8" FontSize="13.5" FontWeight="Bold" Cursor="Hand"/>
            </Grid>
        </Border>
    </Grid>
</Window>
"@

$reader = New-Object System.Xml.XmlNodeReader $xaml
$window = [Windows.Markup.XamlReader]::Load($reader)

$LogoImg = $window.FindName('LogoImg')
$PageConfig = $window.FindName('PageConfig')
$PageProgress = $window.FindName('PageProgress')
$PageDone = $window.FindName('PageDone')
$TxtInstallPath = $window.FindName('TxtInstallPath')
$BtnBrowse = $window.FindName('BtnBrowse')
$TxtDiskSpace = $window.FindName('TxtDiskSpace')
$ChkDesktopIcon = $window.FindName('ChkDesktopIcon')
$ChkUninstallIcon = $window.FindName('ChkUninstallIcon')
$ChkRegisterWin = $window.FindName('ChkRegisterWin')
$TxtStepTitle = $window.FindName('TxtStepTitle')
$TxtPercent = $window.FindName('TxtPercent')
$ProgBar = $window.FindName('ProgBar')
$TxtDetail = $window.FindName('TxtDetail')
$TxtRemaining = $window.FindName('TxtRemaining')
$TxtLog = $window.FindName('TxtLog')
$TxtDonePath = $window.FindName('TxtDonePath')
$ChkLaunchNow = $window.FindName('ChkLaunchNow')
$BtnCancel = $window.FindName('BtnCancel')
$BtnPrimary = $window.FindName('BtnPrimary')

$icoFile = Join-Path $projectRoot 'Resources\app.ico'
if (Test-Path -LiteralPath $icoFile -PathType Leaf) {
    try {
        $uri = New-Object System.Uri($icoFile, [System.UriKind]::Absolute)
        $bmp = New-Object System.Windows.Media.Imaging.BitmapImage($uri)
        $window.Icon = $bmp
        $LogoImg.Source = $bmp
    } catch {}
}

$TxtInstallPath.Text = $InstallDir

function Update-DiskSpaceInfo([string]$pathStr) {
    try {
        $rootDrive = [System.IO.Path]::GetPathRoot($pathStr)
        if (-not [string]::IsNullOrWhiteSpace($rootDrive)) {
            $di = New-Object System.IO.DriveInfo($rootDrive)
            if ($di.IsReady) {
                $freeGb = [Math]::Round($di.AvailableFreeSpace / 1GB, 1)
                $TxtDiskSpace.Text = "المساحة المطلوبة للتثبيت: ~48.5 ميجابايت  |  المساحة المتوفرة على القرص ($($di.Name.TrimEnd('\'))): $freeGb جيجابايت"
                return
            }
        }
    } catch {}
    $TxtDiskSpace.Text = "المساحة المطلوبة للتثبيت: ~48.5 ميجابايت"
}

Update-DiskSpaceInfo $TxtInstallPath.Text
$TxtInstallPath.Add_TextChanged({ Update-DiskSpaceInfo $TxtInstallPath.Text })

$BtnBrowse.Add_Click({
    $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
    $dlg.Description = "اختر مجلد تثبيت ClearWin AI v2.7.7"
    $dlg.SelectedPath = $TxtInstallPath.Text
    if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        $selected = $dlg.SelectedPath
        if ($selected -notmatch 'ClearWin$') {
            $selected = Join-Path $selected 'ClearWin'
        }
        $TxtInstallPath.Text = $selected
    }
})

$syncState = [hashtable]::Synchronized(@{
    Percent = 0
    StepTitle = "جاري بدء التثبيت..."
    Detail = ""
    RemainingSeconds = 18
    Logs = (New-Object System.Collections.Concurrent.ConcurrentQueue[string])
    Completed = $false
    Success = $false
    ErrorMessage = ""
    InstalledExe = ""
})

$script:wizardStage = "Config"
$script:psInstance = $null
$script:timer = New-Object System.Windows.Threading.DispatcherTimer
$script:timer.Interval = [TimeSpan]::FromMilliseconds(100)

$script:timer.Add_Tick({
    $pct = [int]$syncState.Percent
    $remPct = 100 - $pct
    $remSec = [int]$syncState.RemainingSeconds
    $ProgBar.Value = $pct
    $TxtPercent.Text = "$pct%"
    $TxtStepTitle.Text = [string]$syncState.StepTitle
    $TxtDetail.Text = [string]$syncState.Detail
    if ($pct -lt 100) {
        $TxtRemaining.Text = "فاضل: $remPct% (حوالي $remSec ثانية)"
    } else {
        $TxtRemaining.Text = "اكتمل التحميل والتثبيت 100%"
    }

    $logLine = $null
    $appended = $false
    while ($syncState.Logs.TryDequeue([ref]$logLine)) {
        $TxtLog.AppendText($logLine + [Environment]::NewLine)
        $appended = $true
    }
    if ($appended) { $TxtLog.ScrollToEnd() }

    if ($syncState.Completed) {
        $script:timer.Stop()
        if ($null -ne $script:psInstance) {
            try { $script:psInstance.Dispose() } catch {}
            $script:psInstance = $null
        }
        if ($syncState.Success) {
            $script:wizardStage = "Done"
            $PageProgress.Visibility = [System.Windows.Visibility]::Collapsed
            $PageDone.Visibility = [System.Windows.Visibility]::Visible
            $TxtDonePath.Text = $TxtInstallPath.Text
            $BtnCancel.Visibility = [System.Windows.Visibility]::Collapsed
            $BtnPrimary.IsEnabled = $true
            $BtnPrimary.Content = "إنهاء (Finish)"
        } else {
            $TxtStepTitle.Text = "تعذر إكمال التثبيت"
            $TxtRemaining.Text = "حدث خطأ أثناء التثبيت"
            $BtnCancel.IsEnabled = $true
            $BtnCancel.Content = "إغلاق"
            [System.Windows.MessageBox]::Show("حدث خطأ أثناء التثبيت:`n" + $syncState.ErrorMessage, "ClearWin AI Setup", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Error) | Out-Null
        }
    }
})

$BtnPrimary.Add_Click({
    if ($script:wizardStage -eq "Config") {
        $chosenDir = $TxtInstallPath.Text.Trim()
        if ([string]::IsNullOrWhiteSpace($chosenDir)) {
            [System.Windows.MessageBox]::Show("يرجى اختيار مسار تثبيت صالح.", "ClearWin AI Setup") | Out-Null
            return
        }
        $script:wizardStage = "Installing"
        $PageConfig.Visibility = [System.Windows.Visibility]::Collapsed
        $PageProgress.Visibility = [System.Windows.Visibility]::Visible
        $BtnPrimary.IsEnabled = $false
        $BtnCancel.IsEnabled = $false

        $script:psInstance = [powershell]::Create()
        [void]$script:psInstance.AddScript($InstallCoreScriptBlock)
        [void]$script:psInstance.AddArgument($projectRoot)
        [void]$script:psInstance.AddArgument($chosenDir)
        [void]$script:psInstance.AddArgument([bool]$ChkDesktopIcon.IsChecked)
        [void]$script:psInstance.AddArgument([bool]$ChkUninstallIcon.IsChecked)
        [void]$script:psInstance.AddArgument([bool]$ChkRegisterWin.IsChecked)
        [void]$script:psInstance.AddArgument($syncState)
        [void]$script:psInstance.BeginInvoke()
        $script:timer.Start()
    }
    elseif ($script:wizardStage -eq "Done") {
        if ($ChkLaunchNow.IsChecked -and (Test-Path -LiteralPath $syncState.InstalledExe -PathType Leaf)) {
            try {
                Start-Process -FilePath $syncState.InstalledExe -WorkingDirectory (Split-Path -Parent $syncState.InstalledExe)
            } catch {}
        }
        $window.Close()
    }
})

$BtnCancel.Add_Click({
    if ($script:wizardStage -ne "Installing") {
        $window.Close()
    }
})

[void]$window.ShowDialog()
