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
        private static readonly string SettingsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TargetTimer",
            "settings.json"
        );

        public static event Action<bool> OnAutostartChanged;
        public static bool WasFirstRun { get; private set; }

        public static void InitializeOnFirstRun()
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                if (!File.Exists(SettingsFile))
                {
                    WasFirstRun = true;
                    SetAutostart(true);
                }
                else
                {
                    string content = File.ReadAllText(SettingsFile);
                    bool userWantsAutostart = content.IndexOf("\"autostart\": true", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                              content.IndexOf("\"autostart\":true", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (userWantsAutostart)
                    {
                        if (!IsAutostartEnabled())
                        {
                            SetAutostart(true);
                        }
                    }
                }
            }
            catch { }
        }

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
                }

                SaveAutostartSetting(enable);

                if (OnAutostartChanged != null)
                {
                    OnAutostartChanged(enable);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void SaveAutostartSetting(bool enabled)
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string json = "{\r\n" +
                              "  \"first_run\": false,\r\n" +
                              "  \"autostart\": " + (enabled ? "true" : "false") + "\r\n" +
                              "}";
                File.WriteAllText(SettingsFile, json);
            }
            catch { }
        }
    }
}
