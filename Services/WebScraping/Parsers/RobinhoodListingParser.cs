using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CryptoMonitor.Core.Interfaces.WebScraping;
using CryptoMonitor.Core.Models.Blog;
using Microsoft.Extensions.Logging;

namespace CryptoMonitor.Services.WebScraping.Parsers
{
    public class RobinhoodListingParser : IPageParser
    {
        private readonly ILogger<RobinhoodListingParser> _logger;

        public string SourceName => "RobinhoodListing";
        public string ParserType => "RobinhoodListing";

        public RobinhoodListingParser(ILogger<RobinhoodListingParser> logger)
        {
            _logger = logger;
        }

        public bool CanParse(string url)
        {
            return url.Contains("robinhood.com") && url.Contains("lists/robinhood");
        }

        public async Task<List<BlogToken>> ParseTokensAsync(string htmlContent)
        {
            var tokens = new List<BlogToken>();

            try
            {
                // Log HTML sample for debugging
                _logger.LogInformation($"HTML Content sample (first 500 chars): {htmlContent.Substring(0, Math.Min(500, htmlContent.Length))}");

                // Try direct URL extraction - Robinhood uses URLs like /crypto/BTC that we can extract
                var cryptoUrls = Regex.Matches(htmlContent, @"href=""/crypto/([A-Z0-9]+)""", RegexOptions.IgnoreCase);
                _logger.LogInformation($"Found {cryptoUrls.Count} crypto URLs");

                // Track unique symbols to avoid duplicates
                HashSet<string> processedSymbols = new HashSet<string>();

                // Process each crypto URL
                foreach (Match match in cryptoUrls)
                {
                    if (match.Groups.Count >= 2)
                    {
                        string symbol = match.Groups[1].Value.ToUpper();

                        // Skip duplicates
                        if (processedSymbols.Contains(symbol))
                            continue;

                        processedSymbols.Add(symbol);

                        // Try to find the name near the URL
                        string surroundingText = GetSurroundingText(htmlContent, match.Index, 1000);
                        string name = ExtractNameFromContext(surroundingText, symbol);

                        if (string.IsNullOrEmpty(name))
                            name = symbol; // Fallback to using symbol as name

                        _logger.LogInformation($"Extracted token: {name} ({symbol})");

                        tokens.Add(new BlogToken
                        {
                            Name = name,
                            Symbol = symbol,
                            ContractAddress = "Robinhood",
                            Network = "Robinhood",
                            DiscoveredAt = DateTime.UtcNow
                        });
                    }
                }

                // If no tokens found through URLs, try alternate approaches
                if (tokens.Count == 0)
                {
                    // Look for embedded JSON data that might contain the listings
                    var jsonMatches = Regex.Matches(htmlContent, @"initialState"":(.*?),""pageType", RegexOptions.Singleline);
                    if (jsonMatches.Count > 0 && jsonMatches[0].Groups.Count > 1)
                    {
                        string jsonData = jsonMatches[0].Groups[1].Value;
                        _logger.LogInformation($"Found potential JSON data: {jsonData.Substring(0, Math.Min(200, jsonData.Length))}");

                        // Extract symbols from JSON
                        var symbolMatches = Regex.Matches(jsonData, @"""([A-Z0-9]{2,10})""", RegexOptions.IgnoreCase);
                        foreach (Match symbolMatch in symbolMatches)
                        {
                            string symbol = symbolMatch.Groups[1].Value.ToUpper();

                            // Filter out common JSON keys that might look like symbols
                            if (symbol.Length >= 2 && !IsCommonJsonKey(symbol) && !processedSymbols.Contains(symbol))
                            {
                                processedSymbols.Add(symbol);
                                _logger.LogInformation($"Found potential token symbol in JSON: {symbol}");

                                tokens.Add(new BlogToken
                                {
                                    Name = symbol, // Use symbol as name
                                    Symbol = symbol,
                                    ContractAddress = "Robinhood",
                                    Network = "Robinhood",
                                    DiscoveredAt = DateTime.UtcNow
                                });
                            }
                        }
                    }
                }

                _logger.LogInformation($"Extracted {tokens.Count} tokens from Robinhood listing");

                if (tokens.Count > 0)
                {
                    foreach (var token in tokens.Take(3))
                    {
                        _logger.LogInformation($"Found token: {token.Name} ({token.Symbol})");
                    }

                    if (tokens.Count > 3)
                    {
                        _logger.LogInformation($"...and {tokens.Count - 3} more tokens");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting tokens: {Message}", ex.Message);
            }

            return await Task.FromResult(tokens);
        }

        private string GetSurroundingText(string html, int position, int window)
        {
            int start = Math.Max(0, position - window/2);
            int length = Math.Min(window, html.Length - start);
            return html.Substring(start, length);
        }

        private string ExtractNameFromContext(string context, string symbol)
        {
            // Try different patterns to extract name
            string[] patterns = {
                $@">([\w\s]+)<[^>]*>{symbol}",   // Name before symbol
                $@"{symbol}[^<]*<[^>]*>([\w\s]+)<",  // Name after symbol
                $@"title=""([^""]+){symbol}""",   // Title attribute containing the symbol
                $@"alt=""([^""]+){symbol}"""      // Alt attribute containing the symbol
            };

            foreach (string pattern in patterns)
            {
                var match = Regex.Match(context, pattern);
                if (match.Success && match.Groups.Count > 1)
                {
                    string name = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(name) && name.Length > 1)
                        return name;
                }
            }

            return string.Empty;
        }

        private bool IsCommonJsonKey(string text)
        {
            // List of common words in JSON that aren't crypto symbols
            string[] commonWords = { "ID", "URL", "API", "GET", "PUT", "KEY", "NEW", "OLD", "CSS", "DIV", "IMG", "SRC" };
            return commonWords.Contains(text);
        }

        public bool IsHtmlChanged(string oldHtml, string newHtml)
        {
            if (string.IsNullOrEmpty(oldHtml) || string.IsNullOrEmpty(newHtml))
                return true;

            int lengthDiff = Math.Abs(oldHtml.Length - newHtml.Length);
            if (lengthDiff > (oldHtml.Length * 0.01))
                return true;

            // Extract and compare crypto URLs from both HTML versions
            var oldUrls = Regex.Matches(oldHtml, @"href=""/crypto/([A-Z0-9]+)""", RegexOptions.IgnoreCase)
                              .Cast<Match>()
                              .Select(m => m.Groups[1].Value.ToUpper())
                              .OrderBy(s => s)
                              .ToList();

            var newUrls = Regex.Matches(newHtml, @"href=""/crypto/([A-Z0-9]+)""", RegexOptions.IgnoreCase)
                              .Cast<Match>()
                              .Select(m => m.Groups[1].Value.ToUpper())
                              .OrderBy(s => s)
                              .ToList();

            // If URL counts differ, content has changed
            if (oldUrls.Count != newUrls.Count)
                return true;

            // Check if any URLs differ
            for (int i = 0; i < oldUrls.Count; i++)
            {
                if (oldUrls[i] != newUrls[i])
                    return true;
            }

            return false;
        }
    }
}