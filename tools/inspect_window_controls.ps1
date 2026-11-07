$files = @(
    "Resources\WebUI\index.html",
    "src\MainWindow.WebUi.cs",
    "src\MainWindow.Glassmorphism.cs",
    "src\WebUiHost.cs"
)

foreach ($f in $files) {
    if (Test-Path $f) {
        Write-Host "=== $f ===" -ForegroundColor Cyan
        Get-Content $f | Select-String -Pattern '(window:close|window:min|window:max|_closeBtn|_minBtn|app-close|app-minimize|app-maximize)' -Context 2, 2 | ForEach-Object {
            "Line $($_.LineNumber):"
            $_.Context.PreContext | ForEach-Object { "  - $_" }
            "  > $($_.Line)"
            $_.Context.PostContext | ForEach-Object { "  + $_" }
            ""
        }
    }
}
