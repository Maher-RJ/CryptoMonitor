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

        // Partial section identifiers that could be fragmented across tags
        private readonly string[] _ethereumPartialIdentifiers = new[] {
            "Assets on the", "Ethereum", "blockchain (ERC-20 tokens)",
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
                // Check for Ethereum section using a more flexible approach
                if (ContainsAllEthereumIdentifiers(htmlContent))
                {
                    _logger.LogInformation("Found Ethereum section identifiers");

                    // Find approximate position of Ethereum section
                    int ethereumSectionStart = FindEthereumSectionStart(htmlContent);
                    if (ethereumSectionStart >= 0)
                    {
                        _logger.LogInformation($"Ethereum section starts around position {ethereumSectionStart}");
                        ExtractTokensFromSection(htmlContent, ethereumSectionStart, "Ethereum", tokens);
                    }
                }

                // Check for Base network section
                foreach (var identifier in _baseIdentifiers)
                {
                    if (htmlContent.Contains(identifier))
                    {
                        _logger.LogInformation($"Found Base network section: '{identifier}'");
                        ExtractTokensFromSection(htmlContent, htmlContent.IndexOf(identifier), "Base", tokens);
                    }
                }

                // If no tokens found, try extracting from the entire document
                if (tokens.Count == 0)
                {
                    _logger.LogInformation("No tokens found in sections, trying to extract from entire document");
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

        // Check if all the necessary parts of Ethereum section are present
        private bool ContainsAllEthereumIdentifiers(string html)
        {
            bool containsAssets = html.Contains("Assets on the");
            bool containsEthereum = html.Contains("Ethereum");
            bool containsBlockchain = html.Contains("blockchain (ERC-20 tokens)");

            _logger.LogDebug($"Ethereum section checks: Assets={containsAssets}, Ethereum={containsEthereum}, Blockchain={containsBlockchain}");

            return containsAssets && containsEthereum && containsBlockchain;
        }

        // Find the approximate start of the Ethereum section
        private int FindEthereumSectionStart(string html)
        {
            // Look for the pattern that typically starts the Ethereum section
            string[] patterns = {
                "<b>Assets on the </b>",
                "Assets on the Ethereum",
                "Assets on the.*Ethereum"
            };

            foreach (string pattern in patterns)
            {
                var match = Regex.Match(html, pattern);
                if (match.Success)
                {
                    return match.Index;
                }
            }

            // Fallback: Just look for "Assets on the" near "Ethereum"
            int assetsIndex = html.IndexOf("Assets on the");
            if (assetsIndex >= 0)
            {
                string contextAfter = html.Substring(assetsIndex, Math.Min(200, html.Length - assetsIndex));
                if (contextAfter.Contains("Ethereum"))
                {
                    return assetsIndex;
                }
            }

            return -1;
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

        private void ExtractTokensFromSection(string html, int sectionStart, string network, List<BlogToken> tokens)
        {
            try
            {
                if (sectionStart == -1)
                {
                    _logger.LogWarning($"Section start not found for {network}");
                    return;
                }

                // Look for the next section or the end of the document
                int nextSection = html.Length;
                string[] nextSectionMarkers = { "Assets on the", "*This is not an exhaustive list" };

                foreach (var marker in nextSectionMarkers)
                {
                    int markerIndex = html.IndexOf(marker, sectionStart + 50); // Add offset to avoid matching the current section
                    if (markerIndex > sectionStart && markerIndex < nextSection)
                    {
                        nextSection = markerIndex;
                    }
                }

                // Extract the section's HTML
                int sectionEnd = Math.Min(nextSection, sectionStart + 10000);
                string sectionHtml = html.Substring(sectionStart, sectionEnd - sectionStart);

                _logger.LogDebug($"Extracted {sectionHtml.Length} characters of HTML for {network} section");

                // Look for list items with tokens
                string listItemPattern = @"<li[^>]*>.*?<p[^>]*>([^<]*\([A-Z0-9]+\)[^<]*Contract\s+address:\s+0x[a-fA-F0-9]+[^<]*)</p>";
                var listItemMatches = Regex.Matches(sectionHtml, listItemPattern, RegexOptions.Singleline);

                _logger.LogDebug($"Found {listItemMatches.Count} list items in {network} section");

                foreach (Match match in listItemMatches)
                {
                    if (match.Groups.Count >= 2)
                    {
                        string content = match.Groups[1].Value;

                        // Extract the token information using a simpler pattern
                        var tokenMatch = Regex.Match(content, @"([a-zA-Z0-9\s]+)\s*\(([A-Z0-9]+)\)\s*-\s*Contract\s+address:\s+(0x[a-fA-F0-9]+)");

                        if (tokenMatch.Success && tokenMatch.Groups.Count >= 4)
                        {
                            string name = tokenMatch.Groups[1].Value.Trim();
                            string symbol = tokenMatch.Groups[2].Value.Trim();
                            string contractAddress = tokenMatch.Groups[3].Value.Trim();

                            // Skip if already in our list
                            if (tokens.Exists(t => t.Symbol == symbol && t.ContractAddress == contractAddress))
                            {
                                continue;
                            }

                            tokens.Add(new BlogToken
                            {
                                Name = name,
                                Symbol = symbol,
                                ContractAddress = contractAddress,
                                Network = network,
                                DiscoveredAt = DateTime.UtcNow
                            });

                            _logger.LogDebug($"Extracted token: {name} ({symbol}) on {network}");
                        }
                    }
                }

                // If no tokens found using list items, try direct paragraph content
                if (tokens.Count == 0)
                {
                    string paragraphPattern = @"<p[^>]*>\s*([a-zA-Z0-9\s]+)\s*\(([A-Z0-9]+)\)\s*-\s*Contract\s+address:\s+(0x[a-fA-F0-9]+)";
                    var paragraphMatches = Regex.Matches(sectionHtml, paragraphPattern, RegexOptions.Singleline);

                    _logger.LogDebug($"Found {paragraphMatches.Count} paragraphs with token info in {network} section");

                    foreach (Match match in paragraphMatches)
                    {
                        if (match.Groups.Count >= 4)
                        {
                            string name = match.Groups[1].Value.Trim();
                            string symbol = match.Groups[2].Value.Trim();
                            string contractAddress = match.Groups[3].Value.Trim();

                            // Skip if already in our list
                            if (tokens.Exists(t => t.Symbol == symbol && t.ContractAddress == contractAddress))
                            {
                                continue;
                            }

                            tokens.Add(new BlogToken
                            {
                                Name = name,
                                Symbol = symbol,
                                ContractAddress = contractAddress,
                                Network = network,
                                DiscoveredAt = DateTime.UtcNow
                            });

                            _logger.LogDebug($"Extracted token from paragraph: {name} ({symbol}) on {network}");
                        }
                    }
                }

                _logger.LogInformation($"Found {tokens.Count} tokens in the {network} section");
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
                _logger.LogInformation("Searching for tokens in the entire document");

                // Look for list items across the entire document
                string listItemPattern = @"<li[^>]*variant=""body""[^>]*display=""list-item""[^>]*>.*?<p[^>]*>([^<]+)</p>";
                var listMatches = Regex.Matches(html, listItemPattern, RegexOptions.Singleline);

                _logger.LogDebug($"Found {listMatches.Count} list items in the entire document");

                foreach (Match match in listMatches)
                {
                    if (match.Groups.Count >= 2)
                    {
                        string content = match.Groups[1].Value;

                        // Check if this content looks like a token entry
                        if (content.Contains("Contract address:") && content.Contains("(") && content.Contains(")"))
                        {
                            var tokenMatch = Regex.Match(content, @"([a-zA-Z0-9\s]+)\s*\(([A-Z0-9]+)\)\s*-\s*Contract\s+address:\s+(0x[a-fA-F0-9]+)");

                            if (tokenMatch.Success && tokenMatch.Groups.Count >= 4)
                            {
                                string name = tokenMatch.Groups[1].Value.Trim();
                                string symbol = tokenMatch.Groups[2].Value.Trim();
                                string contractAddress = tokenMatch.Groups[3].Value.Trim();

                                // Determine network from context
                                string context = GetSurroundingText(html, match.Index, 1000);
                                string network = DetermineNetworkFromContext(context);

                                // Skip if already in our list
                                if (tokens.Exists(t => t.Symbol == symbol && t.ContractAddress == contractAddress))
                                {
                                    continue;
                                }

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
                    }
                }

                _logger.LogInformation($"Found {tokens.Count} tokens in the entire document");
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