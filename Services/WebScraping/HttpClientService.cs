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

            // Add a small random delay before starting to make the request pattern more human-like
            Random random = new Random();
            await Task.Delay(random.Next(500, 1500));

            return await RetryUtility.ExecuteWithRetryAsync(async () =>
            {
                var request = CreateBrowserLikeRequest(url);

                // Handle compression manually if needed
                // request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
                // request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));

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
            }, maxRetryAttempts: maxRetryAttempts, logger: _logger,
            // Custom retry delay function that adds jitter (randomness) to delays
            retryDelayFunc: attempt =>
            {
                int baseDelay = (int)Math.Pow(2, attempt) * 500;
                int jitter = random.Next(-200, 200);  // Add random jitter
                return baseDelay + jitter;
            });
        }

        private HttpRequestMessage CreateBrowserLikeRequest(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            // Randomize User-Agent from a list of common browser user agents
            string[] userAgents = new[]
            {
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:124.0) Gecko/20100101 Firefox/124.0",
                "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36"
            };

            var random = new Random();
            string userAgent = userAgents[random.Next(userAgents.Length)];

            request.Headers.Add("User-Agent", userAgent);

            // Add common browser headers
            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
            request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
            request.Headers.Add("Cache-Control", "max-age=0");

            // Determine the referer based on the domain
            Uri uri = new Uri(url);
            string referer = $"{uri.Scheme}://{uri.Host}/";
            request.Headers.Add("Referer", referer);

            // Add browser-specific headers that make the request look legitimate
            request.Headers.Add("sec-ch-ua", "\"Google Chrome\";v=\"123\", \"Not:A-Brand\";v=\"8\"");
            request.Headers.Add("sec-ch-ua-mobile", "?0");
            request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.Add("sec-fetch-dest", "document");
            request.Headers.Add("sec-fetch-mode", "navigate");
            request.Headers.Add("sec-fetch-site", "same-origin");
            request.Headers.Add("sec-fetch-user", "?1");
            request.Headers.Add("upgrade-insecure-requests", "1");

            // Add a cookie header with a dummy value
            // In a more sophisticated setup, you would maintain and reuse cookies
            request.Headers.Add("Cookie", "visitor_id=anon_" + Guid.NewGuid().ToString("N").Substring(0, 16));

            return request;
        }
    }
}