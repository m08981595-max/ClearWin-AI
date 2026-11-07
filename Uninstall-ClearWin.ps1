#Requires -Version 5.1
<#
.SYNOPSIS
    ClearWin AI v2.7.7 — Official Windows Graphical Uninstaller Wizard
.DESCRIPTION
    Provides a standard Windows Uninstaller GUI (matching the Setup Wizard style, with progress bar,
    percentage & remaining time counter, safe restoration of Windows power plans and Registry journal,
    shortcut cleanup, and optional user data backup) as well as a -Silent mode.
#>
param(
    [string]$InstallRoot = "",
    [switch]$Gui,
    [switch]$Silent
)

$ErrorActionPreference = 'Stop'

# Resolve installation directory safely
if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    $regUninst = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ClearWin'
    if (Test-Path -LiteralPath $regUninst) {
        try {
            $regLoc = (Get-ItemProperty -Path $regUninst -Name 'InstallLocation' -ErrorAction SilentlyContinue).InstallLocation
            if (-not [string]::IsNullOrWhiteSpace($regLoc)) { $InstallRoot = $regLoc }
        } catch {}
    }
}
if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    $scriptParent = Split-Path -Parent $MyInvocation.MyCommand.Path
    if (-not [string]::IsNullOrWhiteSpace($scriptParent) -and (Test-Path -LiteralPath (Join-Path $scriptParent 'ClearWin.exe') -PathType Leaf)) {
        $InstallRoot = $scriptParent
    } else {
        $InstallRoot = Join-Path $env:LOCALAPPDATA 'ClearWin'
    }
}

# If running from inside $InstallRoot, stage this script to $env:TEMP and relaunch so $InstallRoot can be deleted cleanly
$currentScriptPath = $MyInvocation.MyCommand.Path
if (-not [string]::IsNullOrWhiteSpace($currentScriptPath)) {
    $normScriptDir = [IO.Path]::GetFullPath((Split-Path -Parent $currentScriptPath)).TrimEnd('\')
    $normInstallRoot = [IO.Path]::GetFullPath($InstallRoot).TrimEnd('\')
    if ([string]::Equals($normScriptDir, $normInstallRoot, [StringComparison]::OrdinalIgnoreCase)) {
        $tempCopy = Join-Path $env:TEMP ("ClearWin_Uninstaller_" + [Guid]::NewGuid().ToString("N") + ".ps1")
        Copy-Item -LiteralPath $currentScriptPath -Destination $tempCopy -Force
        $argList = @('-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-WindowStyle', 'Hidden', '-File', ('"{0}"' -f $tempCopy), '-InstallRoot', ('"{0}"' -f $normInstallRoot))
        if ($Silent) { $argList += '-Silent' } else { $argList += '-Gui' }
        Start-Process -FilePath 'powershell.exe' -ArgumentList $argList -WindowStyle Hidden
        exit 0
    }
}

$UninstallCoreScriptBlock = {
    param(
        [string]$TargetRoot,
        [bool]$PreserveUserData,
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
        $root = [IO.Path]::GetFullPath($TargetRoot).TrimEnd('\')
        $defaultExpected = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'ClearWin')).TrimEnd('\')
        $markerPath = Join-Path $root 'install-location.txt'
        $isApprovedRoot = [string]::Equals($root, $defaultExpected, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $markerPath -PathType Leaf) -or (Test-Path -LiteralPath (Join-Path $root 'ClearWin.exe') -PathType Leaf)
        if (-not $isApprovedRoot) {
            throw "Refusing to remove an unverified directory: $root"
        }

        Update-Progress 15 "Step 1/5: Stopping ClearWin processes..." "Closing running ClearWin AI and AdminHelper instances..." 7
        Get-Process -Name 'ClearWin','ClearWin.AdminHelper' -ErrorAction SilentlyContinue | ForEach-Object {
            try { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue } catch {}
        }
        Start-Sleep -Milliseconds 1200

        function Invoke-PowerCfgChecked([string]$Arguments) {
            $p = Start-Process -FilePath 'powercfg.exe' -ArgumentList $Arguments -PassThru -Wait -WindowStyle Hidden
            return ($p.ExitCode -eq 0)
        }

        Update-Progress 35 "Step 2/5: Restoring Windows power settings..." "Checking game-mode-recovery.ini journal..." 5
        $journal = Join-Path $root 'game-mode-recovery.ini'
        if (-not (Test-Path -LiteralPath $journal -PathType Leaf)) {
            $altJournal = Join-Path (Join-Path $env:LOCALAPPDATA 'ClearWin') 'game-mode-recovery.ini'
            if (Test-Path -LiteralPath $altJournal -PathType Leaf) { $journal = $altJournal }
        }
        if (Test-Path -LiteralPath $journal -PathType Leaf) {
            $data = @{}
            foreach ($line in Get-Content -LiteralPath $journal) {
                $split = $line.IndexOf('=')
                if ($split -gt 0) { $data[$line.Substring(0,$split).Trim()] = $line.Substring($split+1).Trim() }
            }
            $plan = [string]$data['plan']; $parkMin = [string]$data['parkMin']; $parkMax = [string]$data['parkMax']
            $guidPattern = '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'
            if ($plan -match $guidPattern) {
                [void](Invoke-PowerCfgChecked ("/setactive " + $plan))
            }
            Remove-Item -LiteralPath $journal -Force -ErrorAction SilentlyContinue
        }

        Update-Progress 55 "Step 3/5: Restoring backed-up Registry values..." "Checking registry-restore-journal.ini..." 4
        $regJournal = Join-Path (Join-Path $env:LOCALAPPDATA 'ClearWin') 'registry-restore-journal.ini'
        if (Test-Path -LiteralPath $regJournal -PathType Leaf) {
            foreach ($line in Get-Content -LiteralPath $regJournal) {
                if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#')) { continue }
                $parts = $line.Split("`t")
                if ($parts.Length -ge 8) {
                    $hive = $parts[0]
                    $subKey = $parts[1]
                    $valName = $parts[2]
                    $valExisted = ($parts[4] -eq '1')
                    $origVal = $parts[6]
                    $writtenVal = if ($parts.Length -ge 9) { $parts[8] } else { '' }
                    if ($hive -eq 'HKCU') {
                        $psPath = "HKCU:\$subKey"
                        if (Test-Path -LiteralPath $psPath) {
                            try {
                                $curProp = Get-ItemProperty -LiteralPath $psPath -Name $valName -ErrorAction SilentlyContinue
                                if ($null -ne $curProp) {
                                    $curStr = [string]($curProp.$valName)
                                    if ($curStr -eq $writtenVal) {
                                        if (-not $valExisted) {
                                            Remove-ItemProperty -LiteralPath $psPath -Name $valName -ErrorAction SilentlyContinue
                                        } else {
                                            Set-ItemProperty -LiteralPath $psPath -Name $valName -Value $origVal -ErrorAction SilentlyContinue
                                        }
                                    }
                                }
                            } catch {}
                        }
                    }
                }
            }
            Remove-Item -LiteralPath $regJournal -Force -ErrorAction SilentlyContinue
        }

        if ($PreserveUserData -and (Test-Path -LiteralPath $root -PathType Container)) {
            Update-Progress 70 "Step 4/5: Backing up AI learning data & logs..." "Saving user telemetry to ClearWin-UserData..." 3
            $backup = Join-Path $env:LOCALAPPDATA ('ClearWin-UserData\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
            foreach ($name in @('agent','logs','crashes','tweak_backups')) {
                $source = Join-Path $root $name
                if (Test-Path -LiteralPath $source -PathType Container) {
                    $target = Join-Path $backup $name
                    New-Item -ItemType Directory -Path $backup -Force | Out-Null
                    Copy-Item -LiteralPath $source -Destination $target -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
        }

        Update-Progress 82 "Step 4/5: Removing shortcuts & Windows registration..." "Cleaning Desktop, Start Menu, and Uninstall registry..." 2
        $startMenuPrograms = Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs'
        $shortcutPaths = @(
            (Join-Path ([Environment]::GetFolderPath('Desktop')) 'ClearWin.lnk'),
            (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Uninstall ClearWin.lnk'),
            (Join-Path $startMenuPrograms 'ClearWin.lnk'),
            (Join-Path $startMenuPrograms 'Uninstall ClearWin.lnk')
        )
        foreach ($sc in $shortcutPaths) {
            Remove-Item -LiteralPath $sc -Force -ErrorAction SilentlyContinue
        }
        $clearWinMenuDir = Join-Path $startMenuPrograms 'ClearWin AI'
        if (Test-Path -LiteralPath $clearWinMenuDir -PathType Container) {
            Remove-Item -LiteralPath $clearWinMenuDir -Recurse -Force -ErrorAction SilentlyContinue
        }
        Remove-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ClearWin' -Recurse -Force -ErrorAction SilentlyContinue

        Update-Progress 92 "Step 5/5: Removing application files..." ("Removing folder: " + $root) 1
        if (Test-Path -LiteralPath $root -PathType Container) {
            Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
        }

        Update-Progress 100 "Uninstallation Complete!" "ClearWin AI v2.7.7 and its shortcuts have been completely removed." 0
        if ($null -ne $SyncState) {
            $SyncState.Completed = $true
            $SyncState.Success = $true
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
    & $UninstallCoreScriptBlock -TargetRoot $InstallRoot -PreserveUserData $true -SyncState $null
    exit 0
}

# =========================================================================
# Graphical Windows Uninstaller Wizard Mode (Same Style as Setup Wizard)
# =========================================================================
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Drawing

[xml]$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="إلغاء تثبيت ClearWin AI v2.7.7 — Uninstaller Wizard"
        Width="660" Height="460"
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
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <StackPanel Grid.Column="0" VerticalAlignment="Center">
                    <TextBlock Text="معالج إلغاء تثبيت ClearWin AI v2.7.7" FontSize="20" FontWeight="Bold" Foreground="#F87171"/>
                    <TextBlock Text="إزالة البرنامج بأمان مع استعادة إعدادات الطاقة والسجل الأصلية لنظام Windows" FontSize="12.5" Foreground="#94A3B8" Margin="0,3,0,0"/>
                </StackPanel>
                <Border Grid.Column="1" Background="#991B1B" CornerRadius="6" Padding="10,4" VerticalAlignment="Top">
                    <TextBlock Text="Uninstaller" FontSize="11" FontWeight="SemiBold" Foreground="White" FlowDirection="LeftToRight"/>
                </Border>
            </Grid>
        </Border>

        <!-- Page 1: Confirm Uninstall -->
        <StackPanel x:Name="PageConfirm" Grid.Row="1" Visibility="Visible">
            <Border Background="#1E293B" CornerRadius="10" Padding="16" BorderBrush="#334155" BorderThickness="1" Margin="0,0,0,14">
                <StackPanel>
                    <TextBlock Text="هل أنت متأكد من رغبتك في إلغاء تثبيت ClearWin AI من جهازك؟" FontSize="14.5" FontWeight="SemiBold" Foreground="#F8FAFC" Margin="0,0,0,8"/>
                    <TextBlock Text="مسار التثبيت الذي سيتم حذفه:" FontSize="12.5" Foreground="#94A3B8" Margin="0,0,0,4"/>
                    <TextBox x:Name="TxtTargetRoot" IsReadOnly="True" Height="34" VerticalContentAlignment="Center"
                             Background="#0F172A" Foreground="#38BDF8" BorderBrush="#475569" BorderThickness="1"
                             Padding="10,0" FontSize="12.5" FlowDirection="LeftToRight"/>
                </StackPanel>
            </Border>

            <Border Background="#1E293B" CornerRadius="10" Padding="16" BorderBrush="#334155" BorderThickness="1">
                <StackPanel>
                    <CheckBox x:Name="ChkPreserveData" IsChecked="True" Foreground="#F1F5F9" FontSize="13" Margin="0,0,0,8"
                              Content="الاحتفاظ بنسخة احتياطية من سجلات التعلم الذكي والإعدادات في مجلد آمن (ClearWin-UserData)"/>
                    <TextBlock Text="• سيقوم المعالج تلقائياً بإيقاف أي عمليات نشطة، واستعادة خطة الطاقة وقيم Registry الأصلية، وحذف اختصارات سطح المكتب وقائمة ابدأ."
                               FontSize="12" Foreground="#94A3B8" TextWrapping="Wrap"/>
                </StackPanel>
            </Border>
        </StackPanel>

        <!-- Page 2: Uninstalling Progress -->
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
                <TextBlock x:Name="TxtStepTitle" Grid.Column="0" Text="جاري إلغاء التثبيت..." FontSize="15" FontWeight="Bold" Foreground="#F8FAFC"/>
                <TextBlock x:Name="TxtPercent" Grid.Column="1" Text="0%" FontSize="18" FontWeight="Bold" Foreground="#F87171" FlowDirection="LeftToRight"/>
            </Grid>

            <ProgressBar x:Name="ProgBar" Grid.Row="1" Height="22" Minimum="0" Maximum="100" Value="0"
                         Background="#1E293B" Foreground="#EF4444" BorderBrush="#334155" BorderThickness="1"/>

            <Grid Grid.Row="2" Margin="0,8,0,12">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <TextBlock x:Name="TxtDetail" Grid.Column="0" Text="جاري إزالة الملفات..." FontSize="12.5" Foreground="#94A3B8"/>
                <TextBlock x:Name="TxtRemaining" Grid.Column="1" Text="فاضل: 100% (~8 ثوانٍ)" FontSize="12.5" FontWeight="SemiBold" Foreground="#FBBF24"/>
            </Grid>

            <Border Grid.Row="3" Background="#090D16" CornerRadius="8" BorderBrush="#1E293B" BorderThickness="1" Padding="10">
                <TextBox x:Name="TxtLog" Background="Transparent" Foreground="#FCA5A5" BorderThickness="0"
                         IsReadOnly="True" TextWrapping="Wrap" VerticalScrollBarVisibility="Auto"
                         FontFamily="Consolas" FontSize="11.5" FlowDirection="LeftToRight"/>
            </Border>
        </Grid>

        <!-- Page 3: Done -->
        <StackPanel x:Name="PageDone" Grid.Row="1" Visibility="Collapsed" VerticalAlignment="Center" HorizontalAlignment="Center">
            <Border Width="64" Height="64" CornerRadius="32" Background="#065F46" HorizontalAlignment="Center" Margin="0,0,0,14">
                <TextBlock Text="✓" FontSize="34" FontWeight="Bold" Foreground="#34D399" HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Border>
            <TextBlock Text="تم إلغاء تثبيت ClearWin AI بنجاح!" FontSize="20" FontWeight="Bold" Foreground="#34D399" HorizontalAlignment="Center"/>
            <TextBlock Text="تمت إزالة جميع ملفات البرنامج والاختصارات واستعادة إعدادات النظام الأصلية." FontSize="13" Foreground="#94A3B8" HorizontalAlignment="Center" Margin="0,8,0,0"/>
        </StackPanel>

        <!-- Footer -->
        <Border Grid.Row="2" BorderBrush="#1E293B" BorderThickness="0,1,0,0" Padding="0,14,0,0" Margin="0,14,0,0">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Text="ClearWin AI Uninstaller • Windows 10 / 11" VerticalAlignment="Center" FontSize="11.5" Foreground="#64748B"/>
                <Button x:Name="BtnCancel" Grid.Column="1" Content="تراجع" Width="110" Height="38" Margin="0,0,10,0"
                        Background="#1E293B" Foreground="#E2E8F0" BorderBrush="#334155" FontSize="13.5" Cursor="Hand"/>
                <Button x:Name="BtnPrimary" Grid.Column="2" Content="إلغاء التثبيت الآن" Width="165" Height="38"
                        Background="#DC2626" Foreground="White" BorderBrush="#F87171" FontSize="13.5" FontWeight="Bold" Cursor="Hand"/>
            </Grid>
        </Border>
    </Grid>
</Window>
"@

$reader = New-Object System.Xml.XmlNodeReader $xaml
$window = [Windows.Markup.XamlReader]::Load($reader)

$PageConfirm = $window.FindName('PageConfirm')
$PageProgress = $window.FindName('PageProgress')
$PageDone = $window.FindName('PageDone')
$TxtTargetRoot = $window.FindName('TxtTargetRoot')
$ChkPreserveData = $window.FindName('ChkPreserveData')
$TxtStepTitle = $window.FindName('TxtStepTitle')
$TxtPercent = $window.FindName('TxtPercent')
$ProgBar = $window.FindName('ProgBar')
$TxtDetail = $window.FindName('TxtDetail')
$TxtRemaining = $window.FindName('TxtRemaining')
$TxtLog = $window.FindName('TxtLog')
$BtnCancel = $window.FindName('BtnCancel')
$BtnPrimary = $window.FindName('BtnPrimary')

$TxtTargetRoot.Text = $InstallRoot

$syncState = [hashtable]::Synchronized(@{
    Percent = 0
    StepTitle = "جاري بدء إلغاء التثبيت..."
    Detail = ""
    RemainingSeconds = 8
    Logs = (New-Object System.Collections.Concurrent.ConcurrentQueue[string])
    Completed = $false
    Success = $false
    ErrorMessage = ""
})

$script:stage = "Confirm"
$script:psInst = $null
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
        $TxtRemaining.Text = "فاضل: $remPct% (حوالي $remSec ثوانٍ)"
    } else {
        $TxtRemaining.Text = "اكتمل إلغاء التثبيت 100%"
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
        if ($null -ne $script:psInst) {
            try { $script:psInst.Dispose() } catch {}
            $script:psInst = $null
        }
        if ($syncState.Success) {
            $script:stage = "Done"
            $PageProgress.Visibility = [System.Windows.Visibility]::Collapsed
            $PageDone.Visibility = [System.Windows.Visibility]::Visible
            $BtnCancel.Visibility = [System.Windows.Visibility]::Collapsed
            $BtnPrimary.IsEnabled = $true
            $BtnPrimary.Background = [System.Windows.Media.SolidColorBrush]([System.Windows.Media.Color]::FromRgb(2, 132, 199))
            $BtnPrimary.Content = "إغلاق (Finish)"
        } else {
            $BtnCancel.IsEnabled = $true
            $BtnCancel.Content = "إغلاق"
            [System.Windows.MessageBox]::Show("حدث خطأ أثناء إلغاء التثبيت:`n" + $syncState.ErrorMessage, "ClearWin AI Uninstaller", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Error) | Out-Null
        }
    }
})

$BtnPrimary.Add_Click({
    if ($script:stage -eq "Confirm") {
        $script:stage = "Running"
        $PageConfirm.Visibility = [System.Windows.Visibility]::Collapsed
        $PageProgress.Visibility = [System.Windows.Visibility]::Visible
        $BtnPrimary.IsEnabled = $false
        $BtnCancel.IsEnabled = $false

        $script:psInst = [powershell]::Create()
        [void]$script:psInst.AddScript($UninstallCoreScriptBlock)
        [void]$script:psInst.AddArgument($TxtTargetRoot.Text)
        [void]$script:psInst.AddArgument([bool]$ChkPreserveData.IsChecked)
        [void]$script:psInst.AddArgument($syncState)
        [void]$script:psInst.BeginInvoke()
        $script:timer.Start()
    }
    elseif ($script:stage -eq "Done") {
        $window.Close()
    }
})

$BtnCancel.Add_Click({
    if ($script:stage -ne "Running") {
        $window.Close()
    }
})

[void]$window.ShowDialog()
