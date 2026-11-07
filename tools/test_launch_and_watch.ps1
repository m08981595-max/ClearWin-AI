$exe = "D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control\bin\ClearWin.exe"
$bin = "D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control\bin"

Write-Host "Starting ClearWin..."
$proc = Start-Process -FilePath $exe -WorkingDirectory $bin -PassThru
Write-Host "Launched PID: $($proc.Id)"

for ($i = 1; $i -le 10; $i++) {
    Start-Sleep -Seconds 1
    $p = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
    if (-not $p) {
        $p = Get-Process -Name ClearWin -ErrorAction SilentlyContinue | Select-Object -First 1
    }
    if ($p) {
        Write-Host "[$i] PID: $($p.Id), MainWindowHandle: $($p.MainWindowHandle), Title: '$($p.MainWindowTitle)', Responding: $($p.Responding), WS: $([math]::Round($p.WorkingSet64/1MB,1)) MB"
        if ($p.MainWindowHandle -ne [IntPtr]::Zero) {
            Write-Host "SUCCESS! MainWindowHandle is active and rendering: $($p.MainWindowHandle)"
            break
        }
    } else {
        Write-Warning "[$i] Process exited!"
        break
    }
}
