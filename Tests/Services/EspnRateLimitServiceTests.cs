using ESPNScrape.Services;
using ESPNScrape.Services.Interfaces;
using ESPNScrape.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ESPNScrape.Tests.Services
{
    public class EspnRateLimitServiceTests
    {
        private readonly Mock<ILogger<EspnRateLimitService>> _mockLogger;
        private readonly Mock<IOptions<ResilienceConfiguration>> _mockResilienceConfig;
        private readonly ResilienceConfiguration _config;
        private readonly EspnRateLimitService _service;

        public EspnRateLimitServiceTests()
        {
            _mockLogger = new Mock<ILogger<EspnRateLimitService>>();
            _mockResilienceConfig = new Mock<IOptions<ResilienceConfiguration>>();

            _config = new ResilienceConfiguration
            {
                RateLimit = new RateLimitConfig
                {
                    MaxRequests = 5,
                    TimeWindowSeconds = 10,
                    BurstAllowance = 2,
                    QueueTimeoutMs = 1000
                }
            };

            _mockResilienceConfig.Setup(x => x.Value).Returns(_config);
            _service = new EspnRateLimitService(_mockResilienceConfig.Object, _mockLogger.Object);
        }

        [Fact]
        public void CanMakeRequest_WithinLimit_ReturnsTrue()
        {
            // Act
            var canMake = _service.CanMakeRequest();

            // Assert
            Assert.True(canMake);
        }

        [Fact]
        public async Task WaitForRequestAsync_WithinLimit_CompletesQuickly()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            await _service.WaitForRequestAsync();
            stopwatch.Stop();

            // Assert
            Assert.True(stopwatch.ElapsedMilliseconds < 100); // Should complete quickly
        }

        [Fact]
        public async Task WaitForRequestAsync_ExceedsLimit_WaitsAppropriately()
        {
            // Arrange - make requests up to the limit
            for (int i = 0; i < _config.RateLimit.MaxRequests; i++)
            {
                await _service.WaitForRequestAsync();
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act - this should trigger rate limiting
            await _service.WaitForRequestAsync();
            stopwatch.Stop();

            // Assert - should have waited some time
            Assert.True(stopwatch.ElapsedMilliseconds > 500, $"Expected delay but completed in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Fact]
        public void GetStatus_InitialState_ReturnsCorrectInfo()
        {
            // Act
            var status = _service.GetStatus();

            // Assert
            Assert.Equal(_config.RateLimit.MaxRequests, status.RequestsRemaining);
            Assert.Equal(0, status.TotalRequests);
            Assert.False(status.IsLimited);
        }

        [Fact]
        public async Task GetStatus_AfterRequests_UpdatesCorrectly()
        {
            // Arrange
            await _service.WaitForRequestAsync();
            await _service.WaitForRequestAsync();

            // Act
            var status = _service.GetStatus();

            // Assert
            Assert.Equal(_config.RateLimit.MaxRequests - 2, status.RequestsRemaining);
            Assert.Equal(2, status.TotalRequests);
            Assert.False(status.IsLimited);
        }

        [Fact]
        public void Reset_ClearsAllCounters()
        {
            // Arrange - make some requests first
            _service.WaitForRequestAsync().Wait();
            _service.WaitForRequestAsync().Wait();

            // Act
            _service.Reset();
            var status = _service.GetStatus();

            // Assert
            Assert.Equal(_config.RateLimit.MaxRequests, status.RequestsRemaining);
            Assert.Equal(0, status.TotalRequests);
            Assert.False(status.IsLimited);
        }

        [Fact]
        public async Task WaitForRequestAsync_Cancellation_ThrowsCorrectException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _service.WaitForRequestAsync(cts.Token));
        }

        [Fact]
        public async Task WaitForRequestAsync_QueueTimeout_ThrowsTimeoutException()
        {
            // Arrange - create a config with very short timeout
            var shortTimeoutConfig = new ResilienceConfiguration
            {
                RateLimit = new RateLimitConfig
                {
                    MaxRequests = 1,
                    TimeWindowSeconds = 60, // Long window
                    BurstAllowance = 1,
                    QueueTimeoutMs = 10 // Very short timeout
                }
            };

            var mockConfig = new Mock<IOptions<ResilienceConfiguration>>();
            mockConfig.Setup(x => x.Value).Returns(shortTimeoutConfig);
            var service = new EspnRateLimitService(mockConfig.Object, _mockLogger.Object);

            // Fill up the rate limit
            await service.WaitForRequestAsync();

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(() =>
                service.WaitForRequestAsync());
        }
    }
}