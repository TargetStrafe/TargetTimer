using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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
            "browser.exe",
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
            { "стим", "steampowered.com" },
            { "pinterest", "pinterest.com" },
            { "пинтерест", "pinterest.com" },
            { "twitter", "x.com" },
            { "твиттер", "x.com" },
            { "x.com", "x.com" }
        };

        private static readonly string[] BrowserSuffixes = new[]
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
            " - Vivaldi",
            " - Arc",
            " - Waterfox",
            " - Zen Browser"
        };

        private static readonly Regex DomainRegex = new Regex(
            @"\b([a-zA-Z0-9-]+\.(?:com|org|net|ru|io|ai|co|dev|app|tv|so|me|cc|by|kz|ua|info|biz|to|gg|online|pro|site|tech|xyz))\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static IntPtr _lastHwnd = IntPtr.Zero;
        private static string _lastRawTitle = null;
        private static string _cachedResult = null;

        public static bool IsBrowser(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return false;
            return BrowserExeNames.Contains(processName);
        }

        public static string DetectSite(IntPtr hWnd, string processName, string windowTitle)
        {
            if (string.IsNullOrEmpty(windowTitle)) return "Unknown";

            // Instant cache check
            if (hWnd == _lastHwnd && string.Equals(windowTitle, _lastRawTitle, StringComparison.Ordinal))
            {
                return _cachedResult;
            }

            _lastHwnd = hWnd;
            _lastRawTitle = windowTitle;

            string domain = ExtractDomainFromTitle(windowTitle);
            _cachedResult = domain;
            return domain;
        }

        public static string ExtractDomainFromTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "Вкладка";

            string clean = title;
            foreach (var suffix in BrowserSuffixes)
            {
                int pos = clean.LastIndexOf(suffix, StringComparison.OrdinalIgnoreCase);
                if (pos > 0)
                {
                    clean = clean.Substring(0, pos).Trim();
                    break;
                }
            }

            // Check for domain patterns in clean title
            var match = DomainRegex.Match(clean);
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

            if (clean.IndexOf("новая вкладка", StringComparison.OrdinalIgnoreCase) >= 0 ||
                clean.IndexOf("new tab", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "newtab";
            }

            if (clean.Length > 28)
            {
                clean = clean.Substring(0, 28) + "...";
            }

            return clean;
        }
    }
}
