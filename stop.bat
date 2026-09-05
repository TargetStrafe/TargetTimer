@echo off
echo [TargetTimer] Stopping process...
taskkill /IM TargetTimer.exe /F >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo [OK] TargetTimer has been stopped.
) else (
    echo [INFO] TargetTimer was not running.
)
