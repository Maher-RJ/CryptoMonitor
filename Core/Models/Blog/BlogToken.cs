using System;

namespace CryptoMonitor.Core.Models.Blog
{
    public class BlogToken
    {
        public string Name { get; set; }
        public string Symbol { get; set; }
        public string ContractAddress { get; set; }
        public string Network { get; set; }
        public DateTime DiscoveredAt { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is not BlogToken other)
                return false;

            return Symbol == other.Symbol &&
                   ContractAddress == other.ContractAddress &&
                   Network == other.Network;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Symbol, ContractAddress, Network);
        }
    }
}