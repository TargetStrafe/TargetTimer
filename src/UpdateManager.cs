using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace TargetTimer
{
    public static class UpdateManager
    {
        public const string CurrentVersion = "1.0.0";
        private const string VersionUrl = "https://raw.githubusercontent.com/TargetStrafe/TargetTimer/main/version.json";
        private static System.Threading.Timer _timer;

        public static void InitAutoUpdate()
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            _timer = new System.Threading.Timer(state => CheckForUpdates(false), null, TimeSpan.FromSeconds(15), TimeSpan.FromHours(6));
        }

        public static void CheckManual(NotifyIcon trayIcon)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                bool found = CheckForUpdates(true, trayIcon);
                if (!found && trayIcon != null)
                {
                    trayIcon.ShowBalloonTip(3000, "TargetTimer", "У вас установлена последняя версия (v" + CurrentVersion + ")", ToolTipIcon.Info);
                }
            });
        }

        public static bool CheckForUpdates(bool isManual, NotifyIcon trayIcon = null)
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(VersionUrl);
                req.UserAgent = "TargetTimer-Client/" + CurrentVersion;
                req.Timeout = 8000;
                req.KeepAlive = false;

                string json = null;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                {
                    json = reader.ReadToEnd();
                }

                if (string.IsNullOrEmpty(json)) return false;

                string remoteVersion = ExtractJsonValue(json, "version");
                string downloadUrl = ExtractJsonValue(json, "download_url");

                if (string.IsNullOrEmpty(remoteVersion) || string.IsNullOrEmpty(downloadUrl))
                    return false;

                Version current = new Version(CurrentVersion);
                Version remote = new Version(remoteVersion.TrimStart('v', 'V'));

                if (remote > current)
                {
                    if (trayIcon != null)
                    {
                        trayIcon.ShowBalloonTip(4000, "TargetTimer", "Найдено обновление v" + remoteVersion + "! Загрузка...", ToolTipIcon.Info);
                    }

                    return DownloadAndApply(downloadUrl);
                }
            }
            catch
            {
                if (isManual && trayIcon != null)
                {
                    trayIcon.ShowBalloonTip(3000, "TargetTimer", "Не удалось проверить обновления. Проверьте интернет-соединение.", ToolTipIcon.Warning);
                }
            }
            return false;
        }

        private static bool DownloadAndApply(string downloadUrl)
        {
            string currentExe = Application.ExecutablePath;
            string newExe = currentExe + ".new";

            try
            {
                if (File.Exists(newExe)) File.Delete(newExe);

                using (WebClient wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "TargetTimer-Client/" + CurrentVersion;
                    wc.DownloadFile(downloadUrl, newExe);
                }

                FileInfo fi = new FileInfo(newExe);
                if (fi.Length < 10240)
                {
                    File.Delete(newExe);
                    return false;
                }

                string scriptPath = Path.Combine(Path.GetTempPath(), "targettimer_update.bat");
                string script = string.Format(
                    "@echo off\r\n" +
                    "ping 127.0.0.1 -n 3 >nul\r\n" +
                    "move /y \"{0}\" \"{1}\" >nul\r\n" +
                    "start \"\" \"{1}\"\r\n" +
                    "del \"%~f0\"\r\n",
                    newExe, currentExe);

                File.WriteAllText(scriptPath, script);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c \"" + scriptPath + "\"",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = true
                };

                Process.Start(psi);
                Environment.Exit(0);
                return true;
            }
            catch
            {
                try { if (File.Exists(newExe)) File.Delete(newExe); } catch { }
                return false;
            }
        }

        private static string ExtractJsonValue(string json, string key)
        {
            string pattern = "\"" + key + "\":";
            int idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            int valStart = json.IndexOf('"', idx + pattern.Length);
            if (valStart < 0) return null;

            int valEnd = json.IndexOf('"', valStart + 1);
            if (valEnd < 0) return null;

            return json.Substring(valStart + 1, valEnd - valStart - 1);
        }
    }
}
