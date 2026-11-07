Write-Host "Reinstalling Microsoft OneDrive..."
Start-Process -FilePath winget -ArgumentList "install -e --id Microsoft.OneDrive --accept-source-agreements --accept-package-agreements --silent" -NoNewWindow -Wait
