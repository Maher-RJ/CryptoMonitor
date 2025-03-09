using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Communication.Email;
using CryptoMonitor.Core.Interfaces.Notification;
using CryptoMonitor.Core.Models.Coinbase;
using CryptoMonitor.Services.DataSources.Api;
using Microsoft.Extensions.Logging;

namespace CryptoMonitor.Services.Notification
{
    public class EmailService : INotificationChannel
    {
        private readonly EmailClient _emailClient;
        private readonly string _senderAddress;
        private readonly string _recipientAddress;
        private readonly ILogger<EmailService> _logger;
        private readonly bool _isEnabled;

        public bool IsEnabled => _isEnabled;

        public EmailService(ILogger<EmailService> logger)
        {
            _logger = logger;

            string connectionString = Environment.GetEnvironmentVariable("AzureCommunicationServicesConnectionString");
            _senderAddress = Environment.GetEnvironmentVariable("EmailSenderAddress");
            _recipientAddress = Environment.GetEnvironmentVariable("EmailRecipientAddress");
            _isEnabled = bool.TryParse(Environment.GetEnvironmentVariable("Notifications:Email:Enabled") ?? "true", out bool enabled) && enabled;

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Azure Communication Services connection string not configured");
                return;
            }

            _emailClient = new EmailClient(connectionString);
        }

        public async Task SendNotificationAsync<T>(string subject, List<T> items, string source)
        {
            if (!_isEnabled)
            {
                _logger.LogInformation("Email notifications are disabled. Skipping email notification.");
                return;
            }

            if (_emailClient == null || string.IsNullOrEmpty(_senderAddress) || string.IsNullOrEmpty(_recipientAddress))
            {
                _logger.LogWarning("Email service not properly configured");
                return;
            }

            try
            {
                StringBuilder bodyBuilder = new StringBuilder();

                bodyBuilder.AppendLine("<html><body>");
                bodyBuilder.AppendLine($"<h2>New Tokens on {source}</h2>");
                bodyBuilder.AppendLine($"<p>The following {items.Count} new tokens were detected on {source}:</p>");
                bodyBuilder.AppendLine("<table border='1' cellpadding='5'>");

                // Special handling for CoinbaseProduct
                if (typeof(T) == typeof(CoinbaseProduct))
                {
                    bodyBuilder.AppendLine("<tr><th>Token</th><th>Trading Pair</th></tr>");

                    foreach (var item in items)
                    {
                        if (item is CoinbaseProduct product)
                        {
                            bodyBuilder.AppendLine($"<tr><td>{product.Display_Name}</td><td>{product.Id}</td></tr>");
                        }
                    }
                }
                else
                {
                    bodyBuilder.AppendLine("<tr><th>Item</th></tr>");

                    foreach (var item in items)
                    {
                        bodyBuilder.AppendLine($"<tr><td>{item}</td></tr>");
                    }
                }

                bodyBuilder.AppendLine("</table>");
                bodyBuilder.AppendLine("<p>These tokens may present trading opportunities.</p>");
                bodyBuilder.AppendLine("</body></html>");

                string htmlContent = bodyBuilder.ToString();

                var emailContent = new EmailContent(subject)
                {
                    Html = htmlContent
                };

                var emailMessage = new EmailMessage(
                    _senderAddress,
                    _recipientAddress,
                    emailContent
                );

                var emailSendOperation = await _emailClient.SendAsync(
                    WaitUntil.Started,
                    emailMessage
                );

                _logger.LogInformation($"Email notification sent: {emailSendOperation.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email notification: {Message}", ex.Message);
            }
        }
    }
}