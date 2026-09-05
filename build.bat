@echo off
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set WPF=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF

echo [TargetTimer] Compiling TargetTimer.exe with icon...
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /win32icon:icon.ico /out:TargetTimer.exe /r:System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll /r:"%WPF%\UIAutomationClient.dll","%WPF%\UIAutomationTypes.dll" src\*.cs

if %ERRORLEVEL% equ 0 (
    echo [OK] Successfully compiled TargetTimer.exe!
) else (
    echo [ERROR] Build failed! Code: %ERRORLEVEL%
)
