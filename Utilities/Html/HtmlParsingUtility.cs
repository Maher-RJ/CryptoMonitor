using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CryptoMonitor.Core.Models.Blog;

namespace CryptoMonitor.Utilities.Html
{
    public static class HtmlParsingUtility
    {
        // Extracts text between two markers in HTML
        public static string ExtractBetween(string html, string startMarker, string endMarker)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            int startIdx = html.IndexOf(startMarker);
            if (startIdx < 0)
                return string.Empty;

            startIdx += startMarker.Length;

            int endIdx = html.IndexOf(endMarker, startIdx);
            if (endIdx < 0)
                return html.Substring(startIdx);

            return html.Substring(startIdx, endIdx - startIdx);
        }

        // Extracts all matches of text between repeated patterns
        public static List<string> ExtractAllBetween(string html, string startMarker, string endMarker)
        {
            var results = new List<string>();
            if (string.IsNullOrEmpty(html))
                return results;

            int currentIndex = 0;

            while (true)
            {
                int startIdx = html.IndexOf(startMarker, currentIndex);
                if (startIdx < 0)
                    break;

                startIdx += startMarker.Length;

                int endIdx = html.IndexOf(endMarker, startIdx);
                if (endIdx < 0)
                    break;

                results.Add(html.Substring(startIdx, endIdx - startIdx));
                currentIndex = endIdx + endMarker.Length;
            }

            return results;
        }

        // Basic HTML sanitization to convert entities and remove tags
        public static string SanitizeHtml(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            // Replace common HTML entities
            string result = html
                .Replace("&amp;", "&")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&quot;", "\"")
                .Replace("&#39;", "'")
                .Replace("&nbsp;", " ");

            // Remove HTML tags
            result = Regex.Replace(result, "<[^>]*>", "");

            // Normalize whitespace
            result = Regex.Replace(result, @"\s+", " ").Trim();

            return result;
        }

        // Extract tokens using regex pattern matching
        public static List<BlogToken> ExtractTokensWithRegex(string html, string pattern, string network)
        {
            var tokens = new List<BlogToken>();

            if (string.IsNullOrEmpty(html))
                return tokens;

            MatchCollection matches = Regex.Matches(html, pattern);

            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 4)
                {
                    string name = match.Groups[1].Value.Trim();
                    string symbol = match.Groups[2].Value.Trim();
                    string contractAddress = match.Groups[3].Value.Trim();

                    tokens.Add(new BlogToken
                    {
                        Name = name,
                        Symbol = symbol,
                        ContractAddress = contractAddress,
                        Network = network,
                        DiscoveredAt = DateTime.UtcNow
                    });
                }
            }

            return tokens;
        }

        // Extract sections of HTML based on heading or section markers
        public static Dictionary<string, string> ExtractSections(string html, List<string> sectionMarkers)
        {
            var sections = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(html) || sectionMarkers == null || sectionMarkers.Count == 0)
                return sections;

            for (int i = 0; i < sectionMarkers.Count; i++)
            {
                string startMarker = sectionMarkers[i];
                string endMarker = (i < sectionMarkers.Count - 1) ? sectionMarkers[i + 1] : null;

                int startIdx = html.IndexOf(startMarker);
                if (startIdx < 0)
                    continue;

                int endIdx;

                if (endMarker != null)
                {
                    endIdx = html.IndexOf(endMarker, startIdx + startMarker.Length);
                    if (endIdx < 0)
                        endIdx = html.Length;
                }
                else
                {
                    endIdx = html.Length;
                }

                sections[startMarker] = html.Substring(startIdx, endIdx - startIdx);
            }

            return sections;
        }

        // Check if HTML has significantly changed
        public static bool HasSignificantChanges(string oldHtml, string newHtml, double thresholdPercent = 0.5)
        {
            if (string.IsNullOrEmpty(oldHtml) || string.IsNullOrEmpty(newHtml))
                return true;

            // First compare length as a quick check
            int lengthDiff = Math.Abs(newHtml.Length - oldHtml.Length);
            double percentChange = (double)lengthDiff / Math.Max(oldHtml.Length, newHtml.Length) * 100;

            if (percentChange > thresholdPercent)
                return true;

            return false;
        }

        // Pattern for Coinbase-style token listings
        public static readonly string CoinbaseTokenPattern =
            @"([a-zA-Z0-9\s]+)\s+\(([A-Z0-9]+)\)\s*-\s*Contract\s+address:\s*(0x[a-fA-F0-9]+)";
    }
}