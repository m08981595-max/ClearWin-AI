Add-Type -Path "bin\Microsoft.Web.WebView2.Core.dll"
Add-Type -Path "bin\Microsoft.Web.WebView2.Wpf.dll"
Add-Type -AssemblyName PresentationFramework

$userDataDir = [System.IO.Path]::Combine($env:LOCALAPPDATA, "ClearWin", "WebView2")

$app = [System.Windows.Application]::Current
if (-not $app) { $app = New-Object System.Windows.Application }

$win = New-Object System.Windows.Window
$win.Title = "WebView2 Nav Test"
$win.Width = 800
$win.Height = 600

$wv = New-Object Microsoft.Web.WebView2.Wpf.WebView2
$win.Content = $wv

$win.Add_Loaded({
    try {
        $envTask = [Microsoft.Web.WebView2.Core.CoreWebView2Environment]::CreateAsync($null, $userDataDir, $null)
        $env = $envTask.GetAwaiter().GetResult()
        $initTask = $wv.EnsureCoreWebView2Async($env)
        $initTask.GetAwaiter().GetResult()
        
        $wv.Add_NavigationCompleted({
            param($s, $e)
            Write-Host "NavigationCompleted! IsSuccess: $($e.IsSuccess), WebErrorStatus: $($e.WebErrorStatus)"
        })

        $indexPath = [System.IO.Path]::GetFullPath("bin\Resources\WebUI\index.html")
        Write-Host "Navigating to: $indexPath"
        $wv.Source = New-Object System.Uri($indexPath)
    } catch {
        Write-Host "ERROR inside Loaded: $($_.Exception.ToString())"
    }
})

$win.Show()
Start-Sleep -Seconds 4
Write-Host "Closing test window..."
$win.Close()
