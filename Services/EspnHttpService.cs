using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using System.Text.Json;

namespace ESPNScrape.Services
{
    public class EspnHttpService : IEspnHttpService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EspnHttpService> _logger;
        private readonly IAsyncPolicy _retryPolicy;

        private const string BaseUrl = "https://www.espn.com";
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36";

        public EspnHttpService(HttpClient httpClient, ILogger<EspnHttpService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            // Configure headers
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Configure retry policy
            _retryPolicy = Policy
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning("ESPN HTTP retry attempt {RetryCount} after {Delay}ms for {Endpoint}",
                            retryCount, timespan.TotalMilliseconds, context.GetValueOrDefault("endpoint", "unknown"));
                    });
        }

        public async Task<T> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default)
        {
            var json = await GetRawJsonAsync(endpoint, cancellationToken);
            return JsonSerializer.Deserialize<T>(json) ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");
        }

        public async Task<string> GetRawJsonAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            var fullUrl = endpoint.StartsWith("http") ? endpoint : $"{BaseUrl}{endpoint}";

            return await _retryPolicy.ExecuteAsync(async (context) =>
            {
                try
                {
                    _logger.LogDebug("Making HTTP request to {Url}", fullUrl);

                    var response = await _httpClient.GetAsync(fullUrl, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync(cancellationToken);

                    _logger.LogDebug("ESPN HTTP request successful. Response length: {Length} characters", content.Length);

                    return content;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ESPN HTTP request failed for {Url}", fullUrl);
                    throw;
                }
            }, new Context { ["endpoint"] = fullUrl });
        }

        public async Task<T> GetFromReferenceAsync<T>(string referenceUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(referenceUrl))
                throw new ArgumentException("Reference URL cannot be null or empty", nameof(referenceUrl));

            // ESPN $ref URLs are direct API endpoints
            return await GetAsync<T>(referenceUrl, cancellationToken);
        }
    }
}