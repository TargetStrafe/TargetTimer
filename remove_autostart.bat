@echo off
cd /d "%~dp0"
echo [TargetTimer] Disabling autostart on Windows startup...
TargetTimer.exe --autostart-off
echo [OK] Autostart has been disabled.
pause
