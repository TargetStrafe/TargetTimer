using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace TargetTimer
{
    public static class DashboardGenerator
    {
        public static string GenerateAndOpen(StorageManager storage, string targetDate = null)
        {
            if (string.IsNullOrEmpty(targetDate))
            {
                targetDate = DateTime.Now.ToString("yyyy-MM-dd");
            }

            DayActivity day = storage.LoadDate(targetDate) ?? new DayActivity { Date = targetDate };
            List<string> availableDates = storage.GetAvailableDates();

            string html = BuildHtml(day, availableDates);
            string reportsDir = Path.Combine(storage.DataDirectory, "reports");
            if (!Directory.Exists(reportsDir))
            {
                Directory.CreateDirectory(reportsDir);
            }

            try
            {
                string srcIcon = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.png");
                string destIcon = Path.Combine(reportsDir, "icon.png");
                if (File.Exists(srcIcon))
                {
                    File.Copy(srcIcon, destIcon, true);
                }
            }
            catch { }

            // Pre-generate all-time report so switching works immediately
            DayActivity allTime = storage.LoadDate("all-time");
            if (allTime != null)
            {
                File.WriteAllText(Path.Combine(reportsDir, "report_all-time.html"), BuildHtml(allTime, availableDates), Encoding.UTF8);
            }

            string filePath = Path.Combine(reportsDir, string.Format("report_{0}.html", targetDate));
            File.WriteAllText(filePath, html, Encoding.UTF8);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch { }

            return filePath;
        }

        private static string FormatTime(int totalSeconds)
        {
            if (totalSeconds < 60)
            {
                return string.Format("{0} сек", totalSeconds);
            }

            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;

            if (hours > 0)
            {
                return string.Format("{0}ч {1:D2}м {2:D2}с", hours, minutes, seconds);
            }
            return string.Format("{0}м {1:D2}с", minutes, seconds);
        }

        private static string BuildHtml(DayActivity day, List<string> availableDates)
        {
            // Collect aggregated sites
            var siteAggregates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var appList = new List<AppActivity>(day.Apps.Values);
            appList.Sort((a, b) => b.Seconds.CompareTo(a.Seconds));

            foreach (var app in appList)
            {
                foreach (var siteKvp in app.Sites)
                {
                    int current;
                    if (siteAggregates.TryGetValue(siteKvp.Key, out current))
                    {
                        siteAggregates[siteKvp.Key] = current + siteKvp.Value;
                    }
                    else
                    {
                        siteAggregates[siteKvp.Key] = siteKvp.Value;
                    }
                }
            }

            var siteList = new List<KeyValuePair<string, int>>(siteAggregates);
            siteList.Sort((a, b) => b.Value.CompareTo(a.Value));

            string topApp = appList.Count > 0 ? string.Format("{0} ({1})", appList[0].Name, FormatTime(appList[0].Seconds)) : "—";
            string topSite = siteList.Count > 0 ? string.Format("{0} ({1})", siteList[0].Key, FormatTime(siteList[0].Value)) : "—";

            string displayDate = (day.Date == "all-time") ? "За всё время (Lifetime)" : day.Date;
            StringBuilder sb = new StringBuilder();
            sb.Append("<!DOCTYPE html>\n<html lang=\"ru\">\n<head>\n");
            sb.Append("<meta charset=\"UTF-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\n");
            sb.AppendFormat("<title>TargetTimer - {0}</title>\n", displayDate);
            sb.Append("<style>\n");
            sb.Append(@"
                :root {
                    --bg: #0d0e15;
                    --card: #151824;
                    --card-hover: #1c2030;
                    --accent-purple: #8b5cf6;
                    --accent-cyan: #06b6d4;
                    --accent-pink: #ec4899;
                    --accent-gold: #f59e0b;
                    --text-main: #f8fafc;
                    --text-muted: #94a3b8;
                    --border: #23283b;
                    --progress-bg: #1e2438;
                }
                * { box-sizing: border-box; margin: 0; padding: 0; font-family: 'Segoe UI', system-ui, -apple-system, sans-serif; }
                body { background: var(--bg); color: var(--text-main); padding: 30px 20px; line-height: 1.5; }
                .container { max-width: 1100px; margin: 0 auto; }
                header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 25px; flex-wrap: wrap; gap: 15px; }
                .logo { display: flex; align-items: center; gap: 12px; }
                .logo-icon { width: 42px; height: 42px; border-radius: 12px; background: linear-gradient(135deg, var(--accent-purple), var(--accent-cyan)); display: flex; align-items: center; justify-content: center; font-size: 22px; font-weight: bold; box-shadow: 0 4px 15px rgba(139, 92, 246, 0.4); }
                .logo-title { font-size: 24px; font-weight: 800; background: linear-gradient(90deg, #c084fc, #38bdf8); -webkit-background-clip: text; -webkit-text-fill-color: transparent; }
                .logo-sub { font-size: 13px; color: var(--text-muted); }
                
                .date-selector { display: flex; gap: 8px; align-items: center; background: var(--card); padding: 8px 14px; border-radius: 10px; border: 1px solid var(--border); }
                select { background: transparent; border: none; color: var(--text-main); font-size: 14px; font-weight: 600; outline: none; cursor: pointer; }
                select option { background: var(--card); color: var(--text-main); }
                
                .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px; margin-bottom: 30px; }
                .stat-card { background: var(--card); border: 1px solid var(--border); padding: 20px; border-radius: 14px; transition: transform 0.2s, border-color 0.2s; position: relative; overflow: hidden; }
                .stat-card:hover { transform: translateY(-2px); border-color: #3b4260; }
                .stat-card::after { content: ''; position: absolute; top: 0; left: 0; right: 0; height: 3px; }
                .stat-card.active::after { background: linear-gradient(90deg, #10b981, #06b6d4); }
                .stat-card.afk::after { background: linear-gradient(90deg, #f59e0b, #ef4444); }
                .stat-card.top-app::after { background: linear-gradient(90deg, var(--accent-purple), var(--accent-pink)); }
                .stat-card.top-site::after { background: linear-gradient(90deg, var(--accent-cyan), #3b82f6); }
                .stat-label { font-size: 13px; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.5px; font-weight: 600; margin-bottom: 6px; }
                .stat-val { font-size: 24px; font-weight: 700; }

                .content-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; }
                @media (max-width: 860px) { .content-grid { grid-template-columns: 1fr; } }

                .panel { background: var(--card); border: 1px solid var(--border); border-radius: 14px; padding: 22px; }
                .panel-title { font-size: 18px; font-weight: 700; margin-bottom: 18px; display: flex; align-items: center; justify-content: space-between; }
                .panel-title span { display: flex; align-items: center; gap: 8px; }
                .badge { font-size: 12px; background: var(--border); color: var(--text-muted); padding: 3px 10px; border-radius: 20px; font-weight: 600; }

                .item-list { display: flex; flex-direction: column; gap: 14px; }
                .item-row { display: flex; flex-direction: column; gap: 6px; }
                .item-header { display: flex; justify-content: space-between; font-size: 14px; font-weight: 600; }
                .item-name { display: flex; align-items: center; gap: 8px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
                .item-time { color: var(--text-muted); font-size: 13px; white-space: nowrap; }
                .progress-bar { width: 100%; height: 7px; background: var(--progress-bg); border-radius: 6px; overflow: hidden; }
                .progress-fill { height: 100%; border-radius: 6px; transition: width 0.4s ease; }
                .fill-purple { background: linear-gradient(90deg, #8b5cf6, #c084fc); }
                .fill-cyan { background: linear-gradient(90deg, #06b6d4, #38bdf8); }
                
                .sub-sites { margin-left: 18px; margin-top: 6px; border-left: 2px solid var(--border); padding-left: 10px; display: flex; flex-direction: column; gap: 6px; font-size: 12px; color: var(--text-muted); }
                .sub-site-row { display: flex; justify-content: space-between; }
                .empty-state { text-align: center; color: var(--text-muted); padding: 40px 0; font-size: 14px; }
                footer { margin-top: 35px; text-align: center; font-size: 12px; color: #475569; }
            ");
            sb.Append("</style>\n</head>\n<body>\n");
            sb.Append("<div class=\"container\">\n");

            // Header
            sb.Append("<header>\n");
            sb.Append("  <div class=\"logo\">\n");
            sb.Append("    <img src=\"icon.png\" width=\"46\" height=\"46\" style=\"object-fit:contain; border-radius:10px; filter: drop-shadow(0 2px 8px rgba(0,0,0,0.5));\" alt=\"TargetTimer\" onerror=\"this.outerHTML='<div class=\\'logo-icon\\'>⏳</div>'\">\n");
            sb.Append("    <div>\n");
            sb.Append("      <div class=\"logo-title\">TargetTimer</div>\n");
            sb.Append("      <div class=\"logo-sub\">Фоновый трекер времени &bull; Потребление RAM: ~3 МБ</div>\n");
            sb.Append("    </div>\n");
            sb.Append("  </div>\n");

            // Date picker
            sb.Append("  <div class=\"date-selector\">\n");
            sb.Append("    <label for=\"dateSelect\">📅 Дата: </label>\n");
            sb.Append("    <select id=\"dateSelect\" onchange=\"switchDate(this.value)\">\n");
            foreach (var d in availableDates)
            {
                string label = d;
                if (d == "all-time") label = "🏆 За всё время (Lifetime)";
                else if (d == DateTime.Now.ToString("yyyy-MM-dd")) label = d + " (Сегодня)";

                sb.AppendFormat("      <option value=\"{0}\"{1}>{2}</option>\n",
                    d,
                    d == day.Date ? " selected" : "",
                    label);
            }
            sb.Append("    </select>\n");
            sb.Append("  </div>\n");
            sb.Append("</header>\n");

            // Stat cards
            string activeLabel = (day.Date == "all-time") ? "Общее время за всё время" : "Активное время";
            sb.Append("<div class=\"stats-grid\">\n");
            sb.Append("  <div class=\"stat-card active\">\n");
            sb.AppendFormat("    <div class=\"stat-label\">{0}</div>\n", activeLabel);
            sb.AppendFormat("    <div class=\"stat-val\">{0}</div>\n", FormatTime(day.ActiveSeconds));
            sb.Append("  </div>\n");

            sb.Append("  <div class=\"stat-card afk\">\n");
            sb.Append("    <div class=\"stat-label\">Время бездействия (AFK)</div>\n");
            sb.AppendFormat("    <div class=\"stat-val\">{0}</div>\n", FormatTime(day.AfkSeconds));
            sb.Append("  </div>\n");

            sb.Append("  <div class=\"stat-card top-app\">\n");
            sb.Append("    <div class=\"stat-label\">Главное приложение</div>\n");
            sb.AppendFormat("    <div class=\"stat-val\" style=\"font-size: 18px;\" title=\"{0}\">{0}</div>\n", topApp);
            sb.Append("  </div>\n");

            sb.Append("  <div class=\"stat-card top-site\">\n");
            sb.Append("    <div class=\"stat-label\">Главный веб-сайт</div>\n");
            sb.AppendFormat("    <div class=\"stat-val\" style=\"font-size: 18px;\" title=\"{0}\">{0}</div>\n", topSite);
            sb.Append("  </div>\n");
            sb.Append("</div>\n");

            // Main 2-column layout
            sb.Append("<div class=\"content-grid\">\n");

            // Panel: Applications
            sb.Append("  <div class=\"panel\">\n");
            sb.Append("    <div class=\"panel-title\">\n");
            sb.Append("      <span>💻 Программы и приложения</span>\n");
            sb.AppendFormat("      <span class=\"badge\">Всего: {0}</span>\n", appList.Count);
            sb.Append("    </div>\n");

            if (appList.Count == 0)
            {
                sb.Append("    <div class=\"empty-state\">Данных пока нет. Приложение только начало отслеживание!</div>\n");
            }
            else
            {
                sb.Append("    <div class=\"item-list\">\n");
                int maxAppSec = appList[0].Seconds > 0 ? appList[0].Seconds : 1;
                foreach (var app in appList)
                {
                    double percent = Math.Round(((double)app.Seconds / maxAppSec) * 100, 1);
                    double shareOfTotal = day.ActiveSeconds > 0 ? Math.Round(((double)app.Seconds / day.ActiveSeconds) * 100, 1) : 0;

                    sb.Append("      <div class=\"item-row\">\n");
                    sb.Append("        <div class=\"item-header\">\n");
                    sb.AppendFormat("          <span class=\"item-name\"><strong>{0}</strong></span>\n", app.Name);
                    sb.AppendFormat("          <span class=\"item-time\">{0} ({1}%)</span>\n", FormatTime(app.Seconds), shareOfTotal);
                    sb.Append("        </div>\n");
                    sb.Append("        <div class=\"progress-bar\">\n");
                    sb.AppendFormat("          <div class=\"progress-fill fill-purple\" style=\"width: {0}%;\"></div>\n", percent);
                    sb.Append("        </div>\n");

                    if (app.Sites.Count > 0)
                    {
                        sb.Append("        <div class=\"sub-sites\">\n");
                        var sortedSubSites = new List<KeyValuePair<string, int>>(app.Sites);
                        sortedSubSites.Sort((a, b) => b.Value.CompareTo(a.Value));
                        int shown = 0;
                        foreach (var sub in sortedSubSites)
                        {
                            if (shown++ >= 5) break;
                            sb.Append("          <div class=\"sub-site-row\">\n");
                            sb.AppendFormat("            <span>🌐 {0}</span>\n", sub.Key);
                            sb.AppendFormat("            <span>{0}</span>\n", FormatTime(sub.Value));
                            sb.Append("          </div>\n");
                        }
                        if (sortedSubSites.Count > 5)
                        {
                            sb.AppendFormat("          <div style=\"font-size:11px; color:#64748b;\">...и еще {0} сайтов</div>\n", sortedSubSites.Count - 5);
                        }
                        sb.Append("        </div>\n");
                    }

                    sb.Append("      </div>\n");
                }
                sb.Append("    </div>\n");
            }
            sb.Append("  </div>\n");

            // Panel: Websites
            sb.Append("  <div class=\"panel\">\n");
            sb.Append("    <div class=\"panel-title\">\n");
            sb.Append("      <span>🌐 Веб-сайты и домены</span>\n");
            sb.AppendFormat("      <span class=\"badge\">Всего: {0}</span>\n", siteList.Count);
            sb.Append("    </div>\n");

            if (siteList.Count == 0)
            {
                sb.Append("    <div class=\"empty-state\">Сайты пока не зафиксированы.<br>Откройте браузер (Chrome, Edge, Firefox, Brave) для начала учета!</div>\n");
            }
            else
            {
                sb.Append("    <div class=\"item-list\">\n");
                int maxSiteSec = siteList[0].Value > 0 ? siteList[0].Value : 1;
                foreach (var site in siteList)
                {
                    double percent = Math.Round(((double)site.Value / maxSiteSec) * 100, 1);
                    double shareOfTotal = day.ActiveSeconds > 0 ? Math.Round(((double)site.Value / day.ActiveSeconds) * 100, 1) : 0;

                    sb.Append("      <div class=\"item-row\">\n");
                    sb.Append("        <div class=\"item-header\">\n");
                    sb.AppendFormat("          <span class=\"item-name\"><strong>{0}</strong></span>\n", site.Key);
                    sb.AppendFormat("          <span class=\"item-time\">{0} ({1}%)</span>\n", FormatTime(site.Value), shareOfTotal);
                    sb.Append("        </div>\n");
                    sb.Append("        <div class=\"progress-bar\">\n");
                    sb.AppendFormat("          <div class=\"progress-fill fill-cyan\" style=\"width: {0}%;\"></div>\n", percent);
                    sb.Append("        </div>\n");
                    sb.Append("      </div>\n");
                }
                sb.Append("    </div>\n");
            }
            sb.Append("  </div>\n");

            sb.Append("</div>\n"); // content-grid

            // Footer
            sb.Append("<footer>\n");
            sb.AppendFormat("  TargetTimer &bull; Отчет сформирован: {0} &bull; Нажмите F5 для обновления страницы\n", DateTime.Now.ToString("HH:mm:ss"));
            sb.Append("</footer>\n");

            // Script for date selector
            sb.Append("<script>\n");
            sb.Append("  function switchDate(date) {\n");
            sb.Append("    var currentPath = window.location.pathname;\n");
            sb.Append("    var newFile = 'report_' + date + '.html';\n");
            sb.Append("    var dir = currentPath.substring(0, currentPath.lastIndexOf('/') + 1);\n");
            sb.Append("    window.location.href = dir + newFile;\n");
            sb.Append("  }\n");
            sb.Append("</script>\n");

            sb.Append("</div>\n</body>\n</html>");
            return sb.ToString();
        }
    }
}
