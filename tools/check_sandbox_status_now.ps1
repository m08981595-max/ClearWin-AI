# Check Windows Sandbox and System Readiness
$sandboxExe = "C:\Windows\System32\WindowsSandbox.exe"
$sandboxExists = Test-Path $sandboxExe

$os = Get-CimInstance Win32_OperatingSystem
$freeMB = [math]::Round($os.FreePhysicalMemory / 1KB, 2)
$totalMB = [math]::Round($os.TotalVisibleMemorySize / 1KB, 2)

Write-Host "WindowsSandbox.exe Exists: $sandboxExists"
Write-Host "Total RAM: $totalMB MB"
Write-Host "Free RAM : $freeMB MB"
Write-Host "RAM Threshold (>= 3072 MB): $($freeMB -ge 3072)"

# Check CBS Package state for DisposableClientVM
$cbsKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\Packages"
$sandboxPkgs = Get-ChildItem -Path $cbsKey -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -like "*Containers-DisposableClientVM*" }

if ($sandboxPkgs) {
    Write-Host "DisposableClientVM CBS Packages found ($($sandboxPkgs.Count)):"
    foreach ($pkg in $sandboxPkgs) {
        $props = Get-ItemProperty -Path $pkg.PSPath -ErrorAction SilentlyContinue
        Write-Host "  - $($pkg.PSChildName): $($props.CurrentState)"
    }
} else {
    Write-Host "No DisposableClientVM packages found in registry."
}
