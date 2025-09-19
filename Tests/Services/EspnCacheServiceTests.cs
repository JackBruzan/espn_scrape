using ESPNScrape.Configuration;
using ESPNScrape.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ESPNScrape.Tests.Services
{
    public class EspnCacheServiceTests : IDisposable
    {
        private readonly Mock<ILogger<EspnCacheService>> _mockLogger;
        private readonly Mock<IOptions<CacheConfiguration>> _mockOptions;
        private readonly CacheConfiguration _cacheConfig;
        private readonly MemoryCache _memoryCache;
        private readonly EspnCacheService _cacheService;

        public EspnCacheServiceTests()
        {
            _mockLogger = new Mock<ILogger<EspnCacheService>>();
            _mockOptions = new Mock<IOptions<CacheConfiguration>>();

            _cacheConfig = new CacheConfiguration
            {
                DefaultTtlMinutes = 30,
                SeasonDataTtlHours = 24,
                CompletedGameTtlMinutes = 60,
                LiveGameTtlSeconds = 30,
                PlayerStatsTtlMinutes = 15,
                TeamDataTtlHours = 12,
                EnableCacheWarming = true,
                MaxCacheSize = 1000
            };

            _mockOptions.Setup(x => x.Value).Returns(_cacheConfig);
            _memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 });
            _cacheService = new EspnCacheService(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        }

        [Fact]
        public async Task GetAsync_ExistingKey_ReturnsValue()
        {
            // Arrange
            var key = "test:key";
            var testValue = new TestData { Name = "Test", Value = 42 };
            await _cacheService.SetAsync(key, testValue);

            // Act
            var result = await _cacheService.GetAsync<TestData>(key);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(testValue.Name, result.Name);
            Assert.Equal(testValue.Value, result.Value);
        }

        [Fact]
        public async Task GetAsync_NonExistentKey_ReturnsNull()
        {
            // Arrange
            var key = "nonexistent:key";

            // Act
            var result = await _cacheService.GetAsync<TestData>(key);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetAsync_WrongType_ReturnsNullAndRemovesEntry()
        {
            // Arrange
            var key = "test:key";
            var testValue = "string value";
            var options = new MemoryCacheEntryOptions { Size = 1 };
            _memoryCache.Set(key, testValue, options);

            // Act
            var result = await _cacheService.GetAsync<TestData>(key);

            // Assert
            Assert.Null(result);
            Assert.False(_memoryCache.TryGetValue(key, out _)); // Entry should be removed
        }

        [Fact]
        public async Task SetAsync_ValidValue_StoresInCache()
        {
            // Arrange
            var key = "test:key";
            var testValue = new TestData { Name = "Test", Value = 42 };

            // Act
            await _cacheService.SetAsync(key, testValue);

            // Assert
            Assert.True(_memoryCache.TryGetValue(key, out var cachedValue));
            Assert.IsType<TestData>(cachedValue);
            var typed = (TestData)cachedValue;
            Assert.Equal(testValue.Name, typed.Name);
            Assert.Equal(testValue.Value, typed.Value);
        }

        [Fact]
        public async Task SetAsync_NullValue_DoesNotStore()
        {
            // Arrange
            var key = "test:key";

            // Act
            await _cacheService.SetAsync<TestData>(key, null!);

            // Assert
            Assert.False(_memoryCache.TryGetValue(key, out _));
        }

        [Fact]
        public async Task GetOrSetAsync_CacheHit_ReturnsExistingValue()
        {
            // Arrange
            var key = "test:key";
            var existingValue = new TestData { Name = "Existing", Value = 100 };
            await _cacheService.SetAsync(key, existingValue);

            var factoryCalled = false;
            var factory = new Func<Task<TestData>>(() =>
            {
                factoryCalled = true;
                return Task.FromResult(new TestData { Name = "New", Value = 200 });
            });

            // Act
            var result = await _cacheService.GetOrSetAsync(key, factory);

            // Assert
            Assert.False(factoryCalled);
            Assert.Equal(existingValue.Name, result.Name);
            Assert.Equal(existingValue.Value, result.Value);
        }

        [Fact]
        public async Task GetOrSetAsync_CacheMiss_CallsFactoryAndCaches()
        {
            // Arrange
            var key = "test:key";
            var newValue = new TestData { Name = "New", Value = 200 };

            var factoryCalled = false;
            var factory = new Func<Task<TestData>>(() =>
            {
                factoryCalled = true;
                return Task.FromResult(newValue);
            });

            // Act
            var result = await _cacheService.GetOrSetAsync(key, factory);

            // Assert
            Assert.True(factoryCalled);
            Assert.Equal(newValue.Name, result.Name);
            Assert.Equal(newValue.Value, result.Value);

            // Verify it was cached
            var cachedResult = await _cacheService.GetAsync<TestData>(key);
            Assert.NotNull(cachedResult);
            Assert.Equal(newValue.Name, cachedResult.Name);
        }

        [Fact]
        public async Task RemoveAsync_ExistingKey_RemovesFromCache()
        {
            // Arrange
            var key = "test:key";
            var testValue = new TestData { Name = "Test", Value = 42 };
            await _cacheService.SetAsync(key, testValue);

            // Verify it's cached
            Assert.True(_memoryCache.TryGetValue(key, out _));

            // Act
            await _cacheService.RemoveAsync(key);

            // Assert
            Assert.False(_memoryCache.TryGetValue(key, out _));
        }

        [Fact]
        public async Task RemoveByPatternAsync_MatchingKeys_RemovesAllMatches()
        {
            // Arrange
            var keys = new[] { "ESPN:GetSeason:2024", "ESPN:GetWeek:2024:1", "OTHER:Different:Key", "ESPN:GetGames:2024:1" };
            var testValue = new TestData { Name = "Test", Value = 42 };

            foreach (var key in keys)
            {
                await _cacheService.SetAsync(key, testValue);
            }

            // Act
            await _cacheService.RemoveByPatternAsync("^ESPN:.*");

            // Assert
            Assert.False(_memoryCache.TryGetValue("ESPN:GetSeason:2024", out _));
            Assert.False(_memoryCache.TryGetValue("ESPN:GetWeek:2024:1", out _));
            Assert.False(_memoryCache.TryGetValue("ESPN:GetGames:2024:1", out _));
            Assert.True(_memoryCache.TryGetValue("OTHER:Different:Key", out _)); // Should remain
        }

        [Fact]
        public async Task ExistsAsync_ExistingKey_ReturnsTrue()
        {
            // Arrange
            var key = "test:key";
            var testValue = new TestData { Name = "Test", Value = 42 };
            await _cacheService.SetAsync(key, testValue);

            // Act
            var exists = await _cacheService.ExistsAsync(key);

            // Assert
            Assert.True(exists);
        }

        [Fact]
        public async Task ExistsAsync_NonExistentKey_ReturnsFalse()
        {
            // Arrange
            var key = "nonexistent:key";

            // Act
            var exists = await _cacheService.ExistsAsync(key);

            // Assert
            Assert.False(exists);
        }

        [Theory]
        [InlineData("GetSeason", 2024)]
        [InlineData("GetWeeks", 2024, 2)]
        [InlineData("GetCurrentWeek")]
        [InlineData("GetGames", 2024, 1, 2)]
        public void GenerateKey_ValidParameters_ReturnsConsistentKey(string operation, params object[] parameters)
        {
            // Act
            var key1 = _cacheService.GenerateKey(operation, parameters);
            var key2 = _cacheService.GenerateKey(operation, parameters);

            // Assert
            Assert.NotNull(key1);
            Assert.Equal(key1, key2);
            Assert.StartsWith("ESPN:" + operation, key1);
        }

        [Theory]
        [InlineData("getseason", 24)]
        [InlineData("getweeks", 24)]
        [InlineData("getteams", 12)]
        [InlineData("getplayerstats", 15)]
        [InlineData("getgames", 60)]
        [InlineData("live", 0)] // Special case for seconds
        [InlineData("unknown", 30)]
        public void GetTtlForOperation_DifferentOperations_ReturnsCorrectTtl(string operation, int expectedMinutesOrHours)
        {
            // Act
            var ttl = _cacheService.GetTtlForOperation(operation);

            // Assert
            Assert.True(ttl.TotalMinutes > 0);

            // Verify expected TTL based on operation type
            switch (operation.ToLowerInvariant())
            {
                case "getseason":
                case "getweeks":
                    Assert.Equal(TimeSpan.FromHours(expectedMinutesOrHours), ttl);
                    break;
                case "getteams":
                    Assert.Equal(TimeSpan.FromHours(expectedMinutesOrHours), ttl);
                    break;
                case "live":
                    Assert.Equal(TimeSpan.FromSeconds(_cacheConfig.LiveGameTtlSeconds), ttl);
                    break;
                default:
                    Assert.Equal(TimeSpan.FromMinutes(expectedMinutesOrHours), ttl);
                    break;
            }
        }

        [Fact]
        public async Task WarmCacheAsync_CacheWarmingEnabled_LogsWarmingOperations()
        {
            // Arrange
            var currentYear = 2024;
            var currentWeek = 3;

            // Act
            await _cacheService.WarmCacheAsync(currentYear, currentWeek);

            // Assert
            // Verify that appropriate logging occurred (checking mock calls)
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting cache warming")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cache warming completed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task WarmCacheAsync_CacheWarmingDisabled_SkipsWarming()
        {
            // Arrange
            _cacheConfig.EnableCacheWarming = false;
            var currentYear = 2024;
            var currentWeek = 3;

            // Act
            await _cacheService.WarmCacheAsync(currentYear, currentWeek);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cache warming is disabled")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        public void Dispose()
        {
            _memoryCache?.Dispose();
        }

        private class TestData
        {
            public string Name { get; set; } = string.Empty;
            public int Value { get; set; }
        }
    }
}