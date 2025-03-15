using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CryptoMonitor.Utilities.Resilience
{
    public static class RetryUtility
    {
        private static readonly Random _random = new Random();

        public static async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            int maxRetryAttempts = 3,
            ILogger logger = null,
            Func<int, int> retryDelayFunc = null)
        {
            retryDelayFunc ??= attempt => GetDefaultRetryDelay(attempt);

            for (int attempt = 1; attempt <= maxRetryAttempts; attempt++)
            {
                try
                {
                    if (attempt > 1)
                    {
                        int delayMs = retryDelayFunc(attempt);
                        logger?.LogWarning($"Retry attempt {attempt}/{maxRetryAttempts} after {delayMs}ms delay");
                        await Task.Delay(delayMs);
                    }

                    return await operation();
                }
                catch (HttpRequestException ex)
                {
                    logger?.LogWarning(ex, $"HTTP error on attempt {attempt}/{maxRetryAttempts}: {ex.Message}");

                    // Stop retrying if we've reached the max attempts
                    if (attempt == maxRetryAttempts)
                    {
                        throw;
                    }

                    // If we're getting close to the 3-minute window limit, reduce retries
                    if (attempt >= 3 && IsHttpErrorFatal(ex))
                    {
                        logger?.LogWarning("Fatal HTTP error detected, aborting retry sequence");
                        throw;
                    }
                }
                catch (TaskCanceledException ex)
                {
                    // Handle timeouts specially - they may indicate rate limiting
                    logger?.LogWarning(ex, $"Request timeout on attempt {attempt}/{maxRetryAttempts}");

                    if (attempt == maxRetryAttempts)
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, $"Unexpected error on attempt {attempt}/{maxRetryAttempts}: {ex.Message}");
                    throw;
                }
            }

            throw new Exception($"Failed to execute operation after {maxRetryAttempts} attempts");
        }

        public static async Task<HttpResponseMessage> SendWithRetryAsync(
            this HttpClient httpClient,
            HttpRequestMessage request,
            int maxRetryAttempts = 3,
            ILogger logger = null,
            Func<int, int> retryDelayFunc = null)
        {
            retryDelayFunc ??= attempt => GetDefaultRetryDelay(attempt);

            for (int attempt = 1; attempt <= maxRetryAttempts; attempt++)
            {
                try
                {
                    if (attempt > 1)
                    {
                        int delayMs = retryDelayFunc(attempt);
                        logger?.LogWarning($"Retry attempt {attempt}/{maxRetryAttempts} after {delayMs}ms delay");
                        await Task.Delay(delayMs);

                        // Need to clone the request since HttpRequestMessage can't be reused
                        request = CloneHttpRequestMessage(request);
                    }

                    var response = await httpClient.SendAsync(request);

                    // These status codes might benefit from a retry
                    if ((int)response.StatusCode == 429 || ((int)response.StatusCode >= 500 && (int)response.StatusCode < 600))
                    {
                        logger?.LogWarning($"Received status code {response.StatusCode}");

                        if (attempt == maxRetryAttempts)
                        {
                            return response;
                        }

                        continue;
                    }

                    return response;
                }
                catch (HttpRequestException ex)
                {
                    logger?.LogWarning(ex, $"HTTP error on attempt {attempt}/{maxRetryAttempts}: {ex.Message}");

                    if (attempt == maxRetryAttempts || IsHttpErrorFatal(ex))
                    {
                        throw;
                    }
                }
                catch (TaskCanceledException ex)
                {
                    logger?.LogWarning(ex, $"Request timeout on attempt {attempt}/{maxRetryAttempts}");

                    if (attempt == maxRetryAttempts)
                    {
                        throw;
                    }
                }
            }

            throw new Exception($"Failed to send HTTP request after {maxRetryAttempts} attempts");
        }

        // Helper method to clone an HttpRequestMessage since they can't be reused
        private static HttpRequestMessage CloneHttpRequestMessage(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);

            if (request.Content != null)
            {
                // Clone the content as a string - this doesn't work for binary content but works for our HTML/text needs
                var contentTask = request.Content.ReadAsStringAsync();
                contentTask.Wait();
                clone.Content = new StringContent(contentTask.Result);

                // Copy content headers
                if (request.Content.Headers != null)
                    foreach (var h in request.Content.Headers)
                        clone.Content.Headers.Add(h.Key, h.Value);
            }

            // Copy the headers
            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            // Copy properties
            foreach (var property in request.Properties)
            {
                clone.Properties.Add(property);
            }

            return clone;
        }

        // Default retry delay calculation
        private static int GetDefaultRetryDelay(int attempt)
        {
            // Base delay with exponential backoff
            int baseDelay = (int)Math.Pow(2, attempt) * 1000;

            // Add jitter (randomness) to avoid thundering herd problem
            int jitter = _random.Next(-baseDelay / 4, baseDelay / 4);

            // For a 3-minute schedule, we need to keep retries somewhat restrained
            int maxDelay = 20000; // 20 seconds max
            return Math.Min(baseDelay + jitter, maxDelay);
        }

        // Check if an HTTP error is fatal and not worth retrying
        private static bool IsHttpErrorFatal(HttpRequestException ex)
        {
            // For a 3-minute schedule, some errors aren't worth retrying
            string message = ex.Message.ToLowerInvariant();

            return message.Contains("forbidden") ||
                   message.Contains("unauthorized") ||
                   message.Contains("not found") ||
                   message.Contains("method not allowed");
        }
    }
}