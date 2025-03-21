﻿using System;

namespace CryptoMonitor.Configuration.Models
{
    public class AppSettings
    {
        public DataSourceSettings DataSources { get; set; } = new DataSourceSettings();
        public NotificationSettings Notifications { get; set; } = new NotificationSettings();
        public bool TestMode { get; set; } = false;
        public BlogMonitoringSettings BlogMonitoring { get; set; } = new BlogMonitoringSettings();
        public ApiMonitoringSettings ApiMonitoring { get; set; } = new ApiMonitoringSettings();
        public WebScrapingSettings WebScraping { get; set; } = new WebScrapingSettings();
    }

    public class ApiMonitoringSettings
    {
        public bool Enabled { get; set; } = true;
        public int CheckIntervalMinutes { get; set; } = 3;
    }

    public class BlogMonitoringSettings
    {
        public bool Enabled { get; set; } = true;
        public int CheckIntervalMinutes { get; set; } = 5;
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
        public string RoadmapUrl { get; set; } = "https://www.coinbase.com/blog/increasing-transparency-for-new-asset-listings-on-coinbase";
    }

    public class RobinhoodSettings
    {
        public bool Enabled { get; set; } = false;
        public bool ApiEnabled { get; set; } = false;
        public bool BlogEnabled { get; set; } = false;
        public int ApiCheckIntervalMinutes { get; set; } = 5;
        public int BlogCheckIntervalMinutes { get; set; } = 60;
        public string BlogUrl { get; set; } = "";
        public string RoadmapUrl { get; set; } = "";
    }
}