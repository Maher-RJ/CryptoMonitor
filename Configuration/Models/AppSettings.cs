using System;

namespace CryptoMonitor.Configuration.Models
{
    public class AppSettings
    {
        public DataSourceSettings DataSources { get; set; } = new DataSourceSettings();
        public NotificationSettings Notifications { get; set; } = new NotificationSettings();
    }

    public class DataSourceSettings
    {
        public CoinbaseSettings Coinbase { get; set; } = new CoinbaseSettings();
        public RobinhoodSettings Robinhood { get; set; } = new RobinhoodSettings();
    }

    public class CoinbaseSettings
    {
        public bool Enabled { get; set; } = true;
        public bool ApiEnabled { get; set; } = true;
        public bool BlogEnabled { get; set; } = false;
        public int ApiCheckIntervalMinutes { get; set; } = 5;
        public int BlogCheckIntervalMinutes { get; set; } = 60;
        public string ApiUrl { get; set; } = "https://api.exchange.coinbase.com/products";
        public string BlogUrl { get; set; } = "https://blog.coinbase.com";
    }

    public class RobinhoodSettings
    {
        public bool Enabled { get; set; } = false;
        public bool ApiEnabled { get; set; } = false;
        public bool BlogEnabled { get; set; } = false;
        public int ApiCheckIntervalMinutes { get; set; } = 5;
        public int BlogCheckIntervalMinutes { get; set; } = 60;
    }
}