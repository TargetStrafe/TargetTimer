using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TargetTimer
{
    public class AppActivity
    {
        public string Name { get; set; }
        public int Seconds { get; set; }
        public Dictionary<string, int> Sites { get; set; }

        public AppActivity()
        {
            Sites = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public class DayActivity
    {
        public string Date { get; set; }
        public int ActiveSeconds { get; set; }
        public int AfkSeconds { get; set; }
        public Dictionary<string, AppActivity> Apps { get; set; }

        public DayActivity()
        {
            Apps = new Dictionary<string, AppActivity>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public class StorageManager
    {
        private readonly string _dataDir;
        private readonly object _lock = new object();
        private DayActivity _currentDay;
        private DayActivity _totals;
        private string _currentDateStr;
        private bool _isDirty;

        public StorageManager()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _dataDir = Path.Combine(appData, "TargetTimer", "data");
            if (!Directory.Exists(_dataDir))
            {
                Directory.CreateDirectory(_dataDir);
            }
            EnsureCurrentDay();
            EnsureTotals();
        }

        public string DataDirectory
        {
            get { return _dataDir; }
        }

        private void EnsureTotals()
        {
            if (_totals == null)
            {
                string path = Path.Combine(_dataDir, "totals.json");
                if (File.Exists(path))
                {
                    try
                    {
                        string json = File.ReadAllText(path, Encoding.UTF8);
                        _totals = ParseFromJson(json, "all-time");
                    }
                    catch { }
                }
                if (_totals == null)
                {
                    _totals = new DayActivity { Date = "all-time" };
                }
            }
        }

        private void EnsureCurrentDay()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (_currentDay == null || _currentDateStr != today)
            {
                if (_currentDay != null && _isDirty)
                {
                    SaveInternal();
                }

                _currentDateStr = today;
                _currentDay = LoadDateInternal(today) ?? new DayActivity { Date = today };
                _isDirty = false;
                PurgeOldData();
            }
        }

        private void PurgeOldData()
        {
            try
            {
                string reportsDir = Path.Combine(_dataDir, "reports");
                if (Directory.Exists(reportsDir))
                {
                    string[] reportFiles = Directory.GetFiles(reportsDir, "report_*.html");
                    DateTime reportCutoff = DateTime.Now.Date.AddDays(-7);
                    foreach (string rf in reportFiles)
                    {
                        if (File.GetLastWriteTime(rf) < reportCutoff)
                        {
                            try { File.Delete(rf); } catch { }
                        }
                    }
                }

                string appDir = Path.GetDirectoryName(_dataDir);
                if (!string.IsNullOrEmpty(appDir) && Directory.Exists(appDir))
                {
                    string crashLog = Path.Combine(appDir, "crash.log");
                    if (File.Exists(crashLog) && new FileInfo(crashLog).Length > 102400)
                    {
                        try { File.Delete(crashLog); } catch { }
                    }
                }
            }
            catch { }
        }

        private static void AddToDay(DayActivity day, string appName, string siteDomain, int seconds)
        {
            day.ActiveSeconds += seconds;

            AppActivity app;
            if (!day.Apps.TryGetValue(appName, out app))
            {
                app = new AppActivity { Name = appName };
                day.Apps[appName] = app;
            }

            app.Seconds += seconds;

            if (!string.IsNullOrEmpty(siteDomain))
            {
                int siteSec;
                if (app.Sites.TryGetValue(siteDomain, out siteSec))
                {
                    app.Sites[siteDomain] = siteSec + seconds;
                }
                else
                {
                    app.Sites[siteDomain] = seconds;
                }
            }
        }

        public void RecordActive(string appName, string siteDomain, int seconds = 1)
        {
            if (string.IsNullOrEmpty(appName)) return;

            lock (_lock)
            {
                EnsureCurrentDay();
                EnsureTotals();

                AddToDay(_currentDay, appName, siteDomain, seconds);
                AddToDay(_totals, appName, siteDomain, seconds);

                _isDirty = true;
            }
        }

        public void RecordAfk(int seconds = 1)
        {
            lock (_lock)
            {
                EnsureCurrentDay();
                EnsureTotals();
                _currentDay.AfkSeconds += seconds;
                _totals.AfkSeconds += seconds;
                _isDirty = true;
            }
        }

        public void Flush()
        {
            lock (_lock)
            {
                if (_isDirty)
                {
                    SaveInternal();
                }
            }
        }

        public DayActivity GetCurrentDay()
        {
            lock (_lock)
            {
                EnsureCurrentDay();
                return _currentDay;
            }
        }

        public DayActivity LoadDate(string dateStr)
        {
            lock (_lock)
            {
                if (dateStr == "all-time")
                {
                    EnsureTotals();
                    return _totals;
                }
                if (dateStr == _currentDateStr)
                {
                    return _currentDay;
                }
                return LoadDateInternal(dateStr);
            }
        }

        public List<string> GetAvailableDates()
        {
            var dates = new List<string>();
            try
            {
                if (Directory.Exists(_dataDir))
                {
                    var files = Directory.GetFiles(_dataDir, "activity_*.json");
                    foreach (var file in files)
                    {
                        string name = Path.GetFileNameWithoutExtension(file);
                        if (name.StartsWith("activity_") && name.Length == 19)
                        {
                            string d = name.Substring(9);
                            dates.Add(d);
                        }
                    }
                }
            }
            catch { }

            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (!dates.Contains(today))
            {
                dates.Add(today);
            }
            dates.Sort();
            dates.Reverse();
            dates.Insert(0, "all-time");
            return dates;
        }

        private void SaveInternal()
        {
            if (_currentDay == null) return;
            try
            {
                SaveFileDirect(Path.Combine(_dataDir, string.Format("activity_{0}.json", _currentDay.Date)), SerializeToJson(_currentDay));
                if (_totals != null)
                {
                    SaveFileDirect(Path.Combine(_dataDir, "totals.json"), SerializeToJson(_totals));
                }
                _isDirty = false;
            }
            catch (Exception ex)
            {
                try
                {
                    string errLog = Path.Combine(_dataDir, "..", "save_error.log");
                    File.AppendAllText(errLog, DateTime.Now + " SaveError: " + ex + "\r\n");
                }
                catch { }
            }
        }

        private static void SaveFileDirect(string filePath, string json)
        {
            string tempPath = filePath + ".tmp";
            File.WriteAllText(tempPath, json, Encoding.UTF8);
            File.Copy(tempPath, filePath, true);
            try { File.Delete(tempPath); } catch { }
        }

        private DayActivity LoadDateInternal(string dateStr)
        {
            string filePath = Path.Combine(_dataDir, string.Format("activity_{0}.json", dateStr));
            if (!File.Exists(filePath)) return null;

            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                return ParseFromJson(json, dateStr);
            }
            catch
            {
                return null;
            }
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.AppendFormat("\\u{0:x4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static string SerializeToJson(DayActivity day)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\n");
            sb.AppendFormat("  \"date\": \"{0}\",\n", EscapeJson(day.Date));
            sb.AppendFormat("  \"activeSeconds\": {0},\n", day.ActiveSeconds);
            sb.AppendFormat("  \"afkSeconds\": {0},\n", day.AfkSeconds);
            sb.Append("  \"apps\": {\n");

            int appCount = 0;
            foreach (var kvp in day.Apps)
            {
                appCount++;
                var app = kvp.Value;
                sb.AppendFormat("    \"{0}\": {{\n", EscapeJson(kvp.Key));
                sb.AppendFormat("      \"name\": \"{0}\",\n", EscapeJson(app.Name));
                sb.AppendFormat("      \"seconds\": {0},\n", app.Seconds);
                sb.Append("      \"sites\": {\n");

                int siteCount = 0;
                foreach (var siteKvp in app.Sites)
                {
                    siteCount++;
                    sb.AppendFormat("        \"{0}\": {1}{2}\n",
                        EscapeJson(siteKvp.Key),
                        siteKvp.Value,
                        siteCount < app.Sites.Count ? "," : "");
                }
                sb.Append("      }\n");
                sb.AppendFormat("    }}{0}\n", appCount < day.Apps.Count ? "," : "");
            }
            sb.Append("  }\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        private static DayActivity ParseFromJson(string json, string fallbackDate)
        {
            DayActivity day = new DayActivity { Date = fallbackDate };
            if (string.IsNullOrWhiteSpace(json)) return day;

            try
            {
                // Simple resilient token parser for DayActivity format
                int activeSecIdx = json.IndexOf("\"activeSeconds\":");
                if (activeSecIdx >= 0)
                {
                    int start = activeSecIdx + 16;
                    int end = json.IndexOfAny(new[] { ',', '\n', '\r', '}' }, start);
                    int val;
                    if (int.TryParse(json.Substring(start, end - start).Trim(), out val))
                    {
                        day.ActiveSeconds = val;
                    }
                }

                int afkSecIdx = json.IndexOf("\"afkSeconds\":");
                if (afkSecIdx >= 0)
                {
                    int start = afkSecIdx + 13;
                    int end = json.IndexOfAny(new[] { ',', '\n', '\r', '}' }, start);
                    if (end > start)
                    {
                        int val;
                        if (int.TryParse(json.Substring(start, end - start).Trim(), out val))
                        {
                            day.AfkSeconds = val;
                        }
                    }
                }

                int appsIdx = json.IndexOf("\"apps\":");
                if (appsIdx >= 0)
                {
                    int appsStart = json.IndexOf('{', appsIdx + 7);
                    if (appsStart >= 0)
                    {
                        int appsEnd = FindMatchingBrace(json, appsStart);
                        if (appsEnd > appsStart)
                        {
                            ParseAppsSection(json.Substring(appsStart + 1, appsEnd - appsStart - 1), day);
                        }
                    }
                }
            }
            catch
            {
                // Fallback gracefully
            }

            return day;
        }

        private static int FindMatchingBrace(string text, int openPos)
        {
            int depth = 0;
            for (int i = openPos; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        private static void ParseAppsSection(string appsContent, DayActivity day)
        {
            int idx = 0;
            while (idx < appsContent.Length)
            {
                int quote1 = appsContent.IndexOf('"', idx);
                if (quote1 < 0) break;
                int quote2 = appsContent.IndexOf('"', quote1 + 1);
                if (quote2 < 0) break;

                string appKey = appsContent.Substring(quote1 + 1, quote2 - quote1 - 1);
                int braceOpen = appsContent.IndexOf('{', quote2 + 1);
                if (braceOpen < 0) break;

                int braceClose = FindMatchingBrace(appsContent, braceOpen);
                if (braceClose < 0) break;

                string appObj = appsContent.Substring(braceOpen + 1, braceClose - braceOpen - 1);
                var app = new AppActivity { Name = appKey };

                int secIdx = appObj.IndexOf("\"seconds\":");
                if (secIdx >= 0)
                {
                    int start = secIdx + 10;
                    int end = appObj.IndexOfAny(new[] { ',', '\n', '\r', '}' }, start);
                    int sec;
                    if (end > start && int.TryParse(appObj.Substring(start, end - start).Trim(), out sec))
                    {
                        app.Seconds = sec;
                    }
                }

                int sitesIdx = appObj.IndexOf("\"sites\":");
                if (sitesIdx >= 0)
                {
                    int sitesOpen = appObj.IndexOf('{', sitesIdx + 8);
                    if (sitesOpen >= 0)
                    {
                        int sitesClose = FindMatchingBrace(appObj, sitesOpen);
                        if (sitesClose > sitesOpen)
                        {
                            string sitesStr = appObj.Substring(sitesOpen + 1, sitesClose - sitesOpen - 1);
                            ParseSitesSection(sitesStr, app);
                        }
                    }
                }

                day.Apps[appKey] = app;
                idx = braceClose + 1;
            }
        }

        private static void ParseSitesSection(string sitesStr, AppActivity app)
        {
            int idx = 0;
            while (idx < sitesStr.Length)
            {
                int quote1 = sitesStr.IndexOf('"', idx);
                if (quote1 < 0) break;
                int quote2 = sitesStr.IndexOf('"', quote1 + 1);
                if (quote2 < 0) break;

                string siteKey = sitesStr.Substring(quote1 + 1, quote2 - quote1 - 1);
                int colon = sitesStr.IndexOf(':', quote2 + 1);
                if (colon < 0) break;

                int valEnd = sitesStr.IndexOfAny(new[] { ',', '\n', '\r', '}' }, colon + 1);
                if (valEnd < 0) valEnd = sitesStr.Length;

                string numStr = sitesStr.Substring(colon + 1, valEnd - colon - 1).Trim();
                int seconds;
                if (int.TryParse(numStr, out seconds))
                {
                    app.Sites[siteKey] = seconds;
                }

                idx = valEnd + 1;
            }
        }
    }
}
