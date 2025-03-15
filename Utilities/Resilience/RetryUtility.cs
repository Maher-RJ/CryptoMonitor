using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CryptoMonitor.Utilities.Resilience
{
    public static class RetryUtility
    {
        public static async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            int maxRetryAttempts = 3,
            ILogger logger = null,
            Func<int, int> retryDelayFunc = null)
        {
            retryDelayFunc ??= attempt => (int)Math.Pow(2, attempt) * 500; // Default exponential backoff

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
            retryDelayFunc ??= attempt => (int)Math.Pow(2, attempt) * 500; // Default exponential backoff

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
                // You would need to clone the content too, which might be complex
                // For simplicity in this case (GET requests), we're not handling content cloning
            }

            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            foreach (var property in request.Properties)
            {
                clone.Properties.Add(property);
            }

            return clone;
        }
    }
}