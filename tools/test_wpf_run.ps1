Add-Type -AssemblyName PresentationFramework
try {
    $app = New-Object System.Windows.Application
    $w = New-Object System.Windows.Window
    $w.Show()
    Write-Host "Window shown"
    # Testing whether app.Run($w) throws when $w is already shown
    $app.Run($w)
} catch {
    Write-Host "ERROR: $($_.Exception.ToString())"
}
