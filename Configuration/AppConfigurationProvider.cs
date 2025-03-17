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
                settings.ApiMonitoring.Enabled = GetBoolSetting("ApiMonitoringEnabled", true);
                _logger.LogInformation($"API monitoring configured: Enabled={settings.ApiMonitoring.Enabled}");

                // Web scraping settings
                settings.WebScraping.Enabled = GetBoolSetting("WebScrapingEnabled", true);
                LoadWebScrapingPages(settings.WebScraping);
                _logger.LogInformation($"Web scraping configured: Enabled={settings.WebScraping.Enabled}, Pages={settings.WebScraping.Pages.Count}");

                // Coinbase settings
                settings.DataSources.Coinbase.Enabled = GetBoolSetting("DataSourcesCoinbaseEnabled", true);
                settings.DataSources.Coinbase.ApiEnabled = GetBoolSetting("DataSourcesCoinbaseApiEnabled", true);
                settings.DataSources.Coinbase.BlogEnabled = GetBoolSetting("DataSourcesCoinbaseBlogEnabled", false);
                settings.DataSources.Coinbase.ApiUrl = GetStringSetting("DataSourcesCoinbaseApiUrl", "https://api.exchange.coinbase.com/products");
                settings.DataSources.Coinbase.BlogUrl = GetStringSetting("DataSourcesCoinbaseBlogUrl", "https://blog.coinbase.com");
                settings.DataSources.Coinbase.RoadmapUrl = GetStringSetting("DataSourcesCoinbaseRoadmapUrl",
                    "https://www.coinbase.com/blog/increasing-transparency-for-new-asset-listings-on-coinbase");

                // Robinhood settings
                settings.DataSources.Robinhood.Enabled = GetBoolSetting("DataSourcesRobinhoodEnabled", false);
                settings.DataSources.Robinhood.ApiEnabled = GetBoolSetting("DataSourcesRobinhoodApiEnabled", false);
                settings.DataSources.Robinhood.BlogEnabled = GetBoolSetting("DataSourcesRobinhoodBlogEnabled", false);
                settings.DataSources.Robinhood.BlogUrl = GetStringSetting("DataSourcesRobinhoodBlogUrl", "");
                settings.DataSources.Robinhood.RoadmapUrl = GetStringSetting("DataSourcesRobinhoodRoadmapUrl", "");

                // Email notification settings
                settings.Notifications.Email.Enabled = GetBoolSetting("NotificationsEmailEnabled", true);
                settings.Notifications.Email.SenderAddress = GetStringSetting("EmailSenderAddress", "");
                settings.Notifications.Email.RecipientAddress = GetStringSetting("EmailRecipientAddress", "");
                settings.Notifications.Email.ConnectionString = GetStringSetting("AzureCommunicationServicesConnectionString", "");

                // Phone notification settings
                settings.Notifications.Phone.Enabled = GetBoolSetting("NotificationsPhoneEnabled", false);
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
                string nameKey = $"WebScrapingPage{pageIndex}Name";
                string name = GetStringSetting(nameKey, null);

                if (string.IsNullOrEmpty(name))
                    break;

                // Found a page configuration
                string urlKey = $"WebScrapingPage{pageIndex}Url";
                string sourceKey = $"WebScrapingPage{pageIndex}Source";
                string parserTypeKey = $"WebScrapingPage{pageIndex}ParserType";
                string enabledKey = $"WebScrapingPage{pageIndex}Enabled";

                string url = GetStringSetting(urlKey, "");
                string source = GetStringSetting(sourceKey, "");
                string parserType = GetStringSetting(parserTypeKey, "");
                bool enabled = GetBoolSetting(enabledKey, true);

                if (!string.IsNullOrEmpty(url))
                {
                    pages.Add(new WebPageConfig
                    {
                        Name = name,
                        Url = url,
                        Source = source,
                        ParserType = parserType,
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