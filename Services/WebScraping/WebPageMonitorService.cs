using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CryptoMonitor.Core.Interfaces.Configuration;
using CryptoMonitor.Core.Interfaces.Storage;
using CryptoMonitor.Core.Interfaces.WebScraping;
using CryptoMonitor.Core.Models.Blog;
using Microsoft.Extensions.Logging;

namespace CryptoMonitor.Services.WebScraping
{
    public class WebPageMonitorService
    {
        private readonly HttpClientService _httpClientService;
        private readonly IEnumerable<IPageParser> _parsers;
        private readonly IBlogRepository _blogRepository;
        private readonly IConfigurationProvider _configProvider;
        private readonly ILogger<WebPageMonitorService> _logger;

        public WebPageMonitorService(
            HttpClientService httpClientService,
            IEnumerable<IPageParser> parsers,
            IBlogRepository blogRepository,
            IConfigurationProvider configProvider,
            ILogger<WebPageMonitorService> logger)
        {
            _httpClientService = httpClientService;
            _parsers = parsers;
            _blogRepository = blogRepository;
            _configProvider = configProvider;
            _logger = logger;
        }

        public async Task<(List<BlogToken> Tokens, string HtmlContent)> MonitorPageAsync(string url)
        {
            try
            {
                // Find an appropriate parser for this URL
                var parser = _parsers.FirstOrDefault(p => p.CanParse(url));

                if (parser == null)
                {
                    _logger.LogWarning($"No parser found that can handle URL: {url}");
                    return (new List<BlogToken>(), string.Empty);
                }

                _logger.LogInformation($"Using parser '{parser.ParserType}' for URL: {url}");

                // Fetch the HTML content
                string htmlContent = await _httpClientService.GetHtmlContentAsync(url);

                if (string.IsNullOrEmpty(htmlContent))
                {
                    _logger.LogWarning("Failed to fetch HTML content");
                    return (new List<BlogToken>(), string.Empty);
                }

                // Parse the tokens
                var tokens = await parser.ParseTokensAsync(htmlContent);

                _logger.LogInformation($"Parsed {tokens.Count} tokens from URL: {url}");

                return (tokens, htmlContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error monitoring page at {url}: {ex.Message}");
                return (new List<BlogToken>(), string.Empty);
            }
        }

        public async Task<(List<BlogToken> NewTokens, List<BlogToken> RemovedTokens)> CheckForChangesAsync(string url, string sourceName)
        {
            try
            {
                var (currentTokens, _) = await MonitorPageAsync(url);

                if (currentTokens.Count == 0)
                {
                    _logger.LogInformation($"No tokens found at {url}");
                    return (new List<BlogToken>(), new List<BlogToken>());
                }

                // Get changes by comparing with previous data
                var changes = await _blogRepository.GetChangesAsync(currentTokens, sourceName);

                if (changes.NewTokens.Count > 0 || changes.RemovedTokens.Count > 0)
                {
                    // Save the current state
                    await _blogRepository.SaveTokensAsync(currentTokens, sourceName);
                    _logger.LogInformation($"Changes detected for {sourceName}: {changes.NewTokens.Count} new tokens, {changes.RemovedTokens.Count} removed tokens");
                }
                else
                {
                    _logger.LogInformation($"No changes detected for {sourceName}");
                }

                return changes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking for changes at {url}: {ex.Message}");
                return (new List<BlogToken>(), new List<BlogToken>());
            }
        }
    }
}