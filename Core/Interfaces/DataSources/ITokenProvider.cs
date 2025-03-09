using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CryptoMonitor.Core.Interfaces.DataSources
{
    public interface ITokenProvider<T>
    {
        Task<List<T>> GetTokensAsync();
        string SourceName { get; }
    }
}