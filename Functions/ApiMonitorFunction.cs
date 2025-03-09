using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using CryptoMonitor.Configuration;
using CryptoMonitor.Core.Interfaces.Configuration;
using CryptoMonitor.Core.Interfaces.DataSources;
using CryptoMonitor.Core.Models.Coinbase;
using CryptoMonitor.Core.Interfaces.Storage;
using CryptoMonitor.Services.Notification;
using CryptoMonitor.Utilities.Comparison;

namespace CryptoMonitor
{
    public class ApiMonitorFunction
    {
        private readonly ITokenProvider<CoinbaseProduct> _coinbaseService;
        private readonly ITokenRepository<CoinbaseProduct> _tokenRepository;
        private readonly IConfigurationProvider _configProvider;
        private readonly NotificationFactory _notificationFactory;
        private readonly ILogger<ApiMonitorFunction> _logger;

        public ApiMonitorFunction(
            ITokenProvider<CoinbaseProduct> coinbaseService,
            ITokenRepository<CoinbaseProduct> tokenRepository,
            IConfigurationProvider configProvider,
            NotificationFactory notificationFactory,
            ILoggerFactory loggerFactory)
        {
            _coinbaseService = coinbaseService;
            _tokenRepository = tokenRepository;
            _configProvider = configProvider;
            _notificationFactory = notificationFactory;
            _logger = loggerFactory.CreateLogger<ApiMonitorFunction>();
        }

        [Function("ApiMonitorFunction")]
        public async Task Run([TimerTrigger("0 */3 * * * *")] TimerInfo timer)
        {
            _logger.LogInformation($"Crypto API monitoring function executed at: {DateTime.Now}");

            try
            {
                var settings = _configProvider.GetSettings();

                if (!settings.DataSources.Coinbase.Enabled || !settings.DataSources.Coinbase.ApiEnabled)
                {
                    _logger.LogInformation("Coinbase API monitoring is disabled. Skipping check.");
                    return;
                }

                // Get current tokens
                var currentTokens = await _coinbaseService.GetTokensAsync();

                // Get previous tokens
                var previousTokens = await _tokenRepository.GetPreviousTokensAsync(_coinbaseService.SourceName);

                // Find new tokens
                var newTokens = TokenComparisonUtility.FindNewTokens(
                    currentTokens,
                    previousTokens,
                    token => token.Id);

                _logger.LogInformation($"Found {newTokens.Count} new tokens on {_coinbaseService.SourceName}");

                // If new tokens found, send notification and update storage
                if (newTokens.Count > 0)
                {
                    _logger.LogInformation("New tokens found! Details:");
                    var displayTokens = newTokens.Take(3).ToList();
                    foreach (var token in displayTokens)
                    {
                        _logger.LogInformation($"New token: {token.Display_Name} ({token.Id})");
                    }

                    if (newTokens.Count > 3)
                    {
                        _logger.LogInformation($"...and {newTokens.Count - 3} more new tokens");
                    }

                    // Get notification channels and send notifications
                    string subject = $"New tokens on {_coinbaseService.SourceName}! ({newTokens.Count})";
                    var notificationChannels = _notificationFactory.GetEnabledChannels();

                    foreach (var channel in notificationChannels)
                    {
                        await channel.SendNotificationAsync(subject, newTokens, _coinbaseService.SourceName);
                    }

                    // Save current tokens
                    await _tokenRepository.SaveTokensAsync(currentTokens, _coinbaseService.SourceName);

                    _logger.LogInformation("Notifications sent and storage updated");
                }
                else
                {
                    _logger.LogInformation("No new tokens found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while monitoring APIs: {Message}", ex.Message);
            }
        }
    }
}