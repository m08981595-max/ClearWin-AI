# Check official Microsoft Evergreen Standalone Installer (x64)
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$fwlinkUrl = "https://go.microsoft.com/fwlink/?linkid=2124701"
$targetDir = Join-Path $PSScriptRoot "webview2-offline"
if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir -Force | Out-Null }
$targetFile = Join-Path $targetDir "MicrosoftEdgeWebView2RuntimeInstallerX64.exe"

Write-Host "Probing official Microsoft fwlink: $fwlinkUrl"
try {
    $req = [System.Net.HttpWebRequest]::Create($fwlinkUrl)
    $req.AllowAutoRedirect = $true
    $req.Method = "HEAD"
    $req.Timeout = 15000
    $res = $req.GetResponse()
    $resolvedUrl = $res.ResponseUri.AbsoluteUri
    $contentLength = $res.ContentLength
    $res.Close()

    Write-Host "Resolved URL: $resolvedUrl"
    Write-Host "Remote Content Length: $contentLength bytes ($([math]::Round($contentLength / 1MB, 2)) MB)"

    # Verify domain is officially Microsoft
    $uriObj = [System.Uri]$resolvedUrl
    if ($uriObj.Host -notlike "*microsoft.com" -and $uriObj.Host -notlike "*azureedge.net") {
        Write-Error "Resolved host is not an official Microsoft domain: $($uriObj.Host)"
        exit 2
    }

    # Download if not already present or incomplete
    if (-not (Test-Path $targetFile) -or (Get-Item $targetFile).Length -ne $contentLength) {
        Write-Host "Downloading MicrosoftEdgeWebView2RuntimeInstallerX64.exe from official CDN..."
        $webClient = New-Object System.Net.WebClient
        $webClient.DownloadFile($resolvedUrl, $targetFile)
        Write-Host "Download completed successfully."
    } else {
        Write-Host "Existing file matches content length ($contentLength bytes)."
    }

    # Verify file existence and size
    if (-not (Test-Path $targetFile)) {
        Write-Error "File not found after download."
        exit 3
    }
    $fileItem = Get-Item $targetFile
    Write-Host "Local File: $($fileItem.FullName)"
    Write-Host "Local File Size: $($fileItem.Length) bytes ($([math]::Round($fileItem.Length / 1MB, 2)) MB)"

    # Authenticode Signature Verification
    Write-Host "Verifying Authenticode signature..."
    $sig = Get-AuthenticodeSignature -FilePath $targetFile
    Write-Host "Signature Status: $($sig.Status)"
    Write-Host "Signer Subject: $($sig.SignerCertificate.Subject)"
    Write-Host "Issuer: $($sig.SignerCertificate.Issuer)"

    if ($sig.Status -ne "Valid") {
        Write-Error "Digital signature is not valid! Status: $($sig.Status)"
        exit 4
    }

    if ($sig.SignerCertificate.Subject -notlike "*Microsoft Corporation*") {
        Write-Error "Certificate is not issued to Microsoft Corporation!"
        exit 5
    }

    # SHA-256 Calculation
    $sha256 = (Get-FileHash -Path $targetFile -Algorithm SHA256).Hash
    Write-Host "SHA-256: $sha256"

    # Save metadata
    $meta = [PSCustomObject]@{
        ResolvedUrl   = $resolvedUrl
        ContentLength = $contentLength
        FileSize      = $fileItem.Length
        Sha256        = $sha256
        Signer        = $sig.SignerCertificate.Subject
        SigStatus     = "$($sig.Status)"
        Timestamp     = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    }
    $meta | ConvertTo-Json -Depth 4 | Out-File -FilePath (Join-Path $targetDir "metadata.json") -Encoding utf8
    Write-Host "Metadata saved."
    exit 0
} catch {
    Write-Error "Error obtaining WebView2 Runtime: $($_.Exception.Message)"
    exit 1
}
