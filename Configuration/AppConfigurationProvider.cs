using System;
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

                // Coinbase settings
                settings.DataSources.Coinbase.Enabled = GetBoolSetting("DataSources:Coinbase:Enabled", true);
                settings.DataSources.Coinbase.ApiEnabled = GetBoolSetting("DataSources:Coinbase:ApiEnabled", true);
                settings.DataSources.Coinbase.BlogEnabled = GetBoolSetting("DataSources:Coinbase:BlogEnabled", false);
                settings.DataSources.Coinbase.ApiCheckIntervalMinutes = GetIntSetting("DataSources:Coinbase:ApiCheckIntervalMinutes", 5);
                settings.DataSources.Coinbase.BlogCheckIntervalMinutes = GetIntSetting("DataSources:Coinbase:BlogCheckIntervalMinutes", 60);
                settings.DataSources.Coinbase.ApiUrl = GetStringSetting("DataSources:Coinbase:ApiUrl", "https://api.exchange.coinbase.com/products");
                settings.DataSources.Coinbase.BlogUrl = GetStringSetting("DataSources:Coinbase:BlogUrl", "https://blog.coinbase.com");

                // Robinhood settings
                settings.DataSources.Robinhood.Enabled = GetBoolSetting("DataSources:Robinhood:Enabled", false);

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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading configuration: {Message}", ex.Message);
            }

            return settings;
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