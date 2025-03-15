using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using CryptoMonitor.Utilities.Resilience;
using Microsoft.Extensions.Logging;

namespace CryptoMonitor.Services.WebScraping
{
    public class HttpClientService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HttpClientService> _logger;

        public HttpClientService(HttpClient httpClient, ILogger<HttpClientService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<string> GetHtmlContentAsync(string url, int maxRetryAttempts = 5)
        {
            _logger.LogInformation($"Fetching content from URL: {url}");

            return await RetryUtility.ExecuteWithRetryAsync(async () =>
            {
                var request = CreateBrowserLikeRequest(url);
                var response = await _httpClient.SendWithRetryAsync(request, maxRetryAttempts: maxRetryAttempts, logger: _logger);

                _logger.LogInformation($"Received response with status: {response.StatusCode}");

                // Handle 403 Forbidden errors specifically
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning("Website is blocking our request with 403 Forbidden. This may indicate anti-scraping measures.");
                    throw new HttpRequestException($"Website is blocking our request: {response.StatusCode}");
                }

                response.EnsureSuccessStatusCode();
                string htmlContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation($"Successfully fetched HTML content (length: {htmlContent.Length} bytes)");

                return htmlContent;
            }, maxRetryAttempts: maxRetryAttempts, logger: _logger);
        }

        private HttpRequestMessage CreateBrowserLikeRequest(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            // Set standard browser-like headers
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");

            // Determine the referer based on the domain
            Uri uri = new Uri(url);
            string referer = $"{uri.Scheme}://{uri.Host}/";
            request.Headers.Add("Referer", referer);

            // Additional standard headers
            request.Headers.Add("DNT", "1");
            request.Headers.Add("Connection", "keep-alive");
            request.Headers.Add("Sec-Fetch-Dest", "document");
            request.Headers.Add("Sec-Fetch-Mode", "navigate");
            request.Headers.Add("Sec-Fetch-Site", "same-origin");

            return request;
        }
    }
}