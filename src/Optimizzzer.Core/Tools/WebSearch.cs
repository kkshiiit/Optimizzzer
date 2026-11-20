using System;
using System.IO;

namespace Optimizzzer.Core.Tools
{
    /// <summary>
    /// Builds the "what is this?" web search. The browser's own command-line search is used so each person gets their
    /// default browser AND their chosen search engine; only unknown browsers fall back to a search URL.
    /// </summary>
    public static class WebSearch
    {
        public static string Query(string what, bool spanish) =>
            "\"" + (what ?? "").Replace("\"", "").Trim() + "\" " + (spanish ? "qué es" : "what is");

        /// <summary>Command-line arguments that make this browser search with its default engine, or null if unknown.</summary>
        public static string BrowserArgs(string browserExe, string query)
        {
            if (string.IsNullOrEmpty(browserExe)) return null;
            var name = Path.GetFileNameWithoutExtension(browserExe.Replace('\\', '/').Split('/')[browserExe.Replace('\\', '/').Split('/').Length - 1]).ToLowerInvariant();
            var q = (query ?? "").Replace("\"", "\\\"");
            switch (name)
            {
                case "firefox":
                case "librewolf":
                case "waterfox":
                    return "-search \"" + q + "\"";
                case "chrome":
                case "msedge":
                case "brave":
                case "opera":
                case "launcher": // Opera's launcher.exe
                case "vivaldi":
                case "chromium":
                    // "? text" in the address bar means "search this", with the user's default engine.
                    return "\"? " + q + "\"";
                default:
                    return null;
            }
        }

        public static string FallbackUrl(string query) => "https://www.bing.com/search?q=" + Uri.EscapeDataString(query ?? "");
    }
}
