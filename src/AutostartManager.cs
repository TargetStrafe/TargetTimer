using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TargetTimer
{
    public static class AutostartManager
    {
        private const string RegistryRunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "TargetTimer";

        public static bool IsAutostartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryRunPath, false))
                {
                    if (key == null) return false;
                    object val = key.GetValue(AppName);
                    if (val == null) return false;
                    string currentPath = Application.ExecutablePath;
                    return val.ToString().IndexOf(currentPath, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool SetAutostart(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryRunPath, true))
                {
                    if (key == null) return false;

                    if (enable)
                    {
                        string exePath = "\"" + Application.ExecutablePath + "\" --silent";
                        key.SetValue(AppName, exePath);
                    }
                    else
                    {
                        if (key.GetValue(AppName) != null)
                        {
                            key.DeleteValue(AppName, false);
                        }
                    }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
