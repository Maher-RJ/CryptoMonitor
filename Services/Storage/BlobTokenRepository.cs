using Azure;
using Azure.Storage.Blobs;
using CryptoMonitor.Core.Interfaces.Configuration;
using CryptoMonitor.Core.Interfaces.Storage;
using CryptoMonitor.Core.Models.Common;
using CryptoMonitor.Core.Models.Coinbase;
using CryptoMonitor.Services.Mapping;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CryptoMonitor.Services.Storage
{
    public class BlobTokenRepository : ITokenRepository
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<BlobTokenRepository> _logger;
        private readonly IConfigurationProvider _configProvider;
        private const int MaxRetries = 3;

        public BlobTokenRepository(ILogger<BlobTokenRepository> logger, IConfigurationProvider configProvider)
        {
            _logger = logger;
            _configProvider = configProvider;

            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            _blobServiceClient = new BlobServiceClient(connectionString);
        }

        private string GetContainerName()
        {
            var settings = _configProvider.GetSettings();
            string productionContainer = Environment.GetEnvironmentVariable("ProductionContainerName") ?? "crypto-data";
            string testContainer = Environment.GetEnvironmentVariable("TestContainerName") ?? "crypto-data-test";

            string containerName = settings.TestMode ? testContainer : productionContainer;
            _logger.LogInformation($"Using storage container: {containerName} (TestMode={settings.TestMode})");

            return containerName;
        }

        public async Task<List<Token>> GetPreviousTokensAsync(string source)
        {
            int retryCount = 0;

            while (retryCount < MaxRetries)
            {
                try
                {
                    _logger.LogInformation($"Reading previously stored tokens for {source} from Azure Storage...");

                    var containerClient = _blobServiceClient.GetBlobContainerClient(GetContainerName());
                    await containerClient.CreateIfNotExistsAsync();

                    string blobName = GetBlobName(source);
                    var blobClient = containerClient.GetBlobClient(blobName);

                    if (!await blobClient.ExistsAsync())
                    {
                        string legacyBlobName = GetLegacyBlobName(source);
                        var legacyBlobClient = containerClient.GetBlobClient(legacyBlobName);

                        if (await legacyBlobClient.ExistsAsync())
                        {
                            _logger.LogInformation($"Found legacy format blob for {source}, migrating...");
                            return await MigrateLegacyBlobAsync(legacyBlobClient, source);
                        }

                        _logger.LogInformation($"No previous tokens found for {source}");
                        return new List<Token>();
                    }

                    var response = await blobClient.DownloadAsync();
                    using var streamReader = new StreamReader(response.Value.Content);
                    var json = await streamReader.ReadToEndAsync();

                    _logger.LogInformation($"Successfully read blob for {source}, content length: {json.Length} bytes");
                    var tokens = JsonConvert.DeserializeObject<List<Token>>(json);
                    _logger.LogInformation($"Found {tokens?.Count} previously stored tokens for {source}");

                    return tokens ?? new List<Token>();
                }
                catch (RequestFailedException ex) when (ex.Status == 409)
                {
                    retryCount++;
                    _logger.LogWarning($"Storage conflict detected when reading. Retry {retryCount}/{MaxRetries}");

                    if (retryCount >= MaxRetries)
                        throw;

                    int delayMs = (int)Math.Pow(2, retryCount) * 500;
                    await Task.Delay(delayMs);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error reading from Azure Storage for {source}: {ex.Message}");
                    return new List<Token>();
                }
            }

            return new List<Token>();
        }

        private async Task<List<Token>> MigrateLegacyBlobAsync(BlobClient legacyBlobClient, string source)
        {
            try
            {
                var response = await legacyBlobClient.DownloadAsync();
                using var streamReader = new StreamReader(response.Value.Content);
                var json = await streamReader.ReadToEndAsync();

                if (source == "CoinbaseApi")
                {
                    var products = JsonConvert.DeserializeObject<List<CoinbaseProduct>>(json);
                    if (products != null)
                    {
                        var mapper = new CoinbaseMapper();
                        var tokens = mapper.MapToTokens(products);
                        _logger.LogInformation($"Migrated {tokens.Count} legacy Coinbase products to tokens");

                        await SaveTokensAsync(tokens, source);
                        return tokens;
                    }
                }

                return new List<Token>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error migrating legacy blob for {source}: {ex.Message}");
                return new List<Token>();
            }
        }

        public async Task SaveTokensAsync(List<Token> tokens, string source)
        {
            int retryCount = 0;

            while (retryCount < MaxRetries)
            {
                try
                {
                    var containerClient = _blobServiceClient.GetBlobContainerClient(GetContainerName());
                    await containerClient.CreateIfNotExistsAsync();

                    string blobName = GetBlobName(source);
                    var blobClient = containerClient.GetBlobClient(blobName);

                    var json = JsonConvert.SerializeObject(tokens, Formatting.Indented);
                    using var stream = new MemoryStream();
                    using var writer = new StreamWriter(stream);

                    await writer.WriteAsync(json);
                    await writer.FlushAsync();
                    stream.Position = 0;

                    await blobClient.UploadAsync(stream, overwrite: true);
                    _logger.LogInformation($"Successfully saved {tokens.Count} tokens for {source} to blob storage");

                    return;
                }
                catch (RequestFailedException ex) when (ex.Status == 409)
                {
                    retryCount++;
                    _logger.LogWarning($"Storage conflict detected when saving. Retry {retryCount}/{MaxRetries}");

                    if (retryCount >= MaxRetries)
                        throw;

                    int delayMs = (int)Math.Pow(2, retryCount) * 500;
                    await Task.Delay(delayMs);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error saving to Azure Storage for {source}: {ex.Message}");
                    return;
                }
            }
        }

        private string GetBlobName(string source)
        {
            return $"{source.ToLowerInvariant()}-tokens.json";
        }

        private string GetLegacyBlobName(string source)
        {
            return $"{source.ToLowerInvariant()}-products.json";
        }
    }
}