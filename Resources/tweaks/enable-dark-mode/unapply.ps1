$Path = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize"
Set-ItemProperty -Path $Path -Name AppsUseLightTheme -Value 1
Set-ItemProperty -Path $Path -Name SystemUsesLightTheme -Value 1
Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
Start-Process explorer.exe
