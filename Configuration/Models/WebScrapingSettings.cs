using System;
using System.Collections.Generic;

namespace CryptoMonitor.Configuration.Models
{
    public class WebScrapingSettings
    {
        public bool Enabled { get; set; } = true;
        public List<WebPageConfig> Pages { get; set; } = new List<WebPageConfig>();
    }

    public class WebPageConfig
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string Source { get; set; }
        public string ParserType { get; set; }
        public int CheckIntervalMinutes { get; set; } = 5;
        public bool Enabled { get; set; } = true;
    }
}