# ClearWin Classification: Irreversible
Write-Warning "Debloat is a permanent package removal operation (Irreversible)."
Write-Host "Automatic rollback is not possible because Remove-AppxPackage deletes package files from user profiles."
Write-Host "To restore any removed application, please re-install it from Microsoft Store or run 'wsreset.exe'."
Write-Host "Protected Windows utilities (Notepad, Calculator, Paint, Terminal) were preserved and not touched."
