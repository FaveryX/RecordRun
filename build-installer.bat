@echo off
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 goto nodotnet

set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" goto noinno

echo [1/2] Building RecordRun.exe ...
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o "%~dp0exe"
if errorlevel 1 goto fail

echo [2/2] Creating installer ...
"%ISCC%" "%~dp0installer.iss"
if errorlevel 1 goto fail

echo.
echo DONE. Your installer: %~dp0installer\RecordRun-Setup.exe
pause
exit /b 0

:nodotnet
echo .NET 8 SDK not found. Installing...
winget install -e --id Microsoft.DotNet.SDK.8 --accept-package-agreements --accept-source-agreements
echo.
echo Finished. Close this window and double-click build-installer.bat again.
pause
exit /b 1

:noinno
echo Inno Setup not found. Installing...
winget install -e --id JRSoftware.InnoSetup --accept-package-agreements --accept-source-agreements
echo.
echo Finished. Close this window and double-click build-installer.bat again.
pause
exit /b 1

:fail
echo.
echo FAILED. Copy the error text above and send it to Claude.
pause
exit /b 1
