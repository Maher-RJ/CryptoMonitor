using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using CryptoMonitor.Core.Interfaces.Configuration;
using CryptoMonitor.Core.Interfaces.Notification;
using CryptoMonitor.Core.Interfaces.Storage;
using CryptoMonitor.Core.Interfaces.WebScraping;
using CryptoMonitor.Configuration.Models;
using CryptoMonitor.Services.Notification;
using CryptoMonitor.Services.WebScraping;
using CryptoMonitor.Services.WebScraping.Parsers;

namespace CryptoMonitor
{
    public class WebScrapingMonitorFunction
    {
        private readonly WebPageMonitorService _webPageMonitorService;
        private readonly IConfigurationProvider _configProvider;
        private readonly NotificationFactory _notificationFactory;
        private readonly ParserFactory _parserFactory;
        private readonly ILogger<WebScrapingMonitorFunction> _logger;

        public WebScrapingMonitorFunction(
            WebPageMonitorService webPageMonitorService,
            IConfigurationProvider configProvider,
            NotificationFactory notificationFactory,
            ParserFactory parserFactory,
            ILoggerFactory loggerFactory)
        {
            _webPageMonitorService = webPageMonitorService;
            _configProvider = configProvider;
            _notificationFactory = notificationFactory;
            _parserFactory = parserFactory;
            _logger = loggerFactory.CreateLogger<WebScrapingMonitorFunction>();
        }

        [Function("WebScrapingMonitorFunction")]
        public async Task Run([TimerTrigger("%WebScrapingSchedule%")] TimerInfo timer)
        {
            var settings = _configProvider.GetSettings();
            string modeStatus = settings.TestMode ? "TEST MODE" : "PRODUCTION MODE";
            _logger.LogInformation($"Web scraping monitor function executed at: {DateTime.Now} [{modeStatus}]");

            if (!settings.WebScraping.Enabled)
            {
                _logger.LogInformation("Web scraping is disabled globally. Skipping check.");
                return;
            }

            if (settings.WebScraping.Pages.Count == 0)
            {
                _logger.LogWarning("No web pages configured for monitoring. Skipping check.");
                return;
            }

            _logger.LogInformation($"Found {settings.WebScraping.Pages.Count} web pages configured for monitoring");

            // Get enabled pages
            var enabledPages = settings.WebScraping.Pages.Where(p => p.Enabled).ToList();

            // Filter out pages from disabled data sources
            var filteredPages = new List<WebPageConfig>();
            foreach (var page in enabledPages)
            {
                if (page.Source.Contains("Coinbase") && !settings.DataSources.Coinbase.Enabled)
                {
                    _logger.LogInformation($"Skipping {page.Name} because Coinbase monitoring is disabled");
                    continue;
                }

                if (page.Source.Contains("Robinhood") && !settings.DataSources.Robinhood.Enabled)
                {
                    _logger.LogInformation($"Skipping {page.Name} because Robinhood monitoring is disabled");
                    continue;
                }

                filteredPages.Add(page);
            }

            _logger.LogInformation($"Found {filteredPages.Count} enabled web pages for monitoring after source filtering");

            foreach (var pageConfig in filteredPages)
            {
                try
                {
                    _logger.LogInformation($"Monitoring page: {pageConfig.Name} ({pageConfig.Url})");

                    var (newTokens, removedTokens) = await _webPageMonitorService.CheckForChangesAsync(
                        pageConfig.Url,
                        pageConfig.Source);

                    bool hasChanges = newTokens.Count > 0 || removedTokens.Count > 0;

                    if (hasChanges)
                    {
                        _logger.LogInformation($"Changes detected on {pageConfig.Name}!");

                        if (newTokens.Count > 0)
                        {
                            _logger.LogInformation($"Found {newTokens.Count} new tokens on {pageConfig.Name}!");
                        }

                        if (removedTokens.Count > 0)
                        {
                            _logger.LogInformation($"Found {removedTokens.Count} tokens removed from {pageConfig.Name} (potentially listed)!");
                        }

                        // Send notifications for token changes
                        var notificationChannels = _notificationFactory.GetEnabledChannels();

                        foreach (var channel in notificationChannels)
                        {
                            if (newTokens.Count > 0)
                            {
                                string newSubject = $"New tokens on {pageConfig.Name}! ({newTokens.Count})";
                                await channel.SendNotificationAsync(newSubject, newTokens, pageConfig.Source);
                            }

                            if (removedTokens.Count > 0)
                            {
                                string removedSubject = $"Tokens removed from {pageConfig.Name} roadmap! ({removedTokens.Count}) - Potentially Listed";
                                await channel.SendNotificationAsync(removedSubject, removedTokens, pageConfig.Source);
                            }
                        }

                        _logger.LogInformation($"Notifications sent for {pageConfig.Name}");
                    }
                    else
                    {
                        _logger.LogInformation($"No changes detected on {pageConfig.Name}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error monitoring page {pageConfig.Name}: {ex.Message}");
                }
            }
        }
    }
}