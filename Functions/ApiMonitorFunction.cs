using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using CryptoMonitor.Core.Interfaces.Configuration;
using CryptoMonitor.Core.Interfaces.DataSources;
using CryptoMonitor.Core.Models.Common;
using CryptoMonitor.Core.Interfaces.Storage;
using CryptoMonitor.Services.Notification;
using CryptoMonitor.Utilities.Comparison;

namespace CryptoMonitor
{
    public class ApiMonitorFunction
    {
        private readonly ITokenProvider _tokenProvider;
        private readonly ITokenRepository _tokenRepository;
        private readonly IConfigurationProvider _configProvider;
        private readonly NotificationFactory _notificationFactory;
        private readonly ILogger<ApiMonitorFunction> _logger;

        public ApiMonitorFunction(
            ITokenProvider tokenProvider,
            ITokenRepository tokenRepository,
            IConfigurationProvider configProvider,
            NotificationFactory notificationFactory,
            ILoggerFactory loggerFactory)
        {
            _tokenProvider = tokenProvider;
            _tokenRepository = tokenRepository;
            _configProvider = configProvider;
            _notificationFactory = notificationFactory;
            _logger = loggerFactory.CreateLogger<ApiMonitorFunction>();
        }

        [Function("ApiMonitorFunction")]
        public async Task Run([TimerTrigger("%ApiSchedule%")] TimerInfo timer)
        {
            var settings = _configProvider.GetSettings();
            string modeStatus = settings.TestMode ? "TEST MODE" : "PRODUCTION MODE";
            _logger.LogInformation($"Crypto API monitoring function executed at: {DateTime.Now} [{modeStatus}]");

            // First check global API monitoring switch
            if (!settings.ApiMonitoring.Enabled)
            {
                _logger.LogInformation("API monitoring is globally disabled. Skipping check.");
                return;
            }

            if (settings.TestMode)
            {
                _logger.LogInformation("Running in TEST MODE with test storage container");
            }

            try
            {
                // Next check exchange-specific and API-specific switches
                if (!settings.DataSources.Coinbase.Enabled)
                {
                    _logger.LogInformation("Coinbase monitoring is disabled. Skipping check.");
                    return;
                }

                if (!settings.DataSources.Coinbase.ApiEnabled)
                {
                    _logger.LogInformation("Coinbase API monitoring is disabled. Skipping check.");
                    return;
                }

                var currentTokens = await _tokenProvider.GetTokensAsync();
                var previousTokens = await _tokenRepository.GetPreviousTokensAsync(_tokenProvider.SourceName);

                var newTokens = TokenComparisonUtility.FindNewTokens(
                    currentTokens,
                    previousTokens,
                    token => token.Id);

                _logger.LogInformation($"Found {newTokens.Count} new tokens on {_tokenProvider.SourceName}");

                if (newTokens.Count > 0)
                {
                    _logger.LogInformation("New tokens found! Details:");
                    var displayTokens = newTokens.Take(3).ToList();
                    foreach (var token in displayTokens)
                    {
                        _logger.LogInformation($"New token: {token.Symbol} ({token.Id})");
                    }

                    if (newTokens.Count > 3)
                    {
                        _logger.LogInformation($"...and {newTokens.Count - 3} more new tokens");
                    }

                    string subject = $"New tokens on {_tokenProvider.SourceName}! ({newTokens.Count})";
                    var notificationChannels = _notificationFactory.GetEnabledChannels();

                    foreach (var channel in notificationChannels)
                    {
                        await channel.SendNotificationAsync(subject, newTokens, _tokenProvider.SourceName);
                    }

                    await _tokenRepository.SaveTokensAsync(currentTokens, _tokenProvider.SourceName);

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