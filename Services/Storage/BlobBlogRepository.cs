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

        public async Task<string> GetPreviousHtmlAsync(string source)
        {
            int retryCount = 0;

            while (retryCount < MaxRetries)
            {
                try
                {
                    _logger.LogInformation($"Reading previously stored HTML for {source} from Azure Storage...");

                    var containerClient = _blobServiceClient.GetBlobContainerClient(GetContainerName());
                    await containerClient.CreateIfNotExistsAsync();

                    string blobName = GetHtmlBlobName(source);
                    var blobClient = containerClient.GetBlobClient(blobName);

                    if (!await blobClient.ExistsAsync())
                    {
                        _logger.LogInformation($"No previous HTML found for {source}");
                        return string.Empty;
                    }

                    var response = await blobClient.DownloadAsync();
                    using var streamReader = new StreamReader(response.Value.Content);
                    var html = await streamReader.ReadToEndAsync();

                    _logger.LogInformation($"Successfully read HTML for {source}, content length: {html.Length} bytes");

                    return html;
                }
                catch (RequestFailedException ex) when (ex.Status == 409)
                {
                    retryCount++;
                    _logger.LogWarning($"Storage conflict detected when reading HTML. Retry {retryCount}/{MaxRetries}");

                    if (retryCount >= MaxRetries)
                        throw;

                    int delayMs = (int)Math.Pow(2, retryCount) * 500;
                    await Task.Delay(delayMs);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error reading HTML from Azure Storage for {source}: {ex.Message}");
                    return string.Empty;
                }
            }

            return string.Empty;
        }

        public async Task SaveHtmlAsync(string htmlContent, string source)
        {
            int retryCount = 0;

            while (retryCount < MaxRetries)
            {
                try
                {
                    var containerClient = _blobServiceClient.GetBlobContainerClient(GetContainerName());
                    await containerClient.CreateIfNotExistsAsync();

                    string blobName = GetHtmlBlobName(source);
                    var blobClient = containerClient.GetBlobClient(blobName);

                    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(htmlContent));
                    await blobClient.UploadAsync(stream, overwrite: true);

                    _logger.LogInformation($"Successfully saved HTML for {source} to blob storage (length: {htmlContent.Length} bytes)");

                    return;
                }
                catch (RequestFailedException ex) when (ex.Status == 409)
                {
                    retryCount++;
                    _logger.LogWarning($"Storage conflict detected when saving HTML. Retry {retryCount}/{MaxRetries}");

                    if (retryCount >= MaxRetries)
                        throw;

                    int delayMs = (int)Math.Pow(2, retryCount) * 500;
                    await Task.Delay(delayMs);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error saving HTML to Azure Storage for {source}: {ex.Message}");
                    return;
                }
            }
        }

        public async Task<(List<BlogToken> NewTokens, bool HtmlChanged)> GetChangesAsync(
            List<BlogToken> currentTokens,
            string currentHtml,
            string source)
        {
            var previousTokens = await GetPreviousTokensAsync(source);
            var previousHtml = await GetPreviousHtmlAsync(source);

            // Find new tokens by comparison
            var newTokens = FindNewTokens(currentTokens, previousTokens);

            // Simple HTML change detection
            bool htmlChanged = false;
            if (!string.IsNullOrEmpty(previousHtml) && !string.IsNullOrEmpty(currentHtml))
            {
                // Compare HTML length as simple change detection
                // More sophisticated diff could be implemented
                int lengthDiff = Math.Abs(currentHtml.Length - previousHtml.Length);

                // If length changed by more than 0.5%, consider it changed
                htmlChanged = lengthDiff > (currentHtml.Length * 0.005);

                // Log the difference
                if (htmlChanged)
                {
                    _logger.LogInformation($"HTML content changed for {source}. Size difference: {lengthDiff} bytes");
                }
            }
            else if (string.IsNullOrEmpty(previousHtml) && !string.IsNullOrEmpty(currentHtml))
            {
                // First time we're getting HTML
                htmlChanged = true;
            }

            return (newTokens, htmlChanged);
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

        private string GetTokensBlobName(string source)
        {
            return $"{source.ToLowerInvariant()}-blog-tokens.json";
        }

        private string GetHtmlBlobName(string source)
        {
            return $"{source.ToLowerInvariant()}-blog-latest.html";
        }
    }
}