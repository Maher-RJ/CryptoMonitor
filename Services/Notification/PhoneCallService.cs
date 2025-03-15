using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CryptoMonitor.Core.Interfaces.Notification;
using Microsoft.Extensions.Logging;

namespace CryptoMonitor.Services.Notification
{
    public class PhoneCallService : INotificationChannel
    {
        private readonly ILogger<PhoneCallService> _logger;
        private readonly string _fromNumber;
        private readonly string _toNumber;
        private readonly string _connectionString;
        private readonly bool _isEnabled;

        public bool IsEnabled => _isEnabled;

        public PhoneCallService(ILogger<PhoneCallService> logger)
        {
            _logger = logger;
            _fromNumber = Environment.GetEnvironmentVariable("PhoneFromNumber");
            _toNumber = Environment.GetEnvironmentVariable("PhoneToNumber");
            _connectionString = Environment.GetEnvironmentVariable("PhoneConnectionString");
            _isEnabled = bool.TryParse(Environment.GetEnvironmentVariable("NotificationsPhoneEnabled") ?? "false", out bool enabled) && enabled;
        }

        public async Task SendNotificationAsync<T>(string subject, List<T> items, string source)
        {
            // TODO: Implement phone call notification logic
            _logger.LogInformation("PhoneCallService is not yet implemented");
            await Task.CompletedTask;
        }
    }
}