using ESPNScrape.Configuration;
using ESPNScrape.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ESPNScrape.Tests.Services
{
    public class EspnMetricsServiceTests
    {
        private readonly Mock<ILogger<EspnMetricsService>> _mockLogger;
        private readonly Mock<IOptions<LoggingConfiguration>> _mockOptions;
        private readonly LoggingConfiguration _config;
        private readonly EspnMetricsService _metricsService;

        public EspnMetricsServiceTests()
        {
            _mockLogger = new Mock<ILogger<EspnMetricsService>>();
            _mockOptions = new Mock<IOptions<LoggingConfiguration>>();

            _config = new LoggingConfiguration
            {
                PerformanceMetrics = new PerformanceMetricsConfig
                {
                    TrackResponseTimes = true,
                    TrackCacheMetrics = true,
                    TrackMemoryMetrics = true,
                    MetricsRetentionPeriod = TimeSpan.FromDays(7)
                },
                BusinessMetrics = new BusinessMetricsConfig
                {
                    EnableBusinessMetrics = true
                },
                Alerting = new AlertingConfig
                {
                    EnableAlerting = true,
                    ErrorRateThreshold = 5.0,
                    ResponseTimeThresholdMs = 2000,
                    CacheHitRateThreshold = 80.0
                }
            };

            _mockOptions.Setup(x => x.Value).Returns(_config);
            _metricsService = new EspnMetricsService(_mockLogger.Object, _mockOptions.Object);
        }

        [Fact]
        public void RecordApiResponseTime_WithValidData_RecordsSuccessfully()
        {
            // Arrange
            var endpoint = "/api/test";
            var responseTime = TimeSpan.FromMilliseconds(500);
            const bool success = true;

            // Act
            _metricsService.RecordApiResponseTime(endpoint, responseTime, success);

            // Assert
            var metrics = _metricsService.GetCurrentMetrics();
            Assert.Contains($"api_response_time_{endpoint}", metrics.RawMetrics.Keys);
            Assert.Contains(endpoint, metrics.ResponseTimeMetrics.Keys);
            Assert.Equal(1, metrics.ResponseTimeMetrics[endpoint].TotalRequests);
        }

        [Fact]
        public void RecordApiResponseTime_WithMultipleCalls_AggregatesCorrectly()
        {
            // Arrange
            var endpoint = "/api/test";
            var responseTime1 = TimeSpan.FromMilliseconds(500);
            var responseTime2 = TimeSpan.FromMilliseconds(300);

            // Act
            _metricsService.RecordApiResponseTime(endpoint, responseTime1, true);
            _metricsService.RecordApiResponseTime(endpoint, responseTime2, true);

            // Assert
            var metrics = _metricsService.GetCurrentMetrics();
            var responseMetrics = metrics.ResponseTimeMetrics[endpoint];
            Assert.Equal(2, responseMetrics.TotalRequests);
            Assert.Equal(TimeSpan.FromMilliseconds(400), responseMetrics.AverageResponseTime);
            Assert.Equal(TimeSpan.FromMilliseconds(500), responseMetrics.MaxResponseTime);
        }

        [Fact]
        public void RecordApiResponseTime_WithFailures_CalculatesErrorRate()
        {
            // Arrange
            var endpoint = "/api/test";
            var responseTime = TimeSpan.FromMilliseconds(500);

            // Act
            _metricsService.RecordApiResponseTime(endpoint, responseTime, true);
            _metricsService.RecordApiResponseTime(endpoint, responseTime, false);
            _metricsService.RecordApiResponseTime(endpoint, responseTime, false);

            // Assert
            var metrics = _metricsService.GetCurrentMetrics();
            var responseMetrics = metrics.ResponseTimeMetrics[endpoint];
            Assert.Equal(3, responseMetrics.TotalRequests);
            Assert.Equal(66.67, responseMetrics.ErrorRate, 2); // 2/3 = 66.67%
        }

        [Fact]
        public void RecordCacheOperation_WithHit_RecordsSuccessfully()
        {
            // Arrange
            var operation = "GetPlayerStats";
            const bool hit = true;
            var duration = TimeSpan.FromMilliseconds(10);

            // Act
            _metricsService.RecordCacheOperation(operation, hit, duration);

            // Assert
            var metrics = _metricsService.GetCurrentMetrics();
            Assert.Contains($"cache_{operation}", metrics.RawMetrics.Keys);
            Assert.Contains(operation, metrics.CacheMetrics.Keys);
            Assert.Equal(100.0, metrics.CacheMetrics[operation].HitRate);
        }

        [Fact]
        public void RecordCacheOperation_WithMixedResults_CalculatesHitRate()
        {
            // Arrange
            var operation = "GetPlayerStats";
            var duration = TimeSpan.FromMilliseconds(10);

            // Act
            _metricsService.RecordCacheOperation(operation, true, duration);
            _metricsService.RecordCacheOperation(operation, true, duration);
            _metricsService.RecordCacheOperation(operation, false, duration);

            // Assert
            var metrics = _metricsService.GetCurrentMetrics();
            var cacheMetrics = metrics.CacheMetrics[operation];
            Assert.Equal(3, cacheMetrics.TotalOperations);
            Assert.Equal(66.67, cacheMetrics.HitRate, 2); // 2/3 = 66.67%
        }

        [Fact]
        public void RecordBusinessMetric_WithValidData_RecordsSuccessfully()
        {
            // Arrange
            var metricName = "players_processed";
            var value = 100.0;
            var tags = new Dictionary<string, string> { { "team", "patriots" } };

            // Act
            _metricsService.RecordBusinessMetric(metricName, value, tags);

            // Assert
            var metrics = _metricsService.GetCurrentMetrics();
            Assert.Contains($"business_{metricName}", metrics.RawMetrics.Keys);
            var metricData = metrics.RawMetrics[$"business_{metricName}"].First();
            Assert.Equal(value, metricData.Value);
            Assert.Equal("patriots", metricData.Tags["team"]);
        }

        [Fact]
        public void RecordBulkOperationMetrics_CalculatesCorrectMetrics()
        {
            // Arrange
            var operationType = "player_stats_collection";
            var itemsProcessed = 1000;
            var duration = TimeSpan.FromSeconds(10);
            var errorCount = 50;

            // Act
            _metricsService.RecordBulkOperationMetrics(operationType, itemsProcessed, duration, errorCount);

            // Assert
            var metrics = _metricsService.GetCurrentMetrics();

            // Check that all bulk operation metrics were recorded
            Assert.Contains($"business_bulk_operation_{operationType}_items_processed", metrics.RawMetrics.Keys);
            Assert.Contains($"business_bulk_operation_{operationType}_duration_seconds", metrics.RawMetrics.Keys);
            Assert.Contains($"business_bulk_operation_{operationType}_throughput", metrics.RawMetrics.Keys);
            Assert.Contains($"business_bulk_operation_{operationType}_error_rate", metrics.RawMetrics.Keys);

            // Verify calculated values
            var throughputMetric = metrics.RawMetrics[$"business_bulk_operation_{operationType}_throughput"].First();
            Assert.Equal(100.0, throughputMetric.Value); // 1000 items / 10 seconds = 100 items/sec

            var errorRateMetric = metrics.RawMetrics[$"business_bulk_operation_{operationType}_error_rate"].First();
            Assert.Equal(5.0, errorRateMetric.Value); // 50/1000 * 100 = 5%
        }

        [Fact]
        public void RecordMemoryUsage_RecordsSuccessfully()
        {
            // Arrange
            var bytesUsed = 1024L * 1024L * 100L; // 100 MB

            // Act
            _metricsService.RecordMemoryUsage(bytesUsed);

            // Assert
            var metrics = _metricsService.GetCurrentMetrics();
            Assert.Contains("memory_usage", metrics.RawMetrics.Keys);
            var memoryMetric = metrics.RawMetrics["memory_usage"].First();
            Assert.Equal(bytesUsed, memoryMetric.Value);
            Assert.Equal("bytes", memoryMetric.Tags["unit"]);
        }

        [Fact]
        public void RecordHealthCheck_RecordsSuccessfully()
        {
            // Arrange
            var checkName = "espn_api";
            const bool healthy = true;
            var duration = TimeSpan.FromMilliseconds(150);

            // Act
            _metricsService.RecordHealthCheck(checkName, healthy, duration);

            // Assert
            var metrics = _metricsService.GetCurrentMetrics();
            Assert.Contains($"health_check_{checkName}", metrics.RawMetrics.Keys);
            var healthMetric = metrics.RawMetrics[$"health_check_{checkName}"].First();
            Assert.Equal(1, healthMetric.Value); // 1 for healthy
            Assert.Equal(checkName, healthMetric.Tags["check_name"]);
        }

        [Fact]
        public void GetMetrics_WithTimeRange_FiltersCorrectly()
        {
            // Arrange
            var endpoint = "/api/test";
            var responseTime = TimeSpan.FromMilliseconds(500);
            var now = DateTime.UtcNow;

            _metricsService.RecordApiResponseTime(endpoint, responseTime, true);
            Thread.Sleep(10); // Ensure different timestamps
            _metricsService.RecordApiResponseTime(endpoint, responseTime, true);

            var from = now.AddMinutes(-1);
            var to = now.AddMinutes(1);

            // Act
            var metrics = _metricsService.GetMetrics(from, to);

            // Assert
            Assert.NotNull(metrics.FromTime);
            Assert.NotNull(metrics.ToTime);
            Assert.Equal(from, metrics.FromTime);
            Assert.Equal(to, metrics.ToTime);
            Assert.Contains($"api_response_time_{endpoint}", metrics.RawMetrics.Keys);
        }

        [Fact]
        public void ResetMetrics_ClearsAllMetrics()
        {
            // Arrange
            _metricsService.RecordApiResponseTime("/api/test", TimeSpan.FromMilliseconds(500), true);
            _metricsService.RecordCacheOperation("test", true, TimeSpan.FromMilliseconds(10));

            // Act
            _metricsService.ResetMetrics();

            // Assert
            var metrics = _metricsService.GetCurrentMetrics();
            Assert.Empty(metrics.RawMetrics);
            Assert.Empty(metrics.ResponseTimeMetrics);
            Assert.Empty(metrics.CacheMetrics);
        }

        [Fact]
        public void CheckAlertConditions_WithHighErrorRate_TriggersAlert()
        {
            // Arrange
            var endpoint = "/api/test";
            var responseTime = TimeSpan.FromMilliseconds(500);

            // Create high error rate (> 5%)
            for (int i = 0; i < 10; i++)
            {
                _metricsService.RecordApiResponseTime(endpoint, responseTime, false); // All failures
            }

            // Act
            var alerts = _metricsService.CheckAlertConditions();

            // Assert
            Assert.NotEmpty(alerts);
            var errorRateAlert = alerts.FirstOrDefault(a => a.Type == "ErrorRate");
            Assert.NotNull(errorRateAlert);
            Assert.Equal(endpoint, errorRateAlert.Component);
            Assert.True(errorRateAlert.CurrentValue > _config.Alerting.ErrorRateThreshold);
        }

        [Fact]
        public void CheckAlertConditions_WithSlowResponseTime_TriggersAlert()
        {
            // Arrange
            var endpoint = "/api/test";
            var slowResponseTime = TimeSpan.FromMilliseconds(3000); // > 2000ms threshold

            _metricsService.RecordApiResponseTime(endpoint, slowResponseTime, true);

            // Act
            var alerts = _metricsService.CheckAlertConditions();

            // Assert
            Assert.NotEmpty(alerts);
            var responseTimeAlert = alerts.FirstOrDefault(a => a.Type == "ResponseTime");
            Assert.NotNull(responseTimeAlert);
            Assert.Equal(endpoint, responseTimeAlert.Component);
            Assert.True(responseTimeAlert.CurrentValue > _config.Alerting.ResponseTimeThresholdMs);
        }

        [Fact]
        public void CheckAlertConditions_WithLowCacheHitRate_TriggersAlert()
        {
            // Arrange
            var operation = "GetPlayerStats";

            // Create low cache hit rate (< 80%)
            _metricsService.RecordCacheOperation(operation, true, TimeSpan.FromMilliseconds(10));   // 1 hit
            _metricsService.RecordCacheOperation(operation, false, TimeSpan.FromMilliseconds(10));  // 1 miss
            _metricsService.RecordCacheOperation(operation, false, TimeSpan.FromMilliseconds(10));  // 1 miss
            _metricsService.RecordCacheOperation(operation, false, TimeSpan.FromMilliseconds(10));  // 1 miss
            _metricsService.RecordCacheOperation(operation, false, TimeSpan.FromMilliseconds(10));  // 1 miss
            // Hit rate = 1/5 = 20% (< 80% threshold)

            // Act
            var alerts = _metricsService.CheckAlertConditions();

            // Assert
            Assert.NotEmpty(alerts);
            var cacheAlert = alerts.FirstOrDefault(a => a.Type == "CacheHitRate");
            Assert.NotNull(cacheAlert);
            Assert.Equal(operation, cacheAlert.Component);
            Assert.True(cacheAlert.CurrentValue < _config.Alerting.CacheHitRateThreshold);
        }

        [Fact]
        public void CheckAlertConditions_WithNoIssues_ReturnsNoAlerts()
        {
            // Arrange
            var endpoint = "/api/test";
            var responseTime = TimeSpan.FromMilliseconds(500); // Good response time

            // Record good metrics
            _metricsService.RecordApiResponseTime(endpoint, responseTime, true);
            _metricsService.RecordCacheOperation("test", true, TimeSpan.FromMilliseconds(10));

            // Act
            var alerts = _metricsService.CheckAlertConditions();

            // Assert
            Assert.Empty(alerts);
        }

        [Fact]
        public void CheckAlertConditions_WithAlertingDisabled_ReturnsNoAlerts()
        {
            // Arrange
            _config.Alerting.EnableAlerting = false;
            var endpoint = "/api/test";
            var slowResponseTime = TimeSpan.FromMilliseconds(5000); // Very slow

            _metricsService.RecordApiResponseTime(endpoint, slowResponseTime, false);

            // Act
            var alerts = _metricsService.CheckAlertConditions();

            // Assert
            Assert.Empty(alerts);
        }
    }

    public class ResponseTimeMetricsTests
    {
        [Fact]
        public void ResponseTimeMetrics_WithSingleSample_CalculatesCorrectly()
        {
            // Arrange
            var responseTime = TimeSpan.FromMilliseconds(500);

            // Act
            var metrics = new ResponseTimeMetrics(responseTime, true);

            // Assert
            Assert.Equal(responseTime, metrics.AverageResponseTime);
            Assert.Equal(responseTime, metrics.MaxResponseTime);
            Assert.Equal(1, metrics.TotalRequests);
            Assert.Equal(0, metrics.ErrorRate);
        }

        [Fact]
        public void ResponseTimeMetrics_WithMultipleSamples_CalculatesAverageCorrectly()
        {
            // Arrange
            var metrics = new ResponseTimeMetrics(TimeSpan.FromMilliseconds(400), true);

            // Act
            metrics.AddSample(TimeSpan.FromMilliseconds(600), true);
            metrics.AddSample(TimeSpan.FromMilliseconds(500), false);

            // Assert
            Assert.Equal(TimeSpan.FromMilliseconds(500), metrics.AverageResponseTime);
            Assert.Equal(TimeSpan.FromMilliseconds(600), metrics.MaxResponseTime);
            Assert.Equal(3, metrics.TotalRequests);
            Assert.Equal(33.33, metrics.ErrorRate, 2); // 1/3 = 33.33%
        }

        [Fact]
        public void ResponseTimeMetrics_Clone_CreatesIndependentCopy()
        {
            // Arrange
            var original = new ResponseTimeMetrics(TimeSpan.FromMilliseconds(500), true);
            original.AddSample(TimeSpan.FromMilliseconds(300), false);

            // Act
            var clone = original.Clone();
            clone.AddSample(TimeSpan.FromMilliseconds(700), true);

            // Assert
            Assert.Equal(2, original.TotalRequests);
            Assert.Equal(3, clone.TotalRequests);
            Assert.NotEqual(original.AverageResponseTime, clone.AverageResponseTime);
        }
    }

    public class CacheMetricsTests
    {
        [Fact]
        public void CacheMetrics_WithSingleOperation_CalculatesCorrectly()
        {
            // Arrange & Act
            var metrics = new CacheMetrics(true, TimeSpan.FromMilliseconds(10));

            // Assert
            Assert.Equal(100.0, metrics.HitRate);
            Assert.Equal(1, metrics.TotalOperations);
            Assert.Equal(TimeSpan.FromMilliseconds(10), metrics.AverageDuration);
        }

        [Fact]
        public void CacheMetrics_WithMultipleOperations_CalculatesHitRateCorrectly()
        {
            // Arrange
            var metrics = new CacheMetrics(true, TimeSpan.FromMilliseconds(10));

            // Act
            metrics.AddOperation(false, TimeSpan.FromMilliseconds(20));
            metrics.AddOperation(true, TimeSpan.FromMilliseconds(5));

            // Assert
            Assert.Equal(66.67, metrics.HitRate, 2); // 2/3 = 66.67%
            Assert.Equal(3, metrics.TotalOperations);
            Assert.True(Math.Abs((metrics.AverageDuration - TimeSpan.FromMilliseconds(11.67)).TotalMilliseconds) < 1);
        }

        [Fact]
        public void CacheMetrics_Clone_CreatesIndependentCopy()
        {
            // Arrange
            var original = new CacheMetrics(true, TimeSpan.FromMilliseconds(10));
            original.AddOperation(false, TimeSpan.FromMilliseconds(20));

            // Act
            var clone = original.Clone();
            clone.AddOperation(true, TimeSpan.FromMilliseconds(5));

            // Assert
            Assert.Equal(2, original.TotalOperations);
            Assert.Equal(3, clone.TotalOperations);
            Assert.NotEqual(original.HitRate, clone.HitRate);
        }
    }
}