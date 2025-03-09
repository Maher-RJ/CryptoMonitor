using System;

namespace CryptoMonitor.Core.Models.Common
{
    public class Token
    {
        public string Id { get; set; }
        public string Symbol { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is not Token other)
                return false;

            return Id == other.Id;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }
    }
}