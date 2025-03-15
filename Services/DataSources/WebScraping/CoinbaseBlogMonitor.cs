using System.Collections.Generic;
using System.Threading.Tasks;
using CryptoMonitor.Core.Interfaces.Configuration;
using CryptoMonitor.Core.Interfaces.DataSources;
using CryptoMonitor.Core.Models.Blog;
using CryptoMonitor.Services.WebScraping;
using Microsoft.Extensions.Logging;

namespace CryptoMonitor.Services.DataSources.WebScraping
{
    public class CoinbaseBlogMonitor : IBlogMonitor
    {
        private readonly WebPageMonitorService _webPageMonitorService;
        private readonly ILogger<CoinbaseBlogMonitor> _logger;
        private readonly IConfigurationProvider _configProvider;

        public string SourceName => "CoinbaseBlog";
        public string BlogUrl { get; private set; }

        public CoinbaseBlogMonitor(
            WebPageMonitorService webPageMonitorService,
            ILogger<CoinbaseBlogMonitor> logger,
            IConfigurationProvider configProvider)
        {
            _webPageMonitorService = webPageMonitorService;
            _logger = logger;
            _configProvider = configProvider;

            var settings = _configProvider.GetSettings();
            BlogUrl = settings.DataSources.Coinbase.RoadmapUrl;
        }

        public async Task<(List<BlogToken> Tokens, string HtmlContent)> GetTokensAsync()
        {
            _logger.LogInformation($"Using WebPageMonitorService to fetch and parse content from {BlogUrl}");

            return await _webPageMonitorService.MonitorPageAsync(BlogUrl);
        }

        public async Task<List<BlogToken>> ExtractTokensFromHtml(string html)
        {
            // This method is retained for backward compatibility but delegates to the WebPageMonitorService
            var (tokens, _) = await GetTokensAsync();
            return tokens;
        }
    }
}