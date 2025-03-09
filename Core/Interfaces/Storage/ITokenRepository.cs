using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CryptoMonitor.Core.Models.Common;

namespace CryptoMonitor.Core.Interfaces.Storage
{
    public interface ITokenRepository
    {
        Task<List<Token>> GetPreviousTokensAsync(string source);
        Task SaveTokensAsync(List<Token> tokens, string source);
    }
}