# ClearWin one-shot deploy: backup old exe, copy new build, write result marker.
# Elevated run only (needed to write into C:\Program Files\ClearWin).
$ErrorActionPreference = 'Stop'
$src   = 'D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\ClearWin_AI_v2.7.7_Repaired\clearwin_v19_scrollbar_control\_ClearWin_Engine\bin\ClearWin.exe'
$dst   = 'C:\Program Files\ClearWin\ClearWin.exe'
$bkDir = 'C:\Program Files\ClearWin\backup-pre-fix-20261118'
$log   = 'D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\ClearWin_AI_v2.7.7_Repaired\clearwin_v19_scrollbar_control\_ClearWin_Engine\tools\deploy_fix_20261118_result.txt'
try {
    New-Item -ItemType Directory -Force -Path $bkDir | Out-Null
    Copy-Item -LiteralPath $dst -Destination (Join-Path $bkDir 'ClearWin.exe') -Force
    $bk = Get-Item (Join-Path $bkDir 'ClearWin.exe')
    Copy-Item -LiteralPath $src -Destination $dst -Force
    $n = Get-Item $dst
    $nLastWrite = $n.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss')
    $bkLastWrite = $bk.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss')
    "OK|new=$($n.Length)@$nLastWrite|backup=$($bk.Length)@$bkLastWrite" | Out-File -LiteralPath $log -Encoding utf8
} catch {
    "FAIL|$($_.Exception.Message)" | Out-File -LiteralPath $log -Encoding utf8
}
