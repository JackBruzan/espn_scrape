using ESPNScrape.HealthChecks;
using ESPNScrape.Services;
using ESPNScrape.Services.Interfaces;
using ESPNScrape.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ESPNScrape.Tests.HealthChecks
{
    public class EspnApiHealthCheckTests
    {
        private readonly Mock<IEspnHttpService> _mockHttpService;
        private readonly Mock<IEspnRateLimitService> _mockRateLimitService;
        private readonly Mock<ILogger<EspnApiHealthCheck>> _mockLogger;
        private readonly Mock<IOptions<ResilienceConfiguration>> _mockResilienceConfig;
        private readonly EspnApiHealthCheck _healthCheck;

        public EspnApiHealthCheckTests()
        {
            _mockHttpService = new Mock<IEspnHttpService>();
            _mockRateLimitService = new Mock<IEspnRateLimitService>();
            _mockLogger = new Mock<ILogger<EspnApiHealthCheck>>();
            _mockResilienceConfig = new Mock<IOptions<ResilienceConfiguration>>();

            var config = new ResilienceConfiguration
            {
                HealthCheck = new HealthCheckConfig
                {
                    TestEndpoints = new List<string> { "/nfl/", "/nfl/scoreboard" }
                },
                Timeouts = new TimeoutConfig
                {
                    HealthCheckTimeoutSeconds = 10
                }
            };

            _mockResilienceConfig.Setup(x => x.Value).Returns(config);
            _healthCheck = new EspnApiHealthCheck(_mockHttpService.Object, _mockRateLimitService.Object,
                                                _mockLogger.Object, _mockResilienceConfig.Object);
        }

        [Fact]
        public async Task CheckHealthAsync_AllEndpointsHealthy_ReturnsHealthy()
        {
            // Arrange
            _mockRateLimitService.Setup(x => x.GetStatus())
                .Returns(new RateLimitStatus
                {
                    RequestsRemaining = 50,
                    TotalRequests = 10,
                    IsLimited = false
                });

            _mockHttpService.Setup(x => x.GetRawJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("valid response content");

            // Act
            var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

            // Assert
            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Contains("ESPN API is responding successfully", result.Description);
            Assert.NotNull(result.Data);
            Assert.True(result.Data.ContainsKey("RateLimit"));
            Assert.True(result.Data.ContainsKey("EndpointTests"));
        }

        [Fact]
        public async Task CheckHealthAsync_RateLimited_ReturnsDegraded()
        {
            // Arrange
            _mockRateLimitService.Setup(x => x.GetStatus())
                .Returns(new RateLimitStatus
                {
                    RequestsRemaining = 0,
                    TotalRequests = 100,
                    IsLimited = true
                });

            _mockHttpService.Setup(x => x.GetRawJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("valid response content");

            // Act
            var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

            // Assert
            Assert.Equal(HealthStatus.Degraded, result.Status);
            Assert.Contains("Rate limit exceeded", result.Description);
        }

        [Fact]
        public async Task CheckHealthAsync_EndpointFailure_ReturnsUnhealthy()
        {
            // Arrange
            _mockRateLimitService.Setup(x => x.GetStatus())
                .Returns(new RateLimitStatus
                {
                    RequestsRemaining = 50,
                    TotalRequests = 10,
                    IsLimited = false
                });

            _mockHttpService.SetupSequence(x => x.GetRawJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("valid response") // First endpoint succeeds
                .ThrowsAsync(new HttpRequestException("Network error")); // Second endpoint fails

            // Act
            var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

            // Assert
            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("Network error", result.Description);
        }

        [Fact]
        public async Task CheckHealthAsync_HttpRequestException_ReturnsUnhealthy()
        {
            // Arrange
            _mockRateLimitService.Setup(x => x.GetStatus())
                .Returns(new RateLimitStatus
                {
                    RequestsRemaining = 50,
                    TotalRequests = 10,
                    IsLimited = false
                });

            _mockHttpService.Setup(x => x.GetRawJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("HTTP 500 Internal Server Error"));

            // Act
            var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

            // Assert
            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("HTTP 500 Internal Server Error", result.Description);
            Assert.Equal(typeof(HttpRequestException), result.Exception?.GetType());
        }

        [Fact]
        public async Task CheckHealthAsync_TaskCanceledException_ReturnsUnhealthy()
        {
            // Arrange
            _mockRateLimitService.Setup(x => x.GetStatus())
                .Returns(new RateLimitStatus
                {
                    RequestsRemaining = 50,
                    TotalRequests = 10,
                    IsLimited = false
                });

            _mockHttpService.Setup(x => x.GetRawJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TaskCanceledException("Request timeout"));

            // Act
            var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

            // Assert
            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("timed out", result.Description);
        }

        [Fact]
        public async Task CheckHealthAsync_UnexpectedException_ReturnsUnhealthy()
        {
            // Arrange
            _mockRateLimitService.Setup(x => x.GetStatus())
                .Returns(new RateLimitStatus
                {
                    RequestsRemaining = 50,
                    TotalRequests = 10,
                    IsLimited = false
                });

            _mockHttpService.Setup(x => x.GetRawJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Unexpected error"));

            // Act
            var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

            // Assert
            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("unexpected error", result.Description);
            Assert.Contains("Unexpected error", result.Description);
        }

        [Fact]
        public async Task CheckHealthAsync_EmptyResponse_ReturnsFailure()
        {
            // Arrange
            _mockRateLimitService.Setup(x => x.GetStatus())
                .Returns(new RateLimitStatus
                {
                    RequestsRemaining = 50,
                    TotalRequests = 10,
                    IsLimited = false
                });

            _mockHttpService.Setup(x => x.GetRawJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(string.Empty);

            // Act
            var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

            // Assert
            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("Empty response", result.Description);
        }

        [Fact]
        public async Task CheckHealthAsync_IncludesDetailedData()
        {
            // Arrange
            _mockRateLimitService.Setup(x => x.GetStatus())
                .Returns(new RateLimitStatus
                {
                    RequestsRemaining = 75,
                    TotalRequests = 25,
                    IsLimited = false,
                    TimeUntilReset = TimeSpan.FromMinutes(5)
                });

            _mockHttpService.Setup(x => x.GetRawJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("test response data");

            // Act
            var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

            // Assert
            Assert.NotNull(result.Data);

            // Check rate limit data
            Assert.True(result.Data.ContainsKey("RateLimit"));
            var rateLimitData = result.Data["RateLimit"];
            Assert.NotNull(rateLimitData);

            // Check endpoint test data
            Assert.True(result.Data.ContainsKey("EndpointTests"));
            var endpointTests = result.Data["EndpointTests"];
            Assert.NotNull(endpointTests);

            // Check total issues count
            Assert.True(result.Data.ContainsKey("TotalIssues"));
            Assert.Equal(0, result.Data["TotalIssues"]);
        }
    }
}