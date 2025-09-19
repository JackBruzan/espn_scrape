using ESPNScrape.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ESPNScrape.Tests.Services
{
    public class EspnHttpServiceTests : IDisposable
    {
        private readonly Mock<HttpMessageHandler> _mockHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<ILogger<EspnHttpService>> _mockLogger;
        private readonly EspnHttpService _service;

        public EspnHttpServiceTests()
        {
            _mockHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHandler.Object);
            _mockLogger = new Mock<ILogger<EspnHttpService>>();
            _service = new EspnHttpService(_httpClient, _mockLogger.Object);
        }

        [Fact]
        public async Task GetAsync_ValidUrl_ReturnsDeserializedObject()
        {
            // Arrange
            var testData = new { name = "test", value = 42 };
            var json = JsonSerializer.Serialize(testData);

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });

            // Act
            var result = await _service.GetAsync<dynamic>("/test/endpoint");

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetAsync_HttpError_ThrowsWithLogging()
        {
            // Arrange
            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError
                });

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => _service.GetAsync<object>("/test/error"));

            // Verify logging occurred
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ESPN HTTP request failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetFromReferenceAsync_ValidRef_ParsesAndReturns()
        {
            // Arrange
            var referenceUrl = "http://sports.core.api.espn.pvt/v2/sports/football/leagues/nfl/seasons/2025/types/2/weeks/3/events";
            var testData = new { items = new[] { new { id = "12345", name = "Test Game" } } };
            var json = JsonSerializer.Serialize(testData);

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });

            // Act
            var result = await _service.GetFromReferenceAsync<dynamic>(referenceUrl);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetFromReferenceAsync_EmptyUrl_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.GetFromReferenceAsync<object>(""));
            await Assert.ThrowsAsync<ArgumentException>(() => _service.GetFromReferenceAsync<object>(null!));
        }

        [Fact]
        public async Task GetRawJsonAsync_ValidEndpoint_ReturnsStringContent()
        {
            // Arrange
            var expectedContent = "{\"test\": \"data\"}";

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(expectedContent, Encoding.UTF8, "application/json")
                });

            // Act
            var result = await _service.GetRawJsonAsync("/test/endpoint");

            // Assert
            Assert.Equal(expectedContent, result);
        }

        [Fact]
        public async Task GetRawJsonAsync_FullUrl_UsesProvidedUrl()
        {
            // Arrange
            var fullUrl = "https://example.com/api/test";
            var expectedContent = "{\"test\": \"data\"}";

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == fullUrl),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(expectedContent, Encoding.UTF8, "application/json")
                });

            // Act
            var result = await _service.GetRawJsonAsync(fullUrl);

            // Assert
            Assert.Equal(expectedContent, result);
        }

        [Fact]
        public async Task GetRawJsonAsync_RelativeUrl_AddsBaseUrl()
        {
            // Arrange
            var endpoint = "/api/test";
            var expectedUrl = "https://www.espn.com/api/test";
            var expectedContent = "{\"test\": \"data\"}";

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == expectedUrl),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(expectedContent, Encoding.UTF8, "application/json")
                });

            // Act
            var result = await _service.GetRawJsonAsync(endpoint);

            // Assert
            Assert.Equal(expectedContent, result);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        public async Task GetRawJsonAsync_HttpErrorCodes_ThrowsHttpRequestException(HttpStatusCode statusCode)
        {
            // Arrange
            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode
                });

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => _service.GetRawJsonAsync("/test/error"));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _httpClient?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}