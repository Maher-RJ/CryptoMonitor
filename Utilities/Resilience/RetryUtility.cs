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
            ILogger logger = null)
        {
            for (int attempt = 1; attempt <= maxRetryAttempts; attempt++)
            {
                try
                {
                    if (attempt > 1)
                    {
                        int delayMs = (int)Math.Pow(2, attempt) * 500;
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
            ILogger logger = null)
        {
            for (int attempt = 1; attempt <= maxRetryAttempts; attempt++)
            {
                try
                {
                    if (attempt > 1)
                    {
                        int delayMs = (int)Math.Pow(2, attempt) * 500;
                        logger?.LogWarning($"Retry attempt {attempt}/{maxRetryAttempts} after {delayMs}ms delay");
                        await Task.Delay(delayMs);
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
    }
}