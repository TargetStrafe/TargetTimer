@echo off
cd /d "%~dp0"
echo [TargetTimer] Starting in background...
start "" "TargetTimer.exe"
echo [OK] TargetTimer is running in the background. Check your system tray (near the clock).
