#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$runtimeGuid = '{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}'
$runtimeUrl = 'https://go.microsoft.com/fwlink/p/?LinkId=2124703'

function Get-WebView2RuntimeVersion {
    $keys = @(
        "HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\$runtimeGuid",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\$runtimeGuid",
        "HKCU:\SOFTWARE\Microsoft\EdgeUpdate\Clients\$runtimeGuid"
    )
    foreach ($key in $keys) {
        try {
            if (Test-Path -LiteralPath $key) {
                $item = Get-ItemProperty -LiteralPath $key -Name 'pv' -ErrorAction Stop
                if (-not [string]::IsNullOrWhiteSpace([string]$item.pv)) { return [string]$item.pv }
            }
        } catch { }
    }
    return $null
}

$installed = Get-WebView2RuntimeVersion
if ($installed) {
    Write-Host "Microsoft Edge WebView2 Runtime is installed: $installed" -ForegroundColor Green
    exit 0
}

$installer = Join-Path $env:TEMP 'MicrosoftEdgeWebView2Setup-ClearWin.exe'
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    Write-Host 'Downloading the official Microsoft Edge WebView2 Runtime installer...'
    Invoke-WebRequest -Uri $runtimeUrl -OutFile $installer -UseBasicParsing -TimeoutSec 90
    if (-not (Test-Path -LiteralPath $installer -PathType Leaf) -or (Get-Item -LiteralPath $installer).Length -lt 100000) {
        throw 'The downloaded WebView2 installer is missing or unexpectedly small.'
    }
    $process = Start-Process -FilePath $installer -ArgumentList @('/silent', '/install') -Wait -PassThru
    if ($process.ExitCode -notin @(0, 3010)) {
        throw "WebView2 Runtime installer exited with code $($process.ExitCode)."
    }
    Start-Sleep -Seconds 2
    $installed = Get-WebView2RuntimeVersion
    if (-not $installed) { throw 'The installer finished but the WebView2 Runtime registry version was not found.' }
    Write-Host "Microsoft Edge WebView2 Runtime is ready: $installed" -ForegroundColor Green
    exit 0
}
catch {
    Write-Error ("Could not install Microsoft Edge WebView2 Runtime automatically. Install it from https://developer.microsoft.com/microsoft-edge/webview2/ and rerun ClearWin. Details: " + $_.Exception.Message)
    exit 1
}
finally {
    Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue
}
