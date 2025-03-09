using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CryptoMonitor.Core.Models.Common;

namespace CryptoMonitor.Core.Interfaces.DataSources
{
    public interface ITokenProvider
    {
        Task<List<Token>> GetTokensAsync();
        string SourceName { get; }
    }
}