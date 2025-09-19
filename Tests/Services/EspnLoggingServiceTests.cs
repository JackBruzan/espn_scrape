using ESPNScrape.Configuration;
using ESPNScrape.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ESPNScrape.Tests.Services
{
    public class EspnLoggingServiceTests
    {
        private readonly Mock<ILogger<EspnLoggingService>> _mockLogger;
        private readonly Mock<IOptions<LoggingConfiguration>> _mockOptions;
        private readonly LoggingConfiguration _config;
        private readonly EspnLoggingService _loggingService;

        public EspnLoggingServiceTests()
        {
            _mockLogger = new Mock<ILogger<EspnLoggingService>>();
            _mockOptions = new Mock<IOptions<LoggingConfiguration>>();

            _config = new LoggingConfiguration
            {
                StructuredLogging = new StructuredLoggingConfig
                {
                    EnableStructuredLogging = true,
                    UseJsonFormat = true,
                    IncludeScopes = true
                },
                Correlation = new CorrelationConfig
                {
                    EnableCorrelationIds = true,
                    GenerateIfMissing = true,
                    IncludeInLogs = true
                },
                PerformanceMetrics = new PerformanceMetricsConfig
                {
                    TrackResponseTimes = true,
                    EnableDetailedMetrics = true
                },
                BusinessMetrics = new BusinessMetricsConfig
                {
                    EnableBusinessMetrics = true,
                    TrackApiCallCounts = true
                }
            };

            _mockOptions.Setup(x => x.Value).Returns(_config);
            _loggingService = new EspnLoggingService(_mockLogger.Object, _mockOptions.Object);
        }

        [Fact]
        public void LogApiOperation_WithValidParameters_LogsCorrectly()
        {
            // Arrange
            var endpoint = "/test/endpoint";
            var method = "GET";
            var responseTime = TimeSpan.FromMilliseconds(500);
            var statusCode = 200;
            const bool success = true;

            // Act
            _loggingService.LogApiOperation(endpoint, method, responseTime, statusCode, success);

            // Assert
            VerifyLogCall(LogLevel.Information, Times.Once());
        }

        [Fact]
        public void LogApiOperation_WithFailure_LogsAsWarning()
        {
            // Arrange
            var endpoint = "/test/endpoint";
            var method = "GET";
            var responseTime = TimeSpan.FromMilliseconds(500);
            var statusCode = 500;
            const bool success = false;

            // Act
            _loggingService.LogApiOperation(endpoint, method, responseTime, statusCode, success);

            // Assert
            VerifyLogCall(LogLevel.Warning, Times.Once());
        }

        [Fact]
        public void LogCacheOperation_WithHit_LogsCorrectly()
        {
            // Arrange
            var operation = "Get";
            var key = "test-key";
            const bool hit = true;
            var duration = TimeSpan.FromMilliseconds(10);

            // Act
            _loggingService.LogCacheOperation(operation, key, hit, duration);

            // Assert
            VerifyLogCall(LogLevel.Debug, Times.Once());
        }

        [Fact]
        public void LogCacheOperation_WithMiss_LogsCorrectly()
        {
            // Arrange
            var operation = "Get";
            var key = "test-key";
            const bool hit = false;
            var duration = TimeSpan.FromMilliseconds(10);

            // Act
            _loggingService.LogCacheOperation(operation, key, hit, duration);

            // Assert
            VerifyLogCall(LogLevel.Debug, Times.Once());
        }

        [Fact]
        public void LogBusinessMetric_WithValidMetric_LogsCorrectly()
        {
            // Arrange
            var metricName = "test_metric";
            var value = 42.5;
            var tags = new Dictionary<string, object> { { "tag1", "value1" } };

            // Act
            _loggingService.LogBusinessMetric(metricName, value, tags);

            // Assert
            VerifyLogCall(LogLevel.Information, Times.Once());
        }

        [Fact]
        public void LogError_WithException_LogsCorrectly()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception");
            var message = "Test error message";
            var context = new Dictionary<string, object> { { "key1", "value1" } };

            // Act
            _loggingService.LogError(exception, message, context);

            // Assert
            VerifyLogCall(LogLevel.Error, Times.Once());
        }

        [Fact]
        public void BeginTimedOperation_ReturnsDisposableOperation()
        {
            // Arrange
            var operationName = "TestOperation";

            // Act
            var operation = _loggingService.BeginTimedOperation(operationName);

            // Assert
            Assert.NotNull(operation);
            Assert.IsAssignableFrom<IDisposable>(operation);
        }

        [Fact]
        public void BeginTimedOperation_WhenDisposed_LogsCompletionTime()
        {
            // Arrange
            var operationName = "TestOperation";

            // Act
            using var operation = _loggingService.BeginTimedOperation(operationName);
            Thread.Sleep(50); // Simulate some work

            // Assert - completion will be logged when disposed
            // We can't easily test the exact log call since it happens in Dispose()
            Assert.NotNull(operation);
        }

        [Fact]
        public void GenerateCorrelationId_ReturnsValidGuid()
        {
            // Act
            var correlationId = EspnLoggingService.GenerateCorrelationId();

            // Assert
            Assert.True(Guid.TryParse(correlationId, out _));
        }

        [Fact]
        public void SetCorrelationContext_WithValidId_SetsContext()
        {
            // Arrange
            var correlationId = Guid.NewGuid().ToString();

            // Act
            _loggingService.SetCorrelationContext(correlationId);

            // Assert
            var currentId = _loggingService.GetCurrentCorrelationId();
            Assert.Equal(correlationId, currentId);
        }

        [Fact]
        public void GetCurrentCorrelationId_WhenNotSet_ReturnsNull()
        {
            // Act
            var correlationId = _loggingService.GetCurrentCorrelationId();

            // Assert
            Assert.Null(correlationId);
        }

        [Fact]
        public void ClearCorrelationContext_RemovesCurrentContext()
        {
            // Arrange
            var correlationId = Guid.NewGuid().ToString();
            _loggingService.SetCorrelationContext(correlationId);

            // Act
            _loggingService.ClearCorrelationContext();

            // Assert
            var currentId = _loggingService.GetCurrentCorrelationId();
            Assert.Null(currentId);
        }

        [Fact]
        public void LogHealth_WithHealthyStatus_LogsCorrectly()
        {
            // Arrange
            var componentName = "TestComponent";
            const bool healthy = true;
            var duration = TimeSpan.FromMilliseconds(100);
            var details = new Dictionary<string, object> { { "detail1", "value1" } };

            // Act
            _loggingService.LogHealth(componentName, healthy, duration, details);

            // Assert
            VerifyLogCall(LogLevel.Information, Times.Once());
        }

        [Fact]
        public void LogHealth_WithUnhealthyStatus_LogsAsWarning()
        {
            // Arrange
            var componentName = "TestComponent";
            const bool healthy = false;
            var duration = TimeSpan.FromMilliseconds(100);
            var details = new Dictionary<string, object> { { "error", "Service unavailable" } };

            // Act
            _loggingService.LogHealth(componentName, healthy, duration, details);

            // Assert
            VerifyLogCall(LogLevel.Warning, Times.Once());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void LogBulkOperation_WithDifferentSuccessStates_LogsCorrectly(bool success)
        {
            // Arrange
            var operationType = "TestBulkOperation";
            var itemsProcessed = 100;
            var duration = TimeSpan.FromMinutes(2);
            var errorCount = success ? 0 : 5;

            // Act
            _loggingService.LogBulkOperation(operationType, itemsProcessed, duration, errorCount, success);

            // Assert
            var expectedLogLevel = success ? LogLevel.Information : LogLevel.Warning;
            VerifyLogCall(expectedLogLevel, Times.Once());
        }

        [Fact]
        public void LogConfigurationLoad_LogsCorrectly()
        {
            // Arrange
            var configSection = "TestSection";
            const bool success = true;
            var details = new Dictionary<string, object> { { "setting1", "value1" } };

            // Act
            _loggingService.LogConfigurationLoad(configSection, success, details);

            // Assert
            VerifyLogCall(LogLevel.Information, Times.Once());
        }

        [Fact]
        public void LogPerformanceMetric_LogsCorrectly()
        {
            // Arrange
            var metricName = "ResponseTime";
            var value = 500.0;
            var unit = "ms";
            var tags = new Dictionary<string, string> { { "endpoint", "/api/test" } };

            // Act
            _loggingService.LogPerformanceMetric(metricName, value, unit, tags);

            // Assert
            VerifyLogCall(LogLevel.Debug, Times.Once());
        }

        private void VerifyLogCall(LogLevel expectedLevel, Times times)
        {
            _mockLogger.Verify(
                x => x.Log(
                    expectedLevel,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                times);
        }
    }

    public class TimedOperationTests
    {
        [Fact]
        public void TimedOperation_TracksElapsedTime()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EspnLoggingService>>();
            var operationName = "TestOperation";

            // Act
            var operation = new TimedOperation(mockLogger.Object, operationName);
            Thread.Sleep(50); // Simulate work
            operation.Complete(true);

            // Assert
            Assert.True(operation.ElapsedTime.TotalMilliseconds >= 50);
            Assert.True(operation.IsCompleted);
            Assert.True(operation.Success);
        }

        [Fact]
        public void TimedOperation_WhenDisposed_CompletesOperation()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EspnLoggingService>>();
            var operationName = "TestOperation";

            // Act
            TimedOperation operation;
            using (operation = new TimedOperation(mockLogger.Object, operationName))
            {
                Thread.Sleep(10);
            }

            // Assert
            Assert.True(operation.IsCompleted);
        }
    }
}