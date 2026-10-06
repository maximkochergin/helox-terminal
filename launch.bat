@echo off
setlocal
if not exist "%~dp0bin\helox.exe" (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
    if errorlevel 1 (
        echo build failed / press any key to close
        pause >nul
        exit /b 1
    )
)
"%~dp0bin\helox.exe" %*
set "helox_exit=%errorlevel%"
if not "%helox_exit%"=="0" if "%~1"=="" pause
exit /b %helox_exit%
