@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $launcher='%~dp0ClassicSkinMorph-Launcher.ps1'; if(-not (Test-Path -LiteralPath $launcher)){ [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; (New-Object Net.WebClient).DownloadFile('https://raw.githubusercontent.com/Niftix/ClassicSkinMorph/master/ClassicSkinMorph-Launcher.ps1',$launcher) }; & $launcher"
if errorlevel 1 pause
