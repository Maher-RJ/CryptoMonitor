using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using CryptoMonitor.Core.Interfaces.DataSources;
using CryptoMonitor.Core.Models.Coinbase;
using CryptoMonitor.Utilities.Resilience;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CryptoMonitor.Services.DataSources.Api
{
    public class CoinbaseApiService : ITokenProvider<CoinbaseProduct>
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CoinbaseApiService> _logger;
        private const string CoinbaseProductsUrl = "https://api.exchange.coinbase.com/products";

        public string SourceName => "CoinbaseApi";

        public CoinbaseApiService(HttpClient httpClient, ILogger<CoinbaseApiService> logger)
        {
            _httpClient = httpClient ?? new HttpClient();
            _logger = logger;
        }

        public async Task<List<CoinbaseProduct>> GetTokensAsync()
        {
            _logger.LogInformation("Fetching products from Coinbase API...");

            return await RetryUtility.ExecuteWithRetryAsync(async () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, CoinbaseProductsUrl);
                var response = await _httpClient.SendWithRetryAsync(request, logger: _logger);

                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                try
                {
                    var products = JsonConvert.DeserializeObject<List<CoinbaseProduct>>(content);
                    _logger.LogInformation($"Found {products.Count} products on Coinbase");
                    return products;
                }
                catch
                {
                    var apiResponse = JsonConvert.DeserializeObject<CoinbaseApiResponse>(content);
                    var products = apiResponse?.Data ?? new List<CoinbaseProduct>();
                    _logger.LogInformation($"Found {products.Count} products on Coinbase (from wrapped response)");
                    return products;
                }
            }, logger: _logger);
        }
    }
}