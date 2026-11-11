@echo off
rem Baldi VR mod installer for Windows: double-click this file.
rem Windows opens a ZIP without unzipping it, and the installer needs its other
rem files, so it checks they are next to it first.
if not exist "%~dp0patches\install-windows.ps1" (
    echo.
    echo The mod is still inside the ZIP file.
    echo Right-click the ZIP, choose "Extract All...", then "Extract",
    echo and double-click this installer again from the new folder.
    echo.
    pause
    exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0patches\install-windows.ps1"
