using System;
using System.Collections.Generic;
using System.Linq;
using CryptoMonitor.Core.Models.Common;

namespace CryptoMonitor.Services.Mapping
{
    public class RobinhoodMapper : IExchangeMapper<object>
    {
        public Token MapToToken(object exchangeModel)
        {
            throw new NotImplementedException("Robinhood mapping not yet implemented");
        }

        public List<Token> MapToTokens(List<object> exchangeModels)
        {
            throw new NotImplementedException("Robinhood mapping not yet implemented");
        }

        public object MapFromToken(Token token)
        {
            throw new NotImplementedException("Robinhood mapping not yet implemented");
        }

        public List<object> MapFromTokens(List<Token> tokens)
        {
            throw new NotImplementedException("Robinhood mapping not yet implemented");
        }
    }
}