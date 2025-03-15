using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using CryptoMonitor.Core.Interfaces.Configuration;
using CryptoMonitor.Core.Interfaces.Storage;
using CryptoMonitor.Core.Models.Blog;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CryptoMonitor.Services.Storage
{
    public class BlobBlogRepository : IBlogRepository
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<BlobBlogRepository> _logger;
        private readonly IConfigurationProvider _configProvider;
        private const int MaxRetries = 3;

        public BlobBlogRepository(
            ILogger<BlobBlogRepository> logger,
            IConfigurationProvider configProvider)
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

        public async Task<List<BlogToken>> GetPreviousTokensAsync(string source)
        {
            int retryCount = 0;

            while (retryCount < MaxRetries)
            {
                try
                {
                    _logger.LogInformation($"Reading previously stored blog tokens for {source} from Azure Storage...");

                    var containerClient = _blobServiceClient.GetBlobContainerClient(GetContainerName());
                    await containerClient.CreateIfNotExistsAsync();

                    string blobName = GetTokensBlobName(source);
                    var blobClient = containerClient.GetBlobClient(blobName);

                    if (!await blobClient.ExistsAsync())
                    {
                        _logger.LogInformation($"No previous blog tokens found for {source}");
                        return new List<BlogToken>();
                    }

                    var response = await blobClient.DownloadAsync();
                    using var streamReader = new StreamReader(response.Value.Content);
                    var json = await streamReader.ReadToEndAsync();

                    _logger.LogInformation($"Successfully read blog tokens for {source}, content length: {json.Length} bytes");
                    var tokens = JsonConvert.DeserializeObject<List<BlogToken>>(json);
                    _logger.LogInformation($"Found {tokens?.Count} previously stored blog tokens for {source}");

                    return tokens ?? new List<BlogToken>();
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
                    _logger.LogError(ex, $"Error reading blog tokens from Azure Storage for {source}: {ex.Message}");
                    return new List<BlogToken>();
                }
            }

            return new List<BlogToken>();
        }

        public async Task SaveTokensAsync(List<BlogToken> tokens, string source)
        {
            int retryCount = 0;

            while (retryCount < MaxRetries)
            {
                try
                {
                    var containerClient = _blobServiceClient.GetBlobContainerClient(GetContainerName());
                    await containerClient.CreateIfNotExistsAsync();

                    string blobName = GetTokensBlobName(source);
                    var blobClient = containerClient.GetBlobClient(blobName);

                    var json = JsonConvert.SerializeObject(tokens, Formatting.Indented);
                    using var stream = new MemoryStream();
                    using var writer = new StreamWriter(stream);

                    await writer.WriteAsync(json);
                    await writer.FlushAsync();
                    stream.Position = 0;

                    await blobClient.UploadAsync(stream, overwrite: true);
                    _logger.LogInformation($"Successfully saved {tokens.Count} blog tokens for {source} to blob storage");

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
                    _logger.LogError(ex, $"Error saving blog tokens to Azure Storage for {source}: {ex.Message}");
                    return;
                }
            }
        }

        public async Task<(List<BlogToken> NewTokens, List<BlogToken> RemovedTokens)> GetChangesAsync(
            List<BlogToken> currentTokens,
            string source)
        {
            var previousTokens = await GetPreviousTokensAsync(source);

            // First run detection
            bool isFirstRun = previousTokens.Count == 0;

            // Find new tokens by comparison
            var newTokens = FindNewTokens(currentTokens, previousTokens);

            // Find removed tokens (tokens that were in previous list but not in current)
            var removedTokens = FindRemovedTokens(currentTokens, previousTokens);

            // Log token changes
            if (newTokens.Count > 0)
            {
                _logger.LogInformation($"Found {newTokens.Count} new tokens for {source}");
            }

            if (removedTokens.Count > 0)
            {
                _logger.LogInformation($"Found {removedTokens.Count} tokens removed from {source} (potentially listed)");
            }

            // On first run, if we have current tokens, consider them all as "new" for notification purposes
            if (isFirstRun && currentTokens.Count > 0 && newTokens.Count == 0)
            {
                _logger.LogInformation($"First run detected for {source}. Treating {currentTokens.Count} tokens as new for notification purposes.");
                return (currentTokens, new List<BlogToken>());
            }

            return (newTokens, removedTokens);
        }

        private List<BlogToken> FindNewTokens(List<BlogToken> currentTokens, List<BlogToken> previousTokens)
        {
            return currentTokens
                .Where(current => !previousTokens.Any(previous =>
                    previous.Symbol == current.Symbol &&
                    previous.Network == current.Network &&
                    previous.ContractAddress == current.ContractAddress))
                .ToList();
        }

        private List<BlogToken> FindRemovedTokens(List<BlogToken> currentTokens, List<BlogToken> previousTokens)
        {
            return previousTokens
                .Where(previous => !currentTokens.Any(current =>
                    current.Symbol == previous.Symbol &&
                    current.Network == previous.Network &&
                    current.ContractAddress == previous.ContractAddress))
                .ToList();
        }

        private string GetTokensBlobName(string source)
        {
            return $"{source.ToLowerInvariant()}-blog-tokens.json";
        }
    }
}