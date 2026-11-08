@echo off
setlocal

set "SOURCE=%~dp0AmongUsRevamped.dll"
set "DEST=C:\Program Files (x86)\Steam\steamapps\common\Among Us Revamped Copilot\BepInEx\plugins"

if not exist "%SOURCE%" (
    echo ERROR: AmongUsRevamped.dll must be in the same folder as this installer.
    pause
    exit /b 1
)

net session >nul 2>&1
if not "%errorlevel%"=="0" (
    echo Requesting administrator permission to install into Program Files...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

if not exist "%DEST%" mkdir "%DEST%"
if errorlevel 1 (
    echo ERROR: Could not create the Among Us plugins folder.
    pause
    exit /b 1
)

copy /Y "%SOURCE%" "%DEST%\AmongUsRevamped.dll" >nul
if errorlevel 1 (
    echo ERROR: Copy failed. Close Among Us and try again.
    pause
    exit /b 1
)

echo Among Us Revamped Blobek Edition installed successfully to:
echo %DEST%\AmongUsRevamped.dll
pause
