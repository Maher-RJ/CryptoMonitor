using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using CryptoMonitor.Core.Interfaces.Storage;
using CryptoMonitor.Core.Models.Coinbase;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CryptoMonitor.Services.Storage
{
    public class BlobTokenRepository : ITokenRepository<CoinbaseProduct>
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<BlobTokenRepository> _logger;
        private const string BlobContainerName = "crypto-data";

        public BlobTokenRepository(ILogger<BlobTokenRepository> logger)
        {
            _logger = logger;

            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            _blobServiceClient = new BlobServiceClient(connectionString);
        }

        public async Task<List<CoinbaseProduct>> GetPreviousTokensAsync(string source)
        {
            try
            {
                _logger.LogInformation($"Reading previously stored tokens for {source} from Azure Storage...");

                // Make sure the container exists
                var containerClient = _blobServiceClient.GetBlobContainerClient(BlobContainerName);
                await containerClient.CreateIfNotExistsAsync();

                // Get the blob client
                string blobName = GetBlobName(source);
                var blobClient = containerClient.GetBlobClient(blobName);

                if (!await blobClient.ExistsAsync())
                {
                    _logger.LogInformation($"No previous tokens found for {source}");
                    return new List<CoinbaseProduct>();
                }

                // Download and parse the blob
                var response = await blobClient.DownloadAsync();
                using var streamReader = new StreamReader(response.Value.Content);
                var json = await streamReader.ReadToEndAsync();

                _logger.LogInformation($"Successfully read blob for {source}, content length: {json.Length} bytes");
                var tokens = JsonConvert.DeserializeObject<List<CoinbaseProduct>>(json);
                _logger.LogInformation($"Found {tokens.Count} previously stored tokens for {source}");

                return tokens ?? new List<CoinbaseProduct>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reading from Azure Storage for {source}: {ex.Message}");
                return new List<CoinbaseProduct>();
            }
        }

        public async Task SaveTokensAsync(List<CoinbaseProduct> tokens, string source)
        {
            try
            {
                // Make sure the container exists
                var containerClient = _blobServiceClient.GetBlobContainerClient(BlobContainerName);
                await containerClient.CreateIfNotExistsAsync();

                // Get the blob client
                string blobName = GetBlobName(source);
                var blobClient = containerClient.GetBlobClient(blobName);

                // Serialize and upload
                var json = JsonConvert.SerializeObject(tokens, Formatting.Indented);
                using var stream = new MemoryStream();
                using var writer = new StreamWriter(stream);

                await writer.WriteAsync(json);
                await writer.FlushAsync();
                stream.Position = 0;

                await blobClient.UploadAsync(stream, overwrite: true);
                _logger.LogInformation($"Successfully saved {tokens.Count} tokens for {source} to blob storage");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving to Azure Storage for {source}: {ex.Message}");
            }
        }

        private string GetBlobName(string source)
        {
            return $"{source.ToLowerInvariant()}-products.json";
        }
    }
}