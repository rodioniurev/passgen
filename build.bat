@echo off
rem Builds PassGen.exe with the compiler that ships with Windows (.NET Framework 4) - nothing to install.
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo csc.exe from .NET Framework 4 not found
  exit /b 1
)

rem close a running copy first
if exist "%~dp0PassGen.exe" "%~dp0PassGen.exe" /exit

"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 ^
  /out:"%~dp0PassGen.new.exe" ^
  /win32icon:"%~dp0src\passgen.ico" ^
  /win32manifest:"%~dp0src\app.manifest" ^
  /resource:"%~dp0src\passgen.ico",passgen.ico ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  "%~dp0src\*.cs"
if errorlevel 1 (
  del "%~dp0PassGen.new.exe" >nul 2>nul
  echo BUILD FAILED
  exit /b 1
)

rem A running (or antivirus-locked) exe cannot be overwritten, but it can be renamed.
del "%~dp0PassGen.old.exe" >nul 2>nul
if exist "%~dp0PassGen.exe" ren "%~dp0PassGen.exe" PassGen.old.exe >nul 2>nul
if exist "%~dp0PassGen.exe" (
  del "%~dp0PassGen.new.exe" >nul 2>nul
  echo Could not replace PassGen.exe - exit it from the tray menu and run build.bat again.
  exit /b 1
)
move /y "%~dp0PassGen.new.exe" "%~dp0PassGen.exe" >nul
del "%~dp0PassGen.old.exe" >nul 2>nul
echo Done: %~dp0PassGen.exe
