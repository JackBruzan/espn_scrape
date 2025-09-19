using ESPNScrape.Configuration;
using ESPNScrape.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ESPNScrape.Services
{
    public class EspnCacheService : IEspnCacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<EspnCacheService> _logger;
        private readonly CacheConfiguration _config;
        private readonly ConcurrentDictionary<string, object> _keyTracking;

        public EspnCacheService(
            IMemoryCache memoryCache,
            ILogger<EspnCacheService> logger,
            IOptions<CacheConfiguration> config)
        {
            _memoryCache = memoryCache;
            _logger = logger;
            _config = config.Value;
            _keyTracking = new ConcurrentDictionary<string, object>();
        }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                if (_memoryCache.TryGetValue(key, out var cachedValue))
                {
                    if (cachedValue is T typedValue)
                    {
                        _logger.LogDebug("Cache hit for key: {Key}", key);
                        return Task.FromResult<T?>(typedValue);
                    }
                    else
                    {
                        _logger.LogWarning("Cache value type mismatch for key: {Key}. Expected: {ExpectedType}, Actual: {ActualType}",
                            key, typeof(T).Name, cachedValue?.GetType().Name ?? "null");
                        _memoryCache.Remove(key);
                    }
                }

                _logger.LogDebug("Cache miss for key: {Key}", key);
                return Task.FromResult<T?>(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving value from cache for key: {Key}", key);
                return Task.FromResult<T?>(null);
            }
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                var cachedValue = await GetAsync<T>(key, cancellationToken);
                if (cachedValue != null)
                {
                    return cachedValue;
                }

                _logger.LogDebug("Executing factory for cache key: {Key}", key);
                var value = await factory();

                if (value != null)
                {
                    await SetAsync(key, value, expiry, cancellationToken);
                }

                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrSetAsync for key: {Key}", key);
                throw;
            }
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                if (value == null)
                {
                    _logger.LogDebug("Attempted to cache null value for key: {Key}", key);
                    return Task.CompletedTask;
                }

                var actualExpiry = expiry ?? GetTtlForOperation(ExtractOperationFromKey(key));
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = actualExpiry,
                    Size = EstimateSize(value)
                };

                _memoryCache.Set(key, value, cacheOptions);
                _keyTracking.TryAdd(key, DateTime.UtcNow);

                _logger.LogDebug("Cached value for key: {Key} with expiry: {Expiry}", key, actualExpiry);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache value for key: {Key}", key);
                throw;
            }
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                _memoryCache.Remove(key);
                _keyTracking.TryRemove(key, out _);
                _logger.LogDebug("Removed cache entry for key: {Key}", key);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache entry for key: {Key}", key);
                throw;
            }
        }

        public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            try
            {
                var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                var keysToRemove = _keyTracking.Keys.Where(k => regex.IsMatch(k)).ToList();

                foreach (var key in keysToRemove)
                {
                    _memoryCache.Remove(key);
                    _keyTracking.TryRemove(key, out _);
                }

                _logger.LogDebug("Removed {Count} cache entries matching pattern: {Pattern}", keysToRemove.Count, pattern);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache entries by pattern: {Pattern}", pattern);
                throw;
            }
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_memoryCache.TryGetValue(key, out _));
        }

        public async Task WarmCacheAsync(int currentYear, int currentWeek, CancellationToken cancellationToken = default)
        {
            if (!_config.EnableCacheWarming)
            {
                _logger.LogDebug("Cache warming is disabled");
                return;
            }

            try
            {
                _logger.LogInformation("Starting cache warming for year: {Year}, week: {Week}", currentYear, currentWeek);

                // Pre-generate common cache keys that would likely be requested
                var commonKeys = new[]
                {
                    GenerateKey("GetSeason", currentYear),
                    GenerateKey("GetWeeks", currentYear, 2), // Regular season
                    GenerateKey("GetCurrentWeek"),
                    GenerateKey("GetWeek", currentYear, currentWeek, 2),
                    GenerateKey("GetGames", currentYear, currentWeek, 2),
                    GenerateKey("GetTeams")
                };

                foreach (var key in commonKeys)
                {
                    // Just log the keys that would be warmed - actual warming would require the ESPN service
                    _logger.LogDebug("Would warm cache for key: {Key}", key);
                }

                _logger.LogInformation("Cache warming completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache warming");
                throw;
            }
        }

        public string GenerateKey(string operation, params object[] parameters)
        {
            try
            {
                var keyParts = new List<string> { "ESPN", operation };
                keyParts.AddRange(parameters.Select(p => p?.ToString() ?? "null"));
                var key = string.Join(":", keyParts);

                _logger.LogTrace("Generated cache key: {Key}", key);
                return key;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating cache key for operation: {Operation}", operation);
                throw;
            }
        }

        public TimeSpan GetTtlForOperation(string operation)
        {
            return operation.ToLowerInvariant() switch
            {
                "getseason" or "getweeks" => TimeSpan.FromHours(_config.SeasonDataTtlHours),
                "getteams" => TimeSpan.FromHours(_config.TeamDataTtlHours),
                "getplayerstats" or "getweekplayerstats" => TimeSpan.FromMinutes(_config.PlayerStatsTtlMinutes),
                "getgame" or "getgames" => TimeSpan.FromMinutes(_config.CompletedGameTtlMinutes),
                "getboxscore" => TimeSpan.FromMinutes(_config.CompletedGameTtlMinutes),
                "live" => TimeSpan.FromSeconds(_config.LiveGameTtlSeconds),
                _ => TimeSpan.FromMinutes(_config.DefaultTtlMinutes)
            };
        }

        private long EstimateSize<T>(T value)
        {
            try
            {
                // Simple size estimation based on JSON serialization
                var json = JsonSerializer.Serialize(value);
                return json.Length / 1024; // Convert to KB approximation
            }
            catch
            {
                // Fallback estimation
                return 1; // 1KB default
            }
        }

        private string ExtractOperationFromKey(string key)
        {
            try
            {
                var parts = key.Split(':');
                return parts.Length > 1 ? parts[1] : "default";
            }
            catch
            {
                return "default";
            }
        }
    }
}