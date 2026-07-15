@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Microsoft C# compiler not found.
  exit /b 1
)
"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"assets\classic-skin-morph.ico" /out:ClassicSkinMorph.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "src\Program.cs"
if errorlevel 1 exit /b 1
echo ClassicSkinMorph.exe built successfully.
