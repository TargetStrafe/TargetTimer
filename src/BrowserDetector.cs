using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Automation;

namespace TargetTimer
{
    public static class BrowserDetector
    {
        private static readonly HashSet<string> BrowserExeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "chrome.exe",
            "msedge.exe",
            "firefox.exe",
            "brave.exe",
            "opera.exe",
            "opera_gx.exe",
            "browser.exe", // Yandex
            "yandex.exe",
            "vivaldi.exe",
            "arc.exe",
            "waterfox.exe",
            "floorp.exe",
            "zen.exe"
        };

        private static readonly Dictionary<string, string> KnownSites = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "youtube", "youtube.com" },
            { "ютуб", "youtube.com" },
            { "github", "github.com" },
            { "гитхаб", "github.com" },
            { "chatgpt", "chatgpt.com" },
            { "openai", "chatgpt.com" },
            { "claude", "claude.ai" },
            { "vkontakte", "vk.com" },
            { "вконтакте", "vk.com" },
            { "vk", "vk.com" },
            { "telegram", "web.telegram.org" },
            { "телеграм", "web.telegram.org" },
            { "habr", "habr.com" },
            { "хабр", "habr.com" },
            { "reddit", "reddit.com" },
            { "реддит", "reddit.com" },
            { "stackoverflow", "stackoverflow.com" },
            { "stack overflow", "stackoverflow.com" },
            { "twitch", "twitch.tv" },
            { "твич", "twitch.tv" },
            { "yandex", "yandex.ru" },
            { "яндекс", "yandex.ru" },
            { "google", "google.com" },
            { "гугл", "google.com" },
            { "wikipedia", "wikipedia.org" },
            { "википедия", "wikipedia.org" },
            { "kinopoisk", "kinopoisk.ru" },
            { "кинопоиск", "kinopoisk.ru" },
            { "notion", "notion.so" },
            { "figma", "figma.com" },
            { "discord", "discord.com" },
            { "дискорд", "discord.com" },
            { "gitlab", "gitlab.com" },
            { "mail.ru", "mail.ru" },
            { "майл", "mail.ru" },
            { "ozon", "ozon.ru" },
            { "озон", "ozon.ru" },
            { "wildberries", "wildberries.ru" },
            { "вайлдберриз", "wildberries.ru" },
            { "aliexpress", "aliexpress.com" },
            { "алиэкспресс", "aliexpress.com" },
            { "spotify", "spotify.com" },
            { "netflix", "netflix.com" },
            { "нетфликс", "netflix.com" },
            { "steam", "steampowered.com" },
            { "стим", "steampowered.com" }
        };

        // Cache the last detected URL / domain per window handle to avoid querying UIAutomation every second
        private static IntPtr _lastHwnd = IntPtr.Zero;
        private static string _lastTitle = "";
        private static string _cachedDomain = "";
        private static DateTime _lastUiAutoCheck = DateTime.MinValue;

        public static bool IsBrowser(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return false;
            return BrowserExeNames.Contains(processName);
        }

        public static string DetectSite(IntPtr hWnd, string processName, string windowTitle)
        {
            if (string.IsNullOrEmpty(windowTitle)) return "Unknown";

            // If same window and title, return cached result
            if (hWnd == _lastHwnd && windowTitle == _lastTitle && !string.IsNullOrEmpty(_cachedDomain))
            {
                return _cachedDomain;
            }

            _lastHwnd = hWnd;
            _lastTitle = windowTitle;

            // 1. Try extracting exact URL via UI Automation (works for Chrome, Edge, Brave)
            // Limit to once every 2 seconds to keep CPU at 0%
            string domainFromUrl = null;
            if ((DateTime.Now - _lastUiAutoCheck).TotalMilliseconds > 1500)
            {
                _lastUiAutoCheck = DateTime.Now;
                domainFromUrl = TryGetDomainFromUiAutomation(hWnd, processName);
            }

            if (!string.IsNullOrEmpty(domainFromUrl))
            {
                _cachedDomain = domainFromUrl;
                return domainFromUrl;
            }

            // 2. Fallback to intelligent title parsing
            string domainFromTitle = ExtractDomainFromTitle(windowTitle);
            _cachedDomain = domainFromTitle;
            return domainFromTitle;
        }

        private static string TryGetDomainFromUiAutomation(IntPtr hWnd, string processName)
        {
            try
            {
                var element = AutomationElement.FromHandle(hWnd);
                if (element == null) return null;

                // Condition: ControlType.Edit
                var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
                var edits = element.FindAll(TreeScope.Descendants, condition);

                foreach (AutomationElement edit in edits)
                {
                    try
                    {
                        object pattern;
                        if (edit.TryGetCurrentPattern(ValuePattern.Pattern, out pattern))
                        {
                            var vp = (ValuePattern)pattern;
                            string val = vp.Current.Value;
                            if (!string.IsNullOrEmpty(val))
                            {
                                string clean = CleanUrlToDomain(val);
                                if (!string.IsNullOrEmpty(clean))
                                {
                                    return clean;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch
            {
                // UIAutomation might throw if window is closing or non-responsive
            }
            return null;
        }

        private static string CleanUrlToDomain(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            string s = input.Trim();
            if (!s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (s.Contains(".") && !s.Contains(" "))
                {
                    s = "https://" + s;
                }
                else
                {
                    return null;
                }
            }

            try
            {
                Uri uri = new Uri(s);
                string host = uri.Host.ToLowerInvariant();
                if (host.StartsWith("www."))
                {
                    host = host.Substring(4);
                }
                if (host.Length > 3 && host.Contains("."))
                {
                    return host;
                }
            }
            catch { }

            return null;
        }

        public static string ExtractDomainFromTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "Empty Tab";

            // Clean common browser suffixes
            string clean = title;
            string[] suffixes = new[]
            {
                " - Google Chrome",
                " - Microsoft​ Edge",
                " - Microsoft Edge",
                " — Mozilla Firefox",
                " - Mozilla Firefox",
                " - Brave",
                " — Яндекс",
                " — Яндекс Браузер",
                " - Opera GX",
                " - Opera",
                " - Vivaldi"
            };

            foreach (var suffix in suffixes)
            {
                int pos = clean.LastIndexOf(suffix, StringComparison.OrdinalIgnoreCase);
                if (pos > 0)
                {
                    clean = clean.Substring(0, pos).Trim();
                    break;
                }
            }

            // Check for explicit URLs or domains in title (e.g. "github.com/org/repo")
            var match = Regex.Match(clean, @"\b([a-zA-Z0-9-]+\.(?:com|org|net|ru|io|ai|co|dev|app|tv|so|me|cc|by|kz|ua|info|biz|to|gg))\b", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string d = match.Groups[1].Value.ToLowerInvariant();
                if (d.StartsWith("www.")) d = d.Substring(4);
                return d;
            }

            // Check known site keywords
            foreach (var kvp in KnownSites)
            {
                if (clean.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return kvp.Value;
                }
            }

            // If title contains "Новая вкладка" or "New Tab"
            if (clean.IndexOf("новая вкладка", StringComparison.OrdinalIgnoreCase) >= 0 ||
                clean.IndexOf("new tab", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "newtab";
            }

            // If short enough, clean title can represent the page/site name
            if (clean.Length > 30)
            {
                clean = clean.Substring(0, 30) + "...";
            }

            return clean;
        }
    }
}
