using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CryptoMonitor.Core.Interfaces.WebScraping;
using CryptoMonitor.Core.Models.Blog;
using Microsoft.Extensions.Logging;

namespace CryptoMonitor.Services.WebScraping.Parsers
{
    public class CoinbaseRoadmapParser : IPageParser
    {
        private readonly ILogger<CoinbaseRoadmapParser> _logger;

        // Exact section identifiers from the HTML
        private readonly string[] _ethereumIdentifiers = new[] {
            "Assets on the Ethereum blockchain (ERC-20 tokens)",
            "Assets on the Ethereum"
        };

        private readonly string[] _baseIdentifiers = new[] {
            "Assets on the Base network"
        };

        public string SourceName => "CoinbaseBlog";
        public string ParserType => "CoinbaseRoadmap";

        public CoinbaseRoadmapParser(ILogger<CoinbaseRoadmapParser> logger)
        {
            _logger = logger;
        }

        public bool CanParse(string url)
        {
            return url.Contains("coinbase.com") &&
                  (url.Contains("roadmap") ||
                   url.Contains("transparency") ||
                   url.Contains("asset-listings") ||
                   url.Contains("new-asset"));
        }

        public async Task<List<BlogToken>> ParseTokensAsync(string htmlContent)
        {
            var tokens = new List<BlogToken>();

            try
            {
                // Extract Ethereum tokens
                foreach (var identifier in _ethereumIdentifiers)
                {
                    if (htmlContent.Contains(identifier))
                    {
                        _logger.LogInformation($"Found Ethereum section: '{identifier}'");
                        ExtractTokensFromSection(htmlContent, identifier, "Ethereum", tokens);
                        break; // Only process the first matching identifier
                    }
                }

                // Extract Base network tokens
                foreach (var identifier in _baseIdentifiers)
                {
                    if (htmlContent.Contains(identifier))
                    {
                        _logger.LogInformation($"Found Base network section: '{identifier}'");
                        ExtractTokensFromSection(htmlContent, identifier, "Base", tokens);
                        break; // Only process the first matching identifier
                    }
                }

                // If we didn't find any tokens using the section approach, try to extract from the entire document
                if (tokens.Count == 0)
                {
                    _logger.LogInformation("No tokens found using section approach, trying to extract from entire document");
                    ExtractTokensFromEntireDocument(htmlContent, tokens);
                }

                _logger.LogInformation($"Successfully extracted {tokens.Count} tokens from HTML");

                if (tokens.Count > 0)
                {
                    foreach (var token in tokens.GetRange(0, Math.Min(3, tokens.Count)))
                    {
                        _logger.LogInformation($"Found token: {token.Name} ({token.Symbol}) on {token.Network} - {token.ContractAddress}");
                    }

                    if (tokens.Count > 3)
                    {
                        _logger.LogInformation($"...and {tokens.Count - 3} more tokens");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting tokens from HTML: {Message}", ex.Message);
            }

            return await Task.FromResult(tokens);
        }

        public bool IsHtmlChanged(string oldHtml, string newHtml)
        {
            if (string.IsNullOrEmpty(oldHtml) || string.IsNullOrEmpty(newHtml))
                return true;

            // Compare HTML length as simple change detection
            int lengthDiff = Math.Abs(oldHtml.Length - newHtml.Length);

            // If length changed by more than 0.5%, consider it changed
            return lengthDiff > (oldHtml.Length * 0.005);
        }

        private void ExtractTokensFromSection(string html, string sectionIdentifier, string network, List<BlogToken> tokens)
        {
            try
            {
                // Find the section
                int sectionStart = html.IndexOf(sectionIdentifier);
                if (sectionStart == -1)
                {
                    _logger.LogWarning($"Section '{sectionIdentifier}' not found in HTML");
                    return;
                }

                // Look for the next section or the end of the document
                int nextSection = int.MaxValue;
                string[] nextSectionMarkers = { "Assets on the", "*This is not an exhaustive list" };

                foreach (var marker in nextSectionMarkers)
                {
                    int markerIndex = html.IndexOf(marker, sectionStart + sectionIdentifier.Length);
                    if (markerIndex > sectionStart && markerIndex < nextSection)
                    {
                        nextSection = markerIndex;
                    }
                }

                // If we didn't find a next section, use a reasonable chunk of HTML after the section
                int sectionEnd = nextSection != int.MaxValue ? nextSection : sectionStart + 5000;

                // Extract the section's HTML
                string sectionHtml = html.Substring(sectionStart, sectionEnd - sectionStart);

                // Pattern based on the exact format seen in the HTML: "Name (SYMBOL) - Contract address: 0x..."
                string pattern = @"([a-zA-Z0-9\s]+)\s*\(([A-Z0-9]+)\)\s*-\s*Contract\s*address:\s*(0x[a-fA-F0-9]+)";

                MatchCollection matches = Regex.Matches(sectionHtml, pattern);

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

                        _logger.LogDebug($"Extracted token from section: {name} ({symbol}) on {network}");
                    }
                }

                _logger.LogInformation($"Found {matches.Count} tokens in the {network} section");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error extracting tokens from {network} section: {ex.Message}");
            }
        }

        private void ExtractTokensFromEntireDocument(string html, List<BlogToken> tokens)
        {
            try
            {
                // Pattern for tokens anywhere in the document: "Name (SYMBOL) - Contract address: 0x..."
                string pattern = @"([a-zA-Z0-9\s]+)\s*\(([A-Z0-9]+)\)\s*-\s*Contract\s*address:\s*(0x[a-fA-F0-9]+)";

                MatchCollection matches = Regex.Matches(html, pattern);

                foreach (Match match in matches)
                {
                    if (match.Groups.Count >= 4)
                    {
                        string name = match.Groups[1].Value.Trim();
                        string symbol = match.Groups[2].Value.Trim();
                        string contractAddress = match.Groups[3].Value.Trim();

                        // Try to determine the network from the surrounding context
                        string surroundingText = GetSurroundingText(html, match.Index, 300);
                        string network = DetermineNetworkFromContext(surroundingText);

                        tokens.Add(new BlogToken
                        {
                            Name = name,
                            Symbol = symbol,
                            ContractAddress = contractAddress,
                            Network = network,
                            DiscoveredAt = DateTime.UtcNow
                        });

                        _logger.LogDebug($"Extracted token from document: {name} ({symbol}) on {network}");
                    }
                }

                _logger.LogInformation($"Found {matches.Count} tokens in the entire document");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error extracting tokens from entire document: {ex.Message}");
            }
        }

        private string GetSurroundingText(string html, int position, int window)
        {
            int start = Math.Max(0, position - window / 2);
            int length = Math.Min(window, html.Length - start);
            return html.Substring(start, length);
        }

        private string DetermineNetworkFromContext(string context)
        {
            if (context.Contains("Ethereum") || context.Contains("ERC-20") || context.Contains("ERC20"))
                return "Ethereum";
            else if (context.Contains("Base network") || context.Contains("Base"))
                return "Base";
            else
                return "Unknown";
        }
    }
}