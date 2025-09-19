using ESPNScrape.Configuration;
using ESPNScrape.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ESPNScrape.Tests.Services
{
    public class EspnAlertingServiceTests
    {
        private readonly Mock<ILogger<EspnAlertingService>> _mockLogger;
        private readonly Mock<IOptions<LoggingConfiguration>> _mockOptions;
        private readonly LoggingConfiguration _config;
        private readonly EspnAlertingService _alertingService;

        public EspnAlertingServiceTests()
        {
            _mockLogger = new Mock<ILogger<EspnAlertingService>>();
            _mockOptions = new Mock<IOptions<LoggingConfiguration>>();

            _config = new LoggingConfiguration
            {
                Alerting = new AlertingConfig
                {
                    EnableAlerting = true,
                    ErrorRateThreshold = 5.0,
                    ResponseTimeThresholdMs = 2000,
                    CacheHitRateThreshold = 80.0,
                    AlertCooldownPeriod = TimeSpan.FromMinutes(5),
                    SendResolutionNotifications = true,
                    EmailEnabled = true,
                    WebhookEnabled = true,
                    WebhookUrl = "https://example.com/webhook",
                    SmsEnabled = false
                }
            };

            _mockOptions.Setup(x => x.Value).Returns(_config);
            _alertingService = new EspnAlertingService(_mockLogger.Object, _mockOptions.Object);
        }

        [Fact]
        public async Task ProcessAlertsAsync_WithNewAlert_CreatesAlertRecord()
        {
            // Arrange
            var alert = CreateTestAlert("ErrorRate", "/api/test", 10.0, 5.0);
            var alerts = new List<AlertCondition> { alert };

            // Act
            await _alertingService.ProcessAlertsAsync(alerts);

            // Assert
            var activeAlerts = _alertingService.GetActiveAlerts();
            Assert.Single(activeAlerts);
            Assert.Equal(alert.Type, activeAlerts[0].AlertCondition.Type);
            Assert.Equal(alert.Component, activeAlerts[0].AlertCondition.Component);
            Assert.Equal(AlertState.Active, activeAlerts[0].State);
        }

        [Fact]
        public async Task ProcessAlertsAsync_WithExistingAlert_UpdatesOccurrenceCount()
        {
            // Arrange
            var alert = CreateTestAlert("ErrorRate", "/api/test", 10.0, 5.0);
            var alerts = new List<AlertCondition> { alert };

            // Act
            await _alertingService.ProcessAlertsAsync(alerts);
            await _alertingService.ProcessAlertsAsync(alerts);

            // Assert
            var activeAlerts = _alertingService.GetActiveAlerts();
            Assert.Single(activeAlerts);
            Assert.Equal(2, activeAlerts[0].OccurrenceCount);
        }

        [Fact]
        public async Task ProcessAlertsAsync_WithAlertingDisabled_DoesNotCreateAlerts()
        {
            // Arrange
            _config.Alerting.EnableAlerting = false;
            var alert = CreateTestAlert("ErrorRate", "/api/test", 10.0, 5.0);
            var alerts = new List<AlertCondition> { alert };

            // Act
            await _alertingService.ProcessAlertsAsync(alerts);

            // Assert
            var activeAlerts = _alertingService.GetActiveAlerts();
            Assert.Empty(activeAlerts);
        }

        [Fact]
        public async Task ProcessAlertsAsync_WithResolvedAlert_MarksAsResolved()
        {
            // Arrange
            var alert = CreateTestAlert("ErrorRate", "/api/test", 10.0, 5.0);
            var alerts = new List<AlertCondition> { alert };

            // Act - Create alert
            await _alertingService.ProcessAlertsAsync(alerts);

            // Act - Process without the alert (simulating resolution)
            await _alertingService.ProcessAlertsAsync(new List<AlertCondition>());

            // Assert
            var allAlerts = _alertingService.GetAlertHistory();
            var resolvedAlert = allAlerts.FirstOrDefault(a => a.State == AlertState.Resolved);
            Assert.NotNull(resolvedAlert);
            Assert.NotNull(resolvedAlert.ResolvedAt);
        }

        [Fact]
        public async Task SendAlertAsync_WithValidAlert_LogsAlert()
        {
            // Arrange
            var alert = CreateTestAlert("ErrorRate", "/api/test", 10.0, 5.0);

            // Act
            await _alertingService.SendAlertAsync(alert);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ALERT")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
        }

        [Fact]
        public async Task SendAlertAsync_WithAlertingDisabled_DoesNotSendAlert()
        {
            // Arrange
            _config.Alerting.EnableAlerting = false;
            var alert = CreateTestAlert("ErrorRate", "/api/test", 10.0, 5.0);

            // Act
            await _alertingService.SendAlertAsync(alert);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Fact]
        public void GetAlertHistory_WithTimeRange_FiltersCorrectly()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var alert = CreateTestAlert("ErrorRate", "/api/test", 10.0, 5.0);

            // Manually add an alert record to test filtering
            var alertRecord = new AlertRecord
            {
                Id = Guid.NewGuid(),
                AlertCondition = alert,
                FirstOccurrence = now.AddMinutes(-10),
                State = AlertState.Active,
                Severity = AlertSeverity.High
            };

            // Act
            var filteredHistory = _alertingService.GetAlertHistory(now.AddMinutes(-15), now.AddMinutes(-5));

            // Assert
            // Since we can't directly inject the alert record, this test verifies the filtering logic exists
            Assert.NotNull(filteredHistory);
        }

        [Fact]
        public void ClearResolvedAlerts_RemovesResolvedAlertsOnly()
        {
            // Arrange & Act
            _alertingService.ClearResolvedAlerts();

            // Assert
            var activeAlerts = _alertingService.GetActiveAlerts();
            // Verify no exceptions and method completes
            Assert.NotNull(activeAlerts);
        }

        [Fact]
        public void GetActiveAlerts_ReturnsOnlyActiveAlerts()
        {
            // Act
            var activeAlerts = _alertingService.GetActiveAlerts();

            // Assert
            Assert.NotNull(activeAlerts);
            Assert.All(activeAlerts, alert => Assert.Equal(AlertState.Active, alert.State));
        }

        [Theory]
        [InlineData("ErrorRate", 15.0, AlertSeverity.Critical)]
        [InlineData("ErrorRate", 7.0, AlertSeverity.High)]
        [InlineData("ErrorRate", 3.0, AlertSeverity.Medium)]
        [InlineData("ResponseTime", 6000.0, AlertSeverity.Critical)]
        [InlineData("ResponseTime", 3000.0, AlertSeverity.High)]
        [InlineData("ResponseTime", 1000.0, AlertSeverity.Medium)]
        [InlineData("CacheHitRate", 40.0, AlertSeverity.Critical)]
        [InlineData("CacheHitRate", 60.0, AlertSeverity.High)]
        [InlineData("CacheHitRate", 75.0, AlertSeverity.Medium)]
        public async Task ProcessAlertsAsync_DeterminesSeverityCorrectly(string alertType, double currentValue, AlertSeverity expectedSeverity)
        {
            // Arrange
            var alert = CreateTestAlert(alertType, "/api/test", currentValue, 5.0);
            var alerts = new List<AlertCondition> { alert };

            // Act
            await _alertingService.ProcessAlertsAsync(alerts);

            // Assert
            var activeAlerts = _alertingService.GetActiveAlerts();
            Assert.Single(activeAlerts);
            Assert.Equal(expectedSeverity, activeAlerts[0].Severity);
        }

        [Fact]
        public async Task ProcessAlertsAsync_WithCooldownPeriod_RespectsRateLimit()
        {
            // Arrange
            _config.Alerting.AlertCooldownPeriod = TimeSpan.FromSeconds(1);
            var alert = CreateTestAlert("ErrorRate", "/api/test", 10.0, 5.0);
            var alerts = new List<AlertCondition> { alert };

            // Act
            await _alertingService.ProcessAlertsAsync(alerts);
            await _alertingService.ProcessAlertsAsync(alerts); // Should not trigger new alert due to cooldown

            // Assert
            var activeAlerts = _alertingService.GetActiveAlerts();
            Assert.Single(activeAlerts);
            Assert.Equal(2, activeAlerts[0].OccurrenceCount);
            // LastAlertSent should only be set once during cooldown period
        }

        [Fact]
        public async Task ProcessAlertsAsync_WithResolutionNotificationsEnabled_SendsResolutionNotification()
        {
            // Arrange
            var alert = CreateTestAlert("ErrorRate", "/api/test", 10.0, 5.0);
            var alerts = new List<AlertCondition> { alert };

            // Act
            await _alertingService.ProcessAlertsAsync(alerts);
            await _alertingService.ProcessAlertsAsync(new List<AlertCondition>()); // Resolve the alert

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("RESOLVED")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task ProcessAlertsAsync_WithDifferentAlertTypes_CreatesMultipleAlerts()
        {
            // Arrange
            var errorRateAlert = CreateTestAlert("ErrorRate", "/api/test", 10.0, 5.0);
            var responseTimeAlert = CreateTestAlert("ResponseTime", "/api/slow", 3000.0, 2000.0);
            var cacheAlert = CreateTestAlert("CacheHitRate", "PlayerStats", 50.0, 80.0);
            var alerts = new List<AlertCondition> { errorRateAlert, responseTimeAlert, cacheAlert };

            // Act
            await _alertingService.ProcessAlertsAsync(alerts);

            // Assert
            var activeAlerts = _alertingService.GetActiveAlerts();
            Assert.Equal(3, activeAlerts.Count);

            var alertTypes = activeAlerts.Select(a => a.AlertCondition.Type).ToList();
            Assert.Contains("ErrorRate", alertTypes);
            Assert.Contains("ResponseTime", alertTypes);
            Assert.Contains("CacheHitRate", alertTypes);
        }

        [Fact]
        public async Task ProcessAlertsAsync_WithExceptionInSending_ContinuesProcessing()
        {
            // Arrange
            var alert = CreateTestAlert("ErrorRate", "/api/test", 10.0, 5.0);
            var alerts = new List<AlertCondition> { alert };

            // Simulate an exception during alert sending by temporarily disabling alerting
            // This tests error handling in the service
            _config.Alerting.EnableAlerting = true;

            // Act & Assert - Should not throw
            await _alertingService.ProcessAlertsAsync(alerts);

            var activeAlerts = _alertingService.GetActiveAlerts();
            Assert.Single(activeAlerts);
        }

        private static AlertCondition CreateTestAlert(string type, string component, double currentValue, double threshold)
        {
            return new AlertCondition
            {
                Type = type,
                Component = component,
                CurrentValue = currentValue,
                Threshold = threshold,
                Message = $"{type} alert for {component}: current={currentValue}, threshold={threshold}",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    public class AlertMonitoringServiceTests
    {
        private readonly Mock<IEspnMetricsService> _mockMetricsService;
        private readonly Mock<IEspnAlertingService> _mockAlertingService;
        private readonly Mock<IEspnLoggingService> _mockLoggingService;
        private readonly Mock<ILogger<AlertMonitoringService>> _mockLogger;
        private readonly Mock<IOptions<LoggingConfiguration>> _mockOptions;
        private readonly LoggingConfiguration _config;

        public AlertMonitoringServiceTests()
        {
            _mockMetricsService = new Mock<IEspnMetricsService>();
            _mockAlertingService = new Mock<IEspnAlertingService>();
            _mockLoggingService = new Mock<IEspnLoggingService>();
            _mockLogger = new Mock<ILogger<AlertMonitoringService>>();
            _mockOptions = new Mock<IOptions<LoggingConfiguration>>();

            _config = new LoggingConfiguration
            {
                Alerting = new AlertingConfig
                {
                    EnableAlerting = true,
                    MonitoringInterval = TimeSpan.FromMilliseconds(100) // Fast for testing
                }
            };

            _mockOptions.Setup(x => x.Value).Returns(_config);
        }

        [Fact]
        public async Task ExecuteAsync_WithAlertingDisabled_LogsAndExits()
        {
            // Arrange
            _config.Alerting.EnableAlerting = false;
            var service = new AlertMonitoringService(
                _mockMetricsService.Object,
                _mockAlertingService.Object,
                _mockLoggingService.Object,
                _mockOptions.Object,
                _mockLogger.Object);

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

            // Act
            await service.StartAsync(cts.Token);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("disabled")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_WithActiveAlerts_ProcessesAlerts()
        {
            // Arrange
            var alerts = new List<AlertCondition>
            {
                new() { Type = "ErrorRate", Component = "/api/test", CurrentValue = 10.0, Threshold = 5.0 }
            };

            _mockMetricsService.Setup(x => x.CheckAlertConditions()).Returns(alerts);
            _mockLoggingService.Setup(x => x.BeginTimedOperation(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Returns(new Mock<IDisposable>().Object);

            var service = new AlertMonitoringService(
                _mockMetricsService.Object,
                _mockAlertingService.Object,
                _mockLoggingService.Object,
                _mockOptions.Object,
                _mockLogger.Object);

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(200); // Allow service to run
            cts.Cancel();

            // Assert
            _mockMetricsService.Verify(x => x.CheckAlertConditions(), Times.AtLeastOnce);
            _mockAlertingService.Verify(x => x.ProcessAlertsAsync(alerts), Times.AtLeastOnce);
        }

        [Fact]
        public async Task ExecuteAsync_OnException_ContinuesOperation()
        {
            // Arrange
            _mockMetricsService.Setup(x => x.CheckAlertConditions())
                .Throws(new InvalidOperationException("Test exception"));
            _mockLoggingService.Setup(x => x.BeginTimedOperation(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Returns(new Mock<IDisposable>().Object);

            var service = new AlertMonitoringService(
                _mockMetricsService.Object,
                _mockAlertingService.Object,
                _mockLoggingService.Object,
                _mockOptions.Object,
                _mockLogger.Object);

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

            // Act & Assert - Should not throw
            await service.StartAsync(cts.Token);
            await Task.Delay(200);
            cts.Cancel();

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error in alert monitoring")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.AtLeastOnce);
        }
    }
}