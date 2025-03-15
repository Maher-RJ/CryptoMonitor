using System;
using System.Collections.Generic;
using CryptoMonitor.Configuration;
using CryptoMonitor.Core.Interfaces.Configuration;
using CryptoMonitor.Core.Interfaces.Notification;
using Microsoft.Extensions.Logging;

namespace CryptoMonitor.Services.Notification
{
    public class NotificationFactory
    {
        private readonly IConfigurationProvider _configProvider;
        private readonly ILogger<NotificationFactory> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public NotificationFactory(
            IConfigurationProvider configProvider,
            ILogger<NotificationFactory> logger,
            ILoggerFactory loggerFactory)
        {
            _configProvider = configProvider;
            _logger = logger;
            _loggerFactory = loggerFactory;
        }

        public IEnumerable<INotificationChannel> GetEnabledChannels()
        {
            var settings = _configProvider.GetSettings();

            if (settings.Notifications.Email.Enabled)
            {
                // Create the correct logger type for EmailService
                var emailLogger = _loggerFactory.CreateLogger<EmailService>();
                yield return new EmailService(emailLogger);
            }

            // Phone notifications are not yet implemented
        }
    }
}