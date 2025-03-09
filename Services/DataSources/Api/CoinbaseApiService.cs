using System.Net.Http.Headers;
using CryptoMonitor.Core.Interfaces.DataSources;
using CryptoMonitor.Core.Models.Coinbase;
using CryptoMonitor.Core.Models.Common;
using CryptoMonitor.Services.Mapping;
using CryptoMonitor.Utilities.Resilience;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CryptoMonitor.Services.DataSources.Api
{
    public class CoinbaseApiService : ITokenProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CoinbaseApiService> _logger;
        private readonly IExchangeMapper<CoinbaseProduct> _mapper;
        private const string CoinbaseProductsUrl = "https://api.exchange.coinbase.com/products";

        public string SourceName => "CoinbaseApi";
        public TokenSource TokenSource => TokenSource.CoinbaseApi;

        public CoinbaseApiService(
            HttpClient httpClient,
            ILogger<CoinbaseApiService> logger,
            IExchangeMapper<CoinbaseProduct> mapper)
        {
            _httpClient = httpClient ?? new HttpClient();
            _logger = logger;
            _mapper = mapper;

            _httpClient.DefaultRequestHeaders.Add("User-Agent", "CryptoMonitor/1.0");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<List<Token>> GetTokensAsync()
        {
            _logger.LogInformation("Fetching products from Coinbase API...");

            return await RetryUtility.ExecuteWithRetryAsync(async () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, CoinbaseProductsUrl);
                var response = await _httpClient.SendWithRetryAsync(request, logger: _logger);

                _logger.LogInformation($"Received response with status: {response.StatusCode}");

                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                List<CoinbaseProduct> products;
                try
                {
                    products = JsonConvert.DeserializeObject<List<CoinbaseProduct>>(content);
                    _logger.LogInformation($"Found {products.Count} products on Coinbase");
                }
                catch
                {
                    _logger.LogInformation("Attempting to parse as wrapped response");
                    var apiResponse = JsonConvert.DeserializeObject<CoinbaseApiResponse>(content);
                    products = apiResponse?.Data ?? new List<CoinbaseProduct>();
                    _logger.LogInformation($"Found {products.Count} products on Coinbase (from wrapped response)");
                }

                var tokens = _mapper.MapToTokens(products);
                _logger.LogInformation($"Mapped {tokens.Count} Coinbase products to tokens");

                return tokens;
            }, logger: _logger);
        }
    }
}