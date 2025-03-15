using System;
using System.Collections.Generic;
using CryptoMonitor.Configuration.Models;
using CryptoMonitor.Core.Interfaces.Configuration;
using Microsoft.Extensions.Logging;

namespace CryptoMonitor.Configuration
{
    public class AppConfigurationProvider : IConfigurationProvider
    {
        private readonly ILogger<AppConfigurationProvider> _logger;
        private readonly AppSettings _settings;

        public AppConfigurationProvider(ILogger<AppConfigurationProvider> logger)
        {
            _logger = logger;
            _settings = LoadSettings();
        }

        public AppSettings GetSettings()
        {
            return _settings;
        }

        private AppSettings LoadSettings()
        {
            var settings = new AppSettings();

            try
            {
                // Application mode
                settings.TestMode = GetBoolSetting("TestMode", false);

                // Storage settings (just logging them, they're used directly in BlobTokenRepository)
                var prodContainer = GetStringSetting("ProductionContainerName", "crypto-data");
                var testContainer = GetStringSetting("TestContainerName", "crypto-data-test");
                _logger.LogInformation($"Storage containers configured: Production={prodContainer}, Test={testContainer}");

                // Global API monitoring settings
                settings.ApiMonitoring.Enabled = GetBoolSetting("ApiMonitoring:Enabled", true);
                settings.ApiMonitoring.CheckIntervalMinutes = GetIntSetting("ApiMonitoring:CheckIntervalMinutes", 3);
                _logger.LogInformation($"API monitoring configured: Enabled={settings.ApiMonitoring.Enabled}, Interval={settings.ApiMonitoring.CheckIntervalMinutes} minutes");

                // Global blog monitoring settings
                settings.BlogMonitoring.Enabled = GetBoolSetting("BlogMonitoring:Enabled", true);
                settings.BlogMonitoring.CheckIntervalMinutes = GetIntSetting("BlogMonitoring:CheckIntervalMinutes", 5);
                _logger.LogInformation($"Blog monitoring configured: Enabled={settings.BlogMonitoring.Enabled}, Interval={settings.BlogMonitoring.CheckIntervalMinutes} minutes");

                // Web scraping settings
                settings.WebScraping.Enabled = GetBoolSetting("WebScraping:Enabled", true);
                LoadWebScrapingPages(settings.WebScraping);
                _logger.LogInformation($"Web scraping configured: Enabled={settings.WebScraping.Enabled}, Pages={settings.WebScraping.Pages.Count}");

                // Coinbase settings
                settings.DataSources.Coinbase.Enabled = GetBoolSetting("DataSources:Coinbase:Enabled", true);
                settings.DataSources.Coinbase.ApiEnabled = GetBoolSetting("DataSources:Coinbase:ApiEnabled", true);
                settings.DataSources.Coinbase.BlogEnabled = GetBoolSetting("DataSources:Coinbase:BlogEnabled", false);
                settings.DataSources.Coinbase.ApiCheckIntervalMinutes = GetIntSetting("DataSources:Coinbase:ApiCheckIntervalMinutes", 5);
                settings.DataSources.Coinbase.BlogCheckIntervalMinutes = GetIntSetting("DataSources:Coinbase:BlogCheckIntervalMinutes", 60);
                settings.DataSources.Coinbase.ApiUrl = GetStringSetting("DataSources:Coinbase:ApiUrl", "https://api.exchange.coinbase.com/products");
                settings.DataSources.Coinbase.BlogUrl = GetStringSetting("DataSources:Coinbase:BlogUrl", "https://blog.coinbase.com");
                settings.DataSources.Coinbase.RoadmapUrl = GetStringSetting("DataSources:Coinbase:RoadmapUrl",
                    "https://www.coinbase.com/blog/increasing-transparency-for-new-asset-listings-on-coinbase");

                // Robinhood settings
                settings.DataSources.Robinhood.Enabled = GetBoolSetting("DataSources:Robinhood:Enabled", false);
                settings.DataSources.Robinhood.ApiEnabled = GetBoolSetting("DataSources:Robinhood:ApiEnabled", false);
                settings.DataSources.Robinhood.BlogEnabled = GetBoolSetting("DataSources:Robinhood:BlogEnabled", false);
                settings.DataSources.Robinhood.ApiCheckIntervalMinutes = GetIntSetting("DataSources:Robinhood:ApiCheckIntervalMinutes", 5);
                settings.DataSources.Robinhood.BlogCheckIntervalMinutes = GetIntSetting("DataSources:Robinhood:BlogCheckIntervalMinutes", 60);
                settings.DataSources.Robinhood.BlogUrl = GetStringSetting("DataSources:Robinhood:BlogUrl", "");
                settings.DataSources.Robinhood.RoadmapUrl = GetStringSetting("DataSources:Robinhood:RoadmapUrl", "");

                // Email notification settings
                settings.Notifications.Email.Enabled = GetBoolSetting("Notifications:Email:Enabled", true);
                settings.Notifications.Email.SenderAddress = GetStringSetting("EmailSenderAddress", "");
                settings.Notifications.Email.RecipientAddress = GetStringSetting("EmailRecipientAddress", "");
                settings.Notifications.Email.ConnectionString = GetStringSetting("AzureCommunicationServicesConnectionString", "");

                // Phone notification settings
                settings.Notifications.Phone.Enabled = GetBoolSetting("Notifications:Phone:Enabled", false);
                settings.Notifications.Phone.FromNumber = GetStringSetting("PhoneFromNumber", "");
                settings.Notifications.Phone.ToNumber = GetStringSetting("PhoneToNumber", "");
                settings.Notifications.Phone.ConnectionString = GetStringSetting("PhoneConnectionString", "");

                // Add Coinbase Roadmap to web scraping pages if not already defined
                if (settings.WebScraping.Pages.Count == 0 && settings.DataSources.Coinbase.Enabled && settings.DataSources.Coinbase.BlogEnabled)
                {
                    settings.WebScraping.Pages.Add(new WebPageConfig
                    {
                        Name = "Coinbase Roadmap",
                        Url = settings.DataSources.Coinbase.RoadmapUrl,
                        Source = "CoinbaseBlog",
                        ParserType = "CoinbaseRoadmap",
                        CheckIntervalMinutes = settings.DataSources.Coinbase.BlogCheckIntervalMinutes,
                        Enabled = true
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading configuration: {Message}", ex.Message);
            }

            return settings;
        }

        private void LoadWebScrapingPages(WebScrapingSettings webScrapingSettings)
        {
            // Look for array-like configuration for pages
            int pageIndex = 0;
            var pages = new List<WebPageConfig>();

            while (true)
            {
                string nameKey = $"WebScraping:Pages:{pageIndex}:Name";
                string name = GetStringSetting(nameKey, null);

                if (string.IsNullOrEmpty(name))
                    break;

                // Found a page configuration
                string urlKey = $"WebScraping:Pages:{pageIndex}:Url";
                string sourceKey = $"WebScraping:Pages:{pageIndex}:Source";
                string parserTypeKey = $"WebScraping:Pages:{pageIndex}:ParserType";
                string intervalKey = $"WebScraping:Pages:{pageIndex}:CheckIntervalMinutes";
                string enabledKey = $"WebScraping:Pages:{pageIndex}:Enabled";

                string url = GetStringSetting(urlKey, "");
                string source = GetStringSetting(sourceKey, "");
                string parserType = GetStringSetting(parserTypeKey, "");
                int interval = GetIntSetting(intervalKey, 5);
                bool enabled = GetBoolSetting(enabledKey, true);

                if (!string.IsNullOrEmpty(url))
                {
                    pages.Add(new WebPageConfig
                    {
                        Name = name,
                        Url = url,
                        Source = source,
                        ParserType = parserType,
                        CheckIntervalMinutes = interval,
                        Enabled = enabled
                    });

                    _logger.LogInformation($"Loaded web page configuration: {name} ({url})");
                }

                pageIndex++;
            }

            webScrapingSettings.Pages = pages;
        }

        private bool GetBoolSetting(string name, bool defaultValue)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return !string.IsNullOrEmpty(value) ? bool.Parse(value) : defaultValue;
        }

        private int GetIntSetting(string name, int defaultValue)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return !string.IsNullOrEmpty(value) ? int.Parse(value) : defaultValue;
        }

        private string GetStringSetting(string name, string defaultValue)
        {
            return Environment.GetEnvironmentVariable(name) ?? defaultValue;
        }
    }
}