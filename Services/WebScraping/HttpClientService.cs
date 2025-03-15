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
            _logger = logger;

            // Configure handler with proxy support
            var handler = new HttpClientHandler
            {
                // Configure proxy (uncomment and set your proxy URL when ready)
                // Proxy = new WebProxy("http://proxy-service-url:port"),
                // UseProxy = true,

                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
                UseCookies = true
            };

            // Initialize HttpClient with the handler
            _httpClient = new HttpClient(handler);

            // Set default headers
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        }

        public async Task<string> GetHtmlContentAsync(string url, int maxRetryAttempts = 5)
        {
            _logger.LogInformation($"Fetching content from URL: {url}");

            // Add a small random delay before starting to make the request pattern more human-like
            Random random = new Random();
            await Task.Delay(random.Next(1000, 3000));

            // Track if we've seen a 403 error
            bool received403 = false;

            return await RetryUtility.ExecuteWithRetryAsync(async () =>
            {
                // If we got a 403 on the previous attempt, wait longer and completely
                // change our request signature
                if (received403)
                {
                    // Much longer delay after a 403 (20-40 seconds)
                    int recoveryDelay = random.Next(20000, 40000);
                    _logger.LogInformation($"Applying special 403 recovery delay of {recoveryDelay}ms before retry");
                    await Task.Delay(recoveryDelay);

                    // Clear any cached DNS entries for the host
                    ServicePointManager.DnsRefreshTimeout = 0;
                }

                var request = CreateBrowserLikeRequest(url);

                var response = await _httpClient.SendWithRetryAsync(request, maxRetryAttempts: 1, logger: _logger);

                _logger.LogInformation($"Received response with status: {response.StatusCode}");

                // Special handling for 403 Forbidden
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    received403 = true;
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