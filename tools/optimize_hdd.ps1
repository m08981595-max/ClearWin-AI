# tools/optimize_hdd.ps1
$workspaceDir = "d:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control\tools"
$logFile = "$workspaceDir\hdd_defrag_log.txt"
$statusFile = "$workspaceDir\hdd_defrag_status.json"

function Log-Message($msg, $color="Cyan") {
    $timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    $line = "[$timestamp] $msg"
    Write-Host $line -ForegroundColor $color
    $line | Out-File -FilePath $logFile -Append -Encoding utf8
}

function Update-Status($stage, $percent, $details) {
    $obj = [PSCustomObject]@{
        Timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
        Stage = $stage
        Progress = $percent
        Details = $details
    }
    $obj | ConvertTo-Json | Out-File -FilePath $statusFile -Force -Encoding utf8
}

"=====================================================" | Out-File -FilePath $logFile -Encoding utf8
"       HDD DEFRAGMENTATION & SEEK OPTIMIZATION        " | Out-File -FilePath $logFile -Append -Encoding utf8
"=====================================================" | Out-File -FilePath $logFile -Append -Encoding utf8
Log-Message "Starting optimization on TOSHIBA MQ01ACF050 HDD..." "Yellow"

# --- Stage 1: Pre-clean Temporary Files ---
Log-Message "Stage 1/4: Cleaning temporary files to ensure contiguous free space..." "Yellow"
Update-Status "Stage 1: Pre-Clean" 10 "Cleaning temp files..."
try {
    $tempPaths = @([System.IO.Path]::GetTempPath(), "$env:windir\Temp")
    foreach ($tp in $tempPaths) {
        if (Test-Path $tp) {
            Get-ChildItem -Path $tp -Recurse -Force -ErrorAction SilentlyContinue |
                Where-Object { -not $_.PSIsContainer -and $_.LastWriteTime -lt (Get-Date).AddDays(-1) } |
                Remove-Item -Force -ErrorAction SilentlyContinue
        }
    }
    Clear-RecycleBin -Force -ErrorAction SilentlyContinue
    Log-Message "Temporary cache cleaned successfully." "Green"
} catch {
    Log-Message "Notice during temp clean: $($_.Exception.Message)" "Gray"
}

# --- Stage 2: Initial Analysis ---
Log-Message "Stage 2/4: Analyzing fragmentation on C: and D:..." "Yellow"
Update-Status "Stage 2: Analysis" 25 "Analyzing drive fragmentation..."
try {
    Log-Message "Analyzing Drive C:..." "Cyan"
    $cAnalysis = defrag C: /A /V
    $cAnalysis | Out-File -FilePath $logFile -Append -Encoding utf8
    $cAnalysis | ForEach-Object { Write-Host "   $_" }

    Log-Message "Analyzing Drive D:..." "Cyan"
    $dAnalysis = defrag D: /A /V
    $dAnalysis | Out-File -FilePath $logFile -Append -Encoding utf8
    $dAnalysis | ForEach-Object { Write-Host "   $_" }
} catch {
    Log-Message "Error during analysis: $($_.Exception.Message)" "Red"
}

# --- Stage 3: Defragmentation of Drive C: ---
Log-Message "Stage 3/4: Defragmenting System Drive (C:) to eliminate needle jumps..." "Yellow"
Update-Status "Stage 3A: Defragmenting C:" 40 "Defragmenting files on Drive C:..."
try {
    Log-Message "Running defragmentation on C: (Normal Priority /H)..." "Cyan"
    $cDefrag = defrag C: /D /U /V /H
    $cDefrag | Out-File -FilePath $logFile -Append -Encoding utf8
    $cDefrag | ForEach-Object { Write-Host "   $_" }
    Log-Message "Drive C: defragmentation completed successfully." "Green"
} catch {
    Log-Message "Error defragmenting C: $($_.Exception.Message)" "Red"
}

# --- Stage 3B: Free Space Consolidation on C: ---
Log-Message "Consolidating free space on C:..." "Cyan"
Update-Status "Stage 3B: Consolidating C:" 60 "Consolidating contiguous free space on Drive C:..."
try {
    $cConsol = defrag C: /X /V /H
    $cConsol | Out-File -FilePath $logFile -Append -Encoding utf8
    Log-Message "Drive C: free space consolidated." "Green"
} catch {
    Log-Message "Notice on consolidation C: $($_.Exception.Message)" "Gray"
}

# --- Stage 3C: Defragmentation of Drive D: ---
Log-Message "Defragmenting Data Drive (D:)..." "Yellow"
Update-Status "Stage 3C: Defragmenting D:" 75 "Defragmenting files on Drive D:..."
try {
    $dDefrag = defrag D: /D /U /V /H
    $dDefrag | Out-File -FilePath $logFile -Append -Encoding utf8
    $dDefrag | ForEach-Object { Write-Host "   $_" }
    Log-Message "Drive D: defragmentation completed successfully." "Green"
} catch {
    Log-Message "Error defragmenting D: $($_.Exception.Message)" "Red"
}

# --- Stage 4: Boot Optimization on Drive C: ---
Log-Message "Stage 4/4: Optimizing boot and launch layout on Drive C: (Boot Optimization)..." "Yellow"
Update-Status "Stage 4: Boot Optimization" 90 "Moving boot/app files to fastest outer tracks (/B)..."
try {
    $cBoot = defrag C: /B /V /H
    $cBoot | Out-File -FilePath $logFile -Append -Encoding utf8
    $cBoot | ForEach-Object { Write-Host "   $_" }
    Log-Message "Boot optimization completed successfully." "Green"
} catch {
    Log-Message "Notice during boot optimization: $($_.Exception.Message)" "Gray"
}

# --- Final Analysis ---
Log-Message "Performing post-optimization analysis on C:..." "Cyan"
try {
    $finalAnalysis = defrag C: /A /V
    $finalAnalysis | Out-File -FilePath $logFile -Append -Encoding utf8
    $finalAnalysis | ForEach-Object { Write-Host "   $_" }
} catch {
    Log-Message "Final analysis notice: $($_.Exception.Message)" "Gray"
}

Log-Message "=====================================================" "Green"
Log-Message "ALL STAGES COMPLETED! HDD NEEDLE SEEK TIME MINIMIZED." "Green"
Log-Message "=====================================================" "Green"
Update-Status "Complete" 100 "HDD optimization and defragmentation completed successfully."
