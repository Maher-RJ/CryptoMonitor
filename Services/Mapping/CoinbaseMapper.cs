using System;
using System.Collections.Generic;
using System.Linq;
using CryptoMonitor.Core.Models.Coinbase;
using CryptoMonitor.Core.Models.Common;

namespace CryptoMonitor.Services.Mapping
{
    public class CoinbaseMapper : IExchangeMapper<CoinbaseProduct>
    {
        public Token MapToToken(CoinbaseProduct product)
        {
            if (product == null)
                return null;

            return new Token
            {
                Id = product.Id,
                Symbol = product.Base_Currency
            };
        }

        public List<Token> MapToTokens(List<CoinbaseProduct> products)
        {
            if (products == null)
                return new List<Token>();

            return products.Select(MapToToken).ToList();
        }

        public CoinbaseProduct MapFromToken(Token token)
        {
            if (token == null)
                return null;

            return new CoinbaseProduct
            {
                Id = token.Id,
                Base_Currency = token.Symbol,
                Display_Name = token.Id
            };
        }

        public List<CoinbaseProduct> MapFromTokens(List<Token> tokens)
        {
            if (tokens == null)
                return new List<CoinbaseProduct>();

            return tokens.Select(MapFromToken).ToList();
        }
    }
}