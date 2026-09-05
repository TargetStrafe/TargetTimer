@echo off
cd /d "%~dp0"
echo [TargetTimer] Generating and opening statistics dashboard...
TargetTimer.exe --report
