using System;

namespace CryptoMonitor.Core.Models.Coinbase
{
    public class CoinbaseProduct
    {
        public string Id { get; set; }
        public string Base_Currency { get; set; }
        public string Quote_Currency { get; set; }
        public string Display_Name { get; set; }
    }
}