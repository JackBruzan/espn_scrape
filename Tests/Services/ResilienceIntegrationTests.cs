using ESPNScrape.Services;
using ESPNScrape.Services.Interfaces;
using ESPNScrape.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Xunit;

namespace ESPNScrape.Tests.Services
{
    public class ResilienceIntegrationTests
    {
        private readonly Mock<HttpMessageHandler> _mockHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<ILogger<EspnHttpService>> _mockLogger;
        private readonly Mock<IEspnRateLimitService> _mockRateLimitService;
        private readonly Mock<IOptions<ResilienceConfiguration>> _mockResilienceConfig;
        private readonly EspnHttpService _service;

        public ResilienceIntegrationTests()
        {
            _mockHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHandler.Object);
            _mockLogger = new Mock<ILogger<EspnHttpService>>();
            _mockRateLimitService = new Mock<IEspnRateLimitService>();
            _mockResilienceConfig = new Mock<IOptions<ResilienceConfiguration>>();

            // Setup test configuration
            var config = new ResilienceConfiguration
            {
                RetryPolicy = new RetryPolicyConfig
                {
                    MaxRetryAttempts = 3,
                    BaseDelayMs = 100,
                    MaxDelayMs = 5000,
                    EnableJitter = false, // Disable for predictable testing
                    RetryableStatusCodes = new List<int> { 429, 502, 503, 504 }
                },
                CircuitBreaker = new CircuitBreakerConfig
                {
                    FailureThreshold = 3,
                    OpenCircuitDurationSeconds = 1,
                    FailureStatusCodes = new List<int> { 500, 502, 503, 504 }
                }
            };

            _mockResilienceConfig.Setup(x => x.Value).Returns(config);
            _service = new EspnHttpService(_httpClient, _mockLogger.Object, _mockRateLimitService.Object, _mockResilienceConfig.Object);
        }

        [Fact]
        public async Task RetryPolicy_TransientFailure_RetriesCorrectly()
        {
            // Arrange
            var callCount = 0;
            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(() =>
                {
                    callCount++;
                    if (callCount < 3)
                    {
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                    }
                    return Task.FromResult(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent("{\"test\": \"data\"}")
                    });
                });

            // Act
            var result = await _service.GetRawJsonAsync("/test/endpoint");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, callCount); // Should have retried twice before success
            Assert.Contains("test", result);
        }

        [Fact]
        public async Task RetryPolicy_NonRetryableError_DoesNotRetry()
        {
            // Arrange
            var callCount = 0;
            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(() =>
                {
                    callCount++;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
                });

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                _service.GetRawJsonAsync("/test/endpoint"));

            Assert.Equal(1, callCount); // Should not have retried
        }

        [Fact]
        public async Task RetryPolicy_ExhaustsRetries_ThrowsException()
        {
            // Arrange
            var callCount = 0;
            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(() =>
                {
                    callCount++;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                });

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                _service.GetRawJsonAsync("/test/endpoint"));

            Assert.Equal(4, callCount); // Original call + 3 retries
        }

        [Fact]
        public async Task RateLimiting_IntegrationWithHttpService_CallsRateLimitService()
        {
            // Arrange
            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"test\": \"data\"}")
                });

            // Act
            await _service.GetRawJsonAsync("/test/endpoint");

            // Assert
            _mockRateLimitService.Verify(x => x.WaitForRequestAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetFromReferenceAsync_ValidReference_CallsCorrectly()
        {
            // Arrange
            var referenceUrl = "http://sports.core.api.espn.pvt/v2/sports/football/leagues/nfl/seasons/2025/events";
            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString() == referenceUrl),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"events\": []}")
                });

            // Act
            var result = await _service.GetFromReferenceAsync<dynamic>(referenceUrl);

            // Assert
            Assert.NotNull(result);
            _mockRateLimitService.Verify(x => x.WaitForRequestAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetFromReferenceAsync_NullReference_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.GetFromReferenceAsync<dynamic>(null));
        }

        [Fact]
        public async Task GetFromReferenceAsync_EmptyReference_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.GetFromReferenceAsync<dynamic>(""));
        }

        [Fact]
        public async Task GetAsync_DeserializationFailure_ThrowsInvalidOperationException()
        {
            // Arrange
            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("invalid json")
                });

            // Act & Assert
            await Assert.ThrowsAsync<JsonException>(() =>
                _service.GetAsync<TestObject>("/test/endpoint"));
        }

        [Fact]
        public async Task ErrorClassification_RetryableStatusCodes_TriggersRetry()
        {
            // Arrange
            var callCount = 0;
            var retryableStatusCodes = new[] { HttpStatusCode.TooManyRequests, HttpStatusCode.BadGateway,
                                               HttpStatusCode.ServiceUnavailable, HttpStatusCode.GatewayTimeout };

            foreach (var statusCode in retryableStatusCodes)
            {
                callCount = 0;
                _mockHandler.Protected()
                    .Setup<Task<HttpResponseMessage>>("SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .Returns(() =>
                    {
                        callCount++;
                        if (callCount < 2)
                        {
                            return Task.FromResult(new HttpResponseMessage(statusCode));
                        }
                        return Task.FromResult(new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent("{\"test\": \"data\"}")
                        });
                    });

                // Act
                var result = await _service.GetRawJsonAsync("/test/endpoint");

                // Assert
                Assert.NotNull(result);
                Assert.Equal(2, callCount); // Should have retried once
            }
        }

        private class TestObject
        {
            public string Name { get; set; } = string.Empty;
            public int Value { get; set; }
        }
    }
}