using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CryptoMonitor.Core.Interfaces.Storage
{
    public interface ITokenRepository<T>
    {
        Task<List<T>> GetPreviousTokensAsync(string source);
        Task SaveTokensAsync(List<T> tokens, string source);
    }
}