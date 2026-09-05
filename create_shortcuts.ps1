$desktop = [Environment]::GetFolderPath('Desktop')
$dir = $PSScriptRoot

$wsh = New-Object -ComObject WScript.Shell

$shortcut1 = $wsh.CreateShortcut("$desktop\TargetTimer.lnk")
$shortcut1.TargetPath = "$dir\TargetTimer.exe"
$shortcut1.WorkingDirectory = $dir
$shortcut1.Description = "TargetTimer - Фоновый трекер времени"
$shortcut1.Save()

$shortcut2 = $wsh.CreateShortcut("$desktop\TargetTimer - Статистика.lnk")
$shortcut2.TargetPath = "$dir\TargetTimer.exe"
$shortcut2.Arguments = "--report"
$shortcut2.WorkingDirectory = $dir
$shortcut2.Description = "TargetTimer - Открыть статистику"
$shortcut2.Save()

Write-Host "[OK] Desktop shortcuts created successfully."
