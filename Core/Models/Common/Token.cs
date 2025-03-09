using System;

namespace CryptoMonitor.Core.Models.Common
{
    public class Token
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Symbol { get; set; }
        public string TradingPair { get; set; }
        public string Source { get; set; }
        public DateTime DiscoveredAt { get; set; }
    }
}