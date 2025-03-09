using System;

namespace CryptoMonitor.Configuration.Models
{
    public class NotificationSettings
    {
        public EmailSettings Email { get; set; } = new EmailSettings();
        public PhoneSettings Phone { get; set; } = new PhoneSettings();
    }

    public class EmailSettings
    {
        public bool Enabled { get; set; } = true;
        public string SenderAddress { get; set; } = "";
        public string RecipientAddress { get; set; } = "";
        public string ConnectionString { get; set; } = "";
    }

    public class PhoneSettings
    {
        public bool Enabled { get; set; } = false;
        public string FromNumber { get; set; } = "";
        public string ToNumber { get; set; } = "";
        public string ConnectionString { get; set; } = "";
    }
}