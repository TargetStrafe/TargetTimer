@echo off
cd /d "%~dp0"
echo [TargetTimer] Enabling autostart on Windows startup...
TargetTimer.exe --autostart-on
echo [OK] TargetTimer will now automatically start on system boot.
pause
