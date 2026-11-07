Add-Type -Path "bin\Microsoft.Web.WebView2.Core.dll"
Add-Type -Path "bin\Microsoft.Web.WebView2.Wpf.dll"
Add-Type -AssemblyName PresentationFramework

$userDataDir = [System.IO.Path]::Combine($env:LOCALAPPDATA, "ClearWin", "WebView2")

$app = [System.Windows.Application]::Current
if (-not $app) { $app = New-Object System.Windows.Application }

$win = New-Object System.Windows.Window
$win.Title = "WebView2 Test"
$win.Width = 800
$win.Height = 600

$wv = New-Object Microsoft.Web.WebView2.Wpf.WebView2
$win.Content = $wv

$win.Add_Loaded({
    try {
        Write-Host "Window loaded, creating environment..."
        $envTask = [Microsoft.Web.WebView2.Core.CoreWebView2Environment]::CreateAsync($null, $userDataDir, $null)
        $env = $envTask.GetAwaiter().GetResult()
        Write-Host "Calling EnsureCoreWebView2Async..."
        $initTask = $wv.EnsureCoreWebView2Async($env)
        $initTask.GetAwaiter().GetResult()
        Write-Host "EnsureCoreWebView2Async succeeded! CoreWebView2 is: $($wv.CoreWebView2 -ne $null)"
        
        $indexPath = [System.IO.Path]::GetFullPath("bin\Resources\WebUI\index.html")
        Write-Host "Navigating to: $indexPath"
        $wv.Source = New-Object System.Uri($indexPath)
        Write-Host "Source set successfully!"
    } catch {
        Write-Host "ERROR inside Loaded: $($_.Exception.ToString())"
    }
})

$win.Show()
Start-Sleep -Seconds 2
Write-Host "Closing test window..."
$win.Close()
