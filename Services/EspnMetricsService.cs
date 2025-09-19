using ESPNScrape.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ESPNScrape.Services
{
    /// <summary>
    /// Service for collecting and managing performance metrics
    /// </summary>
    public interface IEspnMetricsService
    {
        /// <summary>
        /// Record API response time
        /// </summary>
        void RecordApiResponseTime(string endpoint, TimeSpan responseTime, bool success);

        /// <summary>
        /// Record cache operation
        /// </summary>
        void RecordCacheOperation(string operation, bool hit, TimeSpan? duration = null);

        /// <summary>
        /// Record business metric
        /// </summary>
        void RecordBusinessMetric(string metricName, double value, Dictionary<string, string>? tags = null);

        /// <summary>
        /// Record bulk operation metrics
        /// </summary>
        void RecordBulkOperationMetrics(string operationType, int itemsProcessed, TimeSpan duration, int errorCount);

        /// <summary>
        /// Record memory usage
        /// </summary>
        void RecordMemoryUsage(long bytesUsed);

        /// <summary>
        /// Record health check result
        /// </summary>
        void RecordHealthCheck(string checkName, bool healthy, TimeSpan duration);

        /// <summary>
        /// Get current metrics snapshot
        /// </summary>
        MetricsSnapshot GetCurrentMetrics();

        /// <summary>
        /// Get metrics for a specific time range
        /// </summary>
        MetricsSnapshot GetMetrics(DateTime from, DateTime to);

        /// <summary>
        /// Reset all metrics
        /// </summary>
        void ResetMetrics();

        /// <summary>
        /// Check if any alert thresholds are exceeded
        /// </summary>
        List<AlertCondition> CheckAlertConditions();
    }

    /// <summary>
    /// Implementation of ESPN metrics service
    /// </summary>
    public class EspnMetricsService : IEspnMetricsService
    {
        private readonly ILogger<EspnMetricsService> _logger;
        private readonly LoggingConfiguration _config;
        private readonly ConcurrentDictionary<string, List<MetricData>> _metrics = new();
        private readonly ConcurrentDictionary<string, ResponseTimeMetrics> _responseTimeMetrics = new();
        private readonly ConcurrentDictionary<string, CacheMetrics> _cacheMetrics = new();
        private readonly object _lockObject = new();

        public EspnMetricsService(ILogger<EspnMetricsService> logger, IOptions<LoggingConfiguration> config)
        {
            _logger = logger;
            _config = config.Value;
        }

        public void RecordApiResponseTime(string endpoint, TimeSpan responseTime, bool success)
        {
            var metricKey = $"api_response_time_{endpoint}";
            var metric = new MetricData
            {
                Timestamp = DateTime.UtcNow,
                Value = responseTime.TotalMilliseconds,
                Tags = new Dictionary<string, string>
                {
                    { "endpoint", endpoint },
                    { "success", success.ToString() }
                }
            };

            AddMetric(metricKey, metric);

            // Update response time aggregations
            _responseTimeMetrics.AddOrUpdate(endpoint,
                new ResponseTimeMetrics(responseTime, success),
                (key, existing) => existing.AddSample(responseTime, success));

            if (_config.PerformanceMetrics.TrackResponseTimes)
            {
                _logger.LogDebug("Recorded API response time for {Endpoint}: {ResponseTime}ms (Success: {Success})",
                    endpoint, responseTime.TotalMilliseconds, success);
            }
        }

        public void RecordCacheOperation(string operation, bool hit, TimeSpan? duration = null)
        {
            var metricKey = $"cache_{operation}";
            var metric = new MetricData
            {
                Timestamp = DateTime.UtcNow,
                Value = hit ? 1 : 0,
                Tags = new Dictionary<string, string>
                {
                    { "operation", operation },
                    { "hit", hit.ToString() },
                    { "duration", duration?.TotalMilliseconds.ToString() ?? "0" }
                }
            };

            AddMetric(metricKey, metric);

            // Update cache aggregations
            _cacheMetrics.AddOrUpdate(operation,
                new CacheMetrics(hit, duration),
                (key, existing) => existing.AddOperation(hit, duration));

            if (_config.PerformanceMetrics.TrackCacheMetrics)
            {
                var hitMiss = hit ? "HIT" : "MISS";
                var durationInfo = duration.HasValue ? $" in {duration.Value.TotalMilliseconds}ms" : "";
                _logger.LogDebug("Recorded cache {Operation}: {HitMiss}{DurationInfo}", operation, hitMiss, durationInfo);
            }
        }

        public void RecordBusinessMetric(string metricName, double value, Dictionary<string, string>? tags = null)
        {
            var metricKey = $"business_{metricName}";
            var metric = new MetricData
            {
                Timestamp = DateTime.UtcNow,
                Value = value,
                Tags = tags ?? new Dictionary<string, string>()
            };

            metric.Tags["metric_type"] = "business";
            AddMetric(metricKey, metric);

            if (_config.BusinessMetrics.EnableBusinessMetrics)
            {
                _logger.LogInformation("Recorded business metric {MetricName}: {Value}", metricName, value);
            }
        }

        public void RecordBulkOperationMetrics(string operationType, int itemsProcessed, TimeSpan duration, int errorCount)
        {
            var throughput = duration.TotalSeconds > 0 ? itemsProcessed / duration.TotalSeconds : 0;
            var errorRate = itemsProcessed > 0 ? (double)errorCount / itemsProcessed * 100 : 0;

            RecordBusinessMetric($"bulk_operation_{operationType}_items_processed", itemsProcessed);
            RecordBusinessMetric($"bulk_operation_{operationType}_duration_seconds", duration.TotalSeconds);
            RecordBusinessMetric($"bulk_operation_{operationType}_throughput", throughput);
            RecordBusinessMetric($"bulk_operation_{operationType}_error_rate", errorRate);

            _logger.LogInformation("Bulk operation {OperationType} metrics: {ItemsProcessed} items, {Duration}s, {Throughput:F2} items/sec, {ErrorRate:F2}% errors",
                operationType, itemsProcessed, duration.TotalSeconds, throughput, errorRate);
        }

        public void RecordMemoryUsage(long bytesUsed)
        {
            var metricKey = "memory_usage";
            var metric = new MetricData
            {
                Timestamp = DateTime.UtcNow,
                Value = bytesUsed,
                Tags = new Dictionary<string, string>
                {
                    { "unit", "bytes" }
                }
            };

            AddMetric(metricKey, metric);

            if (_config.PerformanceMetrics.TrackMemoryMetrics)
            {
                var megabytes = bytesUsed / (1024.0 * 1024.0);
                _logger.LogDebug("Recorded memory usage: {MemoryUsage:F2} MB", megabytes);
            }
        }

        public void RecordHealthCheck(string checkName, bool healthy, TimeSpan duration)
        {
            var metricKey = $"health_check_{checkName}";
            var metric = new MetricData
            {
                Timestamp = DateTime.UtcNow,
                Value = healthy ? 1 : 0,
                Tags = new Dictionary<string, string>
                {
                    { "check_name", checkName },
                    { "healthy", healthy.ToString() },
                    { "duration", duration.TotalMilliseconds.ToString() }
                }
            };

            AddMetric(metricKey, metric);

            _logger.LogDebug("Recorded health check {CheckName}: {Status} in {Duration}ms",
                checkName, healthy ? "HEALTHY" : "UNHEALTHY", duration.TotalMilliseconds);
        }

        public MetricsSnapshot GetCurrentMetrics()
        {
            lock (_lockObject)
            {
                return new MetricsSnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    ResponseTimeMetrics = _responseTimeMetrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone()),
                    CacheMetrics = _cacheMetrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone()),
                    RawMetrics = _metrics.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.ToList())
                };
            }
        }

        public MetricsSnapshot GetMetrics(DateTime from, DateTime to)
        {
            lock (_lockObject)
            {
                var filteredMetrics = new Dictionary<string, List<MetricData>>();

                foreach (var kvp in _metrics)
                {
                    var filteredData = kvp.Value
                        .Where(m => m.Timestamp >= from && m.Timestamp <= to)
                        .ToList();

                    if (filteredData.Any())
                    {
                        filteredMetrics[kvp.Key] = filteredData;
                    }
                }

                return new MetricsSnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    FromTime = from,
                    ToTime = to,
                    RawMetrics = filteredMetrics
                };
            }
        }

        public void ResetMetrics()
        {
            lock (_lockObject)
            {
                _metrics.Clear();
                _responseTimeMetrics.Clear();
                _cacheMetrics.Clear();
                _logger.LogInformation("All metrics have been reset");
            }
        }

        public List<AlertCondition> CheckAlertConditions()
        {
            var alerts = new List<AlertCondition>();
            var config = _config.Alerting;

            if (!config.EnableAlerting)
                return alerts;

            // Check error rates
            foreach (var responseMetric in _responseTimeMetrics)
            {
                if (responseMetric.Value.ErrorRate > config.ErrorRateThreshold)
                {
                    alerts.Add(new AlertCondition
                    {
                        Type = "ErrorRate",
                        Component = responseMetric.Key,
                        CurrentValue = responseMetric.Value.ErrorRate,
                        Threshold = config.ErrorRateThreshold,
                        Message = $"Error rate for {responseMetric.Key} is {responseMetric.Value.ErrorRate:F2}% (threshold: {config.ErrorRateThreshold}%)"
                    });
                }

                // Check response times
                if (responseMetric.Value.AverageResponseTime.TotalMilliseconds > config.ResponseTimeThresholdMs)
                {
                    alerts.Add(new AlertCondition
                    {
                        Type = "ResponseTime",
                        Component = responseMetric.Key,
                        CurrentValue = responseMetric.Value.AverageResponseTime.TotalMilliseconds,
                        Threshold = config.ResponseTimeThresholdMs,
                        Message = $"Average response time for {responseMetric.Key} is {responseMetric.Value.AverageResponseTime.TotalMilliseconds:F0}ms (threshold: {config.ResponseTimeThresholdMs}ms)"
                    });
                }
            }

            // Check cache hit rates
            foreach (var cacheMetric in _cacheMetrics)
            {
                if (cacheMetric.Value.HitRate < config.CacheHitRateThreshold)
                {
                    alerts.Add(new AlertCondition
                    {
                        Type = "CacheHitRate",
                        Component = cacheMetric.Key,
                        CurrentValue = cacheMetric.Value.HitRate,
                        Threshold = config.CacheHitRateThreshold,
                        Message = $"Cache hit rate for {cacheMetric.Key} is {cacheMetric.Value.HitRate:F2}% (threshold: {config.CacheHitRateThreshold}%)"
                    });
                }
            }

            return alerts;
        }

        private void AddMetric(string key, MetricData metric)
        {
            _metrics.AddOrUpdate(key,
                new List<MetricData> { metric },
                (k, existing) =>
                {
                    lock (existing)
                    {
                        existing.Add(metric);

                        // Clean up old metrics
                        var cutoff = DateTime.UtcNow - _config.PerformanceMetrics.MetricsRetentionPeriod;
                        existing.RemoveAll(m => m.Timestamp < cutoff);

                        return existing;
                    }
                });
        }
    }

    /// <summary>
    /// Represents a single metric data point
    /// </summary>
    public class MetricData
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
    }

    /// <summary>
    /// Aggregated response time metrics
    /// </summary>
    public class ResponseTimeMetrics
    {
        private readonly List<TimeSpan> _responseTimes = new();
        private int _successCount;
        private int _errorCount;

        public ResponseTimeMetrics(TimeSpan initialResponseTime, bool success)
        {
            lock (_responseTimes)
            {
                _responseTimes.Add(initialResponseTime);
                if (success) _successCount++; else _errorCount++;
            }
        }

        public ResponseTimeMetrics AddSample(TimeSpan responseTime, bool success)
        {
            lock (_responseTimes)
            {
                _responseTimes.Add(responseTime);
                if (success) _successCount++; else _errorCount++;
                return this;
            }
        }

        public TimeSpan AverageResponseTime
        {
            get
            {
                lock (_responseTimes)
                {
                    if (!_responseTimes.Any()) return TimeSpan.Zero;
                    var totalMs = _responseTimes.Sum(rt => rt.TotalMilliseconds);
                    return TimeSpan.FromMilliseconds(totalMs / _responseTimes.Count);
                }
            }
        }

        public TimeSpan MaxResponseTime
        {
            get
            {
                lock (_responseTimes)
                {
                    return _responseTimes.Any() ? _responseTimes.Max() : TimeSpan.Zero;
                }
            }
        }

        public double ErrorRate
        {
            get
            {
                var total = _successCount + _errorCount;
                return total > 0 ? (double)_errorCount / total * 100 : 0;
            }
        }

        public int TotalRequests => _successCount + _errorCount;

        public ResponseTimeMetrics Clone()
        {
            lock (_responseTimes)
            {
                var clone = new ResponseTimeMetrics(TimeSpan.Zero, true);
                clone._responseTimes.Clear();
                clone._responseTimes.AddRange(_responseTimes);
                clone._successCount = _successCount;
                clone._errorCount = _errorCount;
                return clone;
            }
        }
    }

    /// <summary>
    /// Aggregated cache metrics
    /// </summary>
    public class CacheMetrics
    {
        private int _hits;
        private int _misses;
        private readonly List<TimeSpan> _durations = new();

        public CacheMetrics(bool hit, TimeSpan? duration)
        {
            if (hit) _hits++; else _misses++;
            if (duration.HasValue) _durations.Add(duration.Value);
        }

        public CacheMetrics AddOperation(bool hit, TimeSpan? duration)
        {
            if (hit) Interlocked.Increment(ref _hits);
            else Interlocked.Increment(ref _misses);

            if (duration.HasValue)
            {
                lock (_durations)
                {
                    _durations.Add(duration.Value);
                }
            }

            return this;
        }

        public double HitRate
        {
            get
            {
                var total = _hits + _misses;
                return total > 0 ? (double)_hits / total * 100 : 0;
            }
        }

        public TimeSpan AverageDuration
        {
            get
            {
                lock (_durations)
                {
                    if (!_durations.Any()) return TimeSpan.Zero;
                    var totalMs = _durations.Sum(d => d.TotalMilliseconds);
                    return TimeSpan.FromMilliseconds(totalMs / _durations.Count);
                }
            }
        }

        public int TotalOperations => _hits + _misses;

        public CacheMetrics Clone()
        {
            var clone = new CacheMetrics(true, TimeSpan.Zero);
            clone._hits = _hits;
            clone._misses = _misses;
            lock (_durations)
            {
                clone._durations.Clear();
                clone._durations.AddRange(_durations);
            }
            return clone;
        }
    }

    /// <summary>
    /// Snapshot of all current metrics
    /// </summary>
    public class MetricsSnapshot
    {
        public DateTime Timestamp { get; set; }
        public DateTime? FromTime { get; set; }
        public DateTime? ToTime { get; set; }
        public Dictionary<string, ResponseTimeMetrics> ResponseTimeMetrics { get; set; } = new();
        public Dictionary<string, CacheMetrics> CacheMetrics { get; set; } = new();
        public Dictionary<string, List<MetricData>> RawMetrics { get; set; } = new();
    }

    /// <summary>
    /// Represents an alert condition that has been triggered
    /// </summary>
    public class AlertCondition
    {
        public string Type { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;
        public double CurrentValue { get; set; }
        public double Threshold { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}