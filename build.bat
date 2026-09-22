@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

echo [AltPowerPlan] Launching local build engine...
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
set EXIT_CODE=%ERRORLEVEL%

if %EXIT_CODE% neq 0 (
    echo.
    echo [ERROR] Build failed with exit code %EXIT_CODE%.
)

rem If double-clicked in Windows File Explorer without arguments, pause before closing
if "%~1"=="" (
    echo %CMDCMDLINE% | findstr /i /c:"%COMSPEC% /c" >nul 2>&1
    if %ERRORLEVEL% equ 0 (
        echo.
        echo Press any key to close this window...
        pause >nul
    )
)

exit /b %EXIT_CODE%
