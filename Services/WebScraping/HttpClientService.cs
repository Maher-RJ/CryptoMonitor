using System;
using System.Collections.Generic;
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
        private readonly CookieContainer _cookieContainer;
        private readonly HttpClientHandler _httpClientHandler;
        private readonly Random _random = new Random();
        private DateTime _lastRequestTime = DateTime.MinValue;
        private readonly TimeSpan _minRequestInterval = TimeSpan.FromSeconds(10);

        // List of realistic browser user agents
        private readonly string[] _userAgents = new[] {
            // Chrome on Windows
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36",
            
            // Chrome on macOS
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
            
            // Firefox on Windows
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:124.0) Gecko/20100101 Firefox/124.0",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:123.0) Gecko/20100101 Firefox/123.0",
            
            // Firefox on macOS
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:124.0) Gecko/20100101 Firefox/124.0",
            
            // Safari on macOS
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15",
            
            // Edge on Windows
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36 Edg/123.0.0.0"
        };

        // List of accept-language values
        private readonly string[] _acceptLanguages = new[] {
            "en-US,en;q=0.9",
            "en-GB,en;q=0.9",
            "en-CA,en;q=0.9,fr-CA;q=0.8,fr;q=0.7",
            "en,de;q=0.9,it;q=0.8",
            "es-ES,es;q=0.9,en-US;q=0.8,en;q=0.7"
        };

        public HttpClientService(ILogger<HttpClientService> logger)
        {
            _logger = logger;
            _cookieContainer = new CookieContainer();
            _httpClientHandler = new HttpClientHandler
            {
                CookieContainer = _cookieContainer,
                UseCookies = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            _httpClient = new HttpClient(_httpClientHandler)
            {
                Timeout = TimeSpan.FromMinutes(1)
            };

            // Add initial browser-like headers to the default headers
            _httpClient.DefaultRequestHeaders.Add("DNT", "1");
            _httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
        }

        public async Task<string> GetHtmlContentAsync(string url, int maxRetryAttempts = 5)
        {
            _logger.LogInformation($"Fetching content from URL: {url}");

            // Ensure we don't make requests too frequently
            await EnforceRequestInterval();

            return await RetryUtility.ExecuteWithRetryAsync(async () =>
            {
                await EnforceRequestInterval();

                var request = CreateBrowserLikeRequest(url);

                // Use an exponential backoff retry approach with 403 handling
                HttpResponseMessage response = null;

                try
                {
                    response = await _httpClient.SendAsync(request);
                    _logger.LogInformation($"Received response with status: {response.StatusCode}");

                    // Handle 403 Forbidden errors specifically
                    if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        _logger.LogWarning("Website is blocking our request with 403 Forbidden. This may indicate anti-scraping measures.");
                        throw new HttpRequestException($"Website is blocking our request: {response.StatusCode}");
                    }

                    response.EnsureSuccessStatusCode();

                    // Store any cookies we received
                    StoreCookiesFromResponse(response, url);

                    string htmlContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"Successfully fetched HTML content (length: {htmlContent.Length} bytes)");

                    return htmlContent;
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("403"))
                {
                    // Special handling for 403 errors
                    int recoveryDelay = CalculateSpecial403RecoveryDelay();
                    _logger.LogInformation($"Applying special 403 recovery delay of {recoveryDelay}ms before retry");
                    await Task.Delay(recoveryDelay);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error fetching HTML: {ex.Message}");
                    throw;
                }
            }, maxRetryAttempts: maxRetryAttempts, logger: _logger,
            retryDelayFunc: attempt =>
            {
                // Progressive backoff with some randomness
                int baseDelay = (int)Math.Pow(2, attempt) * 2000;
                int jitter = _random.Next(-500, 500);
                return baseDelay + jitter;
            });
        }

        private HttpRequestMessage CreateBrowserLikeRequest(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            // Use a random user agent from our list
            string userAgent = _userAgents[_random.Next(_userAgents.Length)];
            request.Headers.Add("User-Agent", userAgent);

            // Add common browser headers
            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            request.Headers.Add("Accept-Language", _acceptLanguages[_random.Next(_acceptLanguages.Length)]);
            request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
            request.Headers.Add("Cache-Control", "max-age=0");
            request.Headers.Add("Connection", "keep-alive");

            // Determine the referer based on the domain or use a common site
            Uri uri = new Uri(url);
            string referer;

            if (_random.Next(100) < 70)
            {
                string[] searchReferrers = {
                    "https://www.google.com/",
                    "https://www.bing.com/",
                    "https://search.yahoo.com/",
                    "https://duckduckgo.com/"
                };
                referer = searchReferrers[_random.Next(searchReferrers.Length)];
            }
            else 
            {
                referer = $"{uri.Scheme}://{uri.Host}/";
            }

            request.Headers.Add("Referer", referer);

            // Add browser-specific headers that make the request look legitimate
            request.Headers.Add("Sec-Fetch-Dest", "document");
            request.Headers.Add("Sec-Fetch-Mode", "navigate");
            request.Headers.Add("Sec-Fetch-Site", _random.Next(100) < 70 ? "cross-site" : "same-origin");
            request.Headers.Add("Sec-Fetch-User", "?1");
            request.Headers.Add("Sec-CH-UA", "\"Google Chrome\";v=\"123\", \"Not:A-Brand\";v=\"8\"");
            request.Headers.Add("Sec-CH-UA-Mobile", "?0");
            request.Headers.Add("Sec-CH-UA-Platform", "\"Windows\"");
            request.Headers.Add("Upgrade-Insecure-Requests", "1");
            request.Headers.Add("Priority", "u=0, i");

            return request;
        }

        private void StoreCookiesFromResponse(HttpResponseMessage response, string url)
        {
            IEnumerable<string> cookieHeaders;
            if (response.Headers.TryGetValues("Set-Cookie", out cookieHeaders))
            {
                Uri uri = new Uri(url);
                foreach (var cookieHeader in cookieHeaders)
                {
                    try
                    {
                        _cookieContainer.SetCookies(uri, cookieHeader);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to parse cookie: {ex.Message}");
                    }
                }
            }
        }

        private async Task EnforceRequestInterval()
        {
            // Ensure we don't make requests too frequently
            var timeSinceLastRequest = DateTime.Now - _lastRequestTime;
            if (timeSinceLastRequest < _minRequestInterval)
            {
                var delayTime = _minRequestInterval - timeSinceLastRequest;
                // Add a random component to make it look more human
                var randomExtraMs = _random.Next(100, 2000);
                var totalDelay = delayTime.Add(TimeSpan.FromMilliseconds(randomExtraMs));

                await Task.Delay(totalDelay);
            }

            _lastRequestTime = DateTime.Now;
        }

        private int CalculateSpecial403RecoveryDelay()
        {
            if (_random.Next(100) < 70)
            {
                return _random.Next(10000, 25000);
            }
            else
            {
                return _random.Next(30000, 60000);
            }
        }
    }
}