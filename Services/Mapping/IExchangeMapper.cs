using System.Collections.Generic;
using CryptoMonitor.Core.Models.Common;

namespace CryptoMonitor.Services.Mapping
{
    public interface IExchangeMapper<TExchangeModel>
    {
        Token MapToToken(TExchangeModel exchangeModel);
        List<Token> MapToTokens(List<TExchangeModel> exchangeModels);

        TExchangeModel MapFromToken(Token token);
        List<TExchangeModel> MapFromTokens(List<Token> tokens);
    }
}