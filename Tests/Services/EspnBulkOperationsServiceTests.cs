using ESPNScrape.Models.Espn;
using ESPNScrape.Services;
using ESPNScrape.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Concurrent;
using Xunit;

namespace ESPNScrape.Tests.Services
{
    public class EspnBulkOperationsServiceTests
    {
        private readonly Mock<IEspnApiService> _mockApiService;
        private readonly Mock<IEspnPlayerStatsService> _mockPlayerStatsService;
        private readonly Mock<IEspnScoreboardService> _mockScoreboardService;
        private readonly Mock<IEspnRateLimitService> _mockRateLimitService;
        private readonly Mock<ILogger<EspnBulkOperationsService>> _mockLogger;
        private readonly EspnBulkOperationsService _service;

        public EspnBulkOperationsServiceTests()
        {
            _mockApiService = new Mock<IEspnApiService>();
            _mockPlayerStatsService = new Mock<IEspnPlayerStatsService>();
            _mockScoreboardService = new Mock<IEspnScoreboardService>();
            _mockRateLimitService = new Mock<IEspnRateLimitService>();
            _mockLogger = new Mock<ILogger<EspnBulkOperationsService>>();

            _service = new EspnBulkOperationsService(
                _mockApiService.Object,
                _mockPlayerStatsService.Object,
                _mockScoreboardService.Object,
                _mockRateLimitService.Object,
                _mockLogger.Object);
        }

        #region GetBulkWeekPlayerStatsAsync Tests

        [Fact]
        public async Task GetBulkWeekPlayerStatsAsync_ValidRequest_ReturnsPlayerStats()
        {
            // Arrange
            var request = new BulkWeekRequest
            {
                Year = 2024,
                WeekNumbers = new List<int> { 1, 2 },
                SeasonType = 2,
                Options = new BulkOperationOptions
                {
                    MaxConcurrency = 2,
                    EnableProgressReporting = false
                }
            };

            var mockPlayerStats = CreateMockPlayerStats("player1", "game1");
            _mockApiService.Setup(s => s.GetWeekPlayerStatsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PlayerStats> { mockPlayerStats });

            // Act
            var result = await _service.GetBulkWeekPlayerStatsAsync(request);

            // Assert
            Assert.NotNull(result);
            var resultList = result.ToList();
            Assert.Equal(2, resultList.Count); // Should return stats for 2 weeks
            Assert.All(resultList, stats => Assert.Equal("player1", stats.PlayerId));

            // Verify API calls
            _mockApiService.Verify(s => s.GetWeekPlayerStatsAsync(2024, 1, 2, It.IsAny<CancellationToken>()), Times.Once);
            _mockApiService.Verify(s => s.GetWeekPlayerStatsAsync(2024, 2, 2, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetBulkWeekPlayerStatsAsync_InvalidOptions_ThrowsArgumentException()
        {
            // Arrange
            var request = new BulkWeekRequest
            {
                Year = 2024,
                WeekNumbers = new List<int> { 1 },
                Options = new BulkOperationOptions
                {
                    MaxConcurrency = 0 // Invalid
                }
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.GetBulkWeekPlayerStatsAsync(request));
        }

        [Fact]
        public async Task GetBulkWeekPlayerStatsAsync_WithProgressReporting_ReportsProgress()
        {
            // Arrange
            var request = new BulkWeekRequest
            {
                Year = 2024,
                WeekNumbers = new List<int> { 1, 2, 3 },
                Options = new BulkOperationOptions
                {
                    MaxConcurrency = 1,
                    EnableProgressReporting = true,
                    EnableMetrics = true
                }
            };

            var mockPlayerStats = CreateMockPlayerStats("player1", "game1");
            _mockApiService.Setup(s => s.GetWeekPlayerStatsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PlayerStats> { mockPlayerStats });

            var progressReports = new List<BulkOperationProgress>();
            var progress = new Progress<BulkOperationProgress>(report => progressReports.Add(report));

            // Act
            var result = await _service.GetBulkWeekPlayerStatsAsync(request, progress);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(progressReports);

            // Should have multiple progress reports
            Assert.True(progressReports.Count >= 2, "Should have at least initial and final progress reports");

            // Check final progress report
            var finalReport = progressReports.LastOrDefault(p => p.IsCompleted);
            Assert.NotNull(finalReport);
            Assert.Equal(3, finalReport.TotalItems);
            Assert.Equal(3, finalReport.CompletedItems);
        }

        [Fact]
        public async Task GetBulkWeekPlayerStatsAsync_ContinueOnError_ProcessesRemainingWeeks()
        {
            // Arrange
            var request = new BulkWeekRequest
            {
                Year = 2024,
                WeekNumbers = new List<int> { 1, 2, 3 },
                Options = new BulkOperationOptions
                {
                    MaxConcurrency = 1,
                    ContinueOnError = true,
                    EnableProgressReporting = true
                }
            };

            var mockPlayerStats = CreateMockPlayerStats("player1", "game1");

            _mockApiService.Setup(s => s.GetWeekPlayerStatsAsync(2024, 1, 2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PlayerStats> { mockPlayerStats });

            _mockApiService.Setup(s => s.GetWeekPlayerStatsAsync(2024, 2, 2, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Week 2 failed"));

            _mockApiService.Setup(s => s.GetWeekPlayerStatsAsync(2024, 3, 2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PlayerStats> { mockPlayerStats });

            var progressReports = new List<BulkOperationProgress>();
            var progress = new Progress<BulkOperationProgress>(report => progressReports.Add(report));

            // Act
            var result = await _service.GetBulkWeekPlayerStatsAsync(request, progress);

            // Assert
            Assert.NotNull(result);
            var resultList = result.ToList();
            Assert.Equal(2, resultList.Count); // Should have 2 successful weeks

            // Check that error was reported in progress
            var finalReport = progressReports.Last(p => p.IsCompleted);
            Assert.Equal(1, finalReport.FailedItems);
            Assert.Equal(2, finalReport.CompletedItems);
            Assert.Contains(finalReport.ErrorMessages, msg => msg.Contains("Week 2 failed"));
        }

        #endregion

        #region GetBulkSeasonPlayerStatsAsync Tests

        [Fact]
        public async Task GetBulkSeasonPlayerStatsAsync_ValidRequest_ReturnsPlayerStats()
        {
            // Arrange
            var request = new BulkSeasonRequest
            {
                Years = new List<int> { 2023, 2024 },
                SeasonTypes = new List<int> { 2 },
                Options = new BulkOperationOptions
                {
                    MaxConcurrency = 2,
                    EnableProgressReporting = false
                }
            };

            var mockPlayerStats = CreateMockPlayerStats("player1", "game1");
            _mockApiService.Setup(s => s.GetSeasonPlayerStatsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PlayerStats> { mockPlayerStats });

            // Act
            var result = await _service.GetBulkSeasonPlayerStatsAsync(request);

            // Assert
            Assert.NotNull(result);
            var resultList = result.ToList();
            Assert.Equal(2, resultList.Count); // Should return stats for 2 seasons

            // Verify API calls
            _mockApiService.Verify(s => s.GetSeasonPlayerStatsAsync(2023, 2, It.IsAny<CancellationToken>()), Times.Once);
            _mockApiService.Verify(s => s.GetSeasonPlayerStatsAsync(2024, 2, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetBulkSeasonPlayerStatsAsync_MultipleSeasonTypes_ProcessesAll()
        {
            // Arrange
            var request = new BulkSeasonRequest
            {
                Years = new List<int> { 2024 },
                SeasonTypes = new List<int> { 1, 2, 3 }, // Preseason, Regular, Postseason
                Options = new BulkOperationOptions
                {
                    MaxConcurrency = 1,
                    EnableProgressReporting = false
                }
            };

            var mockPlayerStats = CreateMockPlayerStats("player1", "game1");
            _mockApiService.Setup(s => s.GetSeasonPlayerStatsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PlayerStats> { mockPlayerStats });

            // Act
            var result = await _service.GetBulkSeasonPlayerStatsAsync(request);

            // Assert
            Assert.NotNull(result);
            var resultList = result.ToList();
            Assert.Equal(3, resultList.Count); // Should return stats for 3 season types

            // Verify API calls for each season type
            _mockApiService.Verify(s => s.GetSeasonPlayerStatsAsync(2024, 1, It.IsAny<CancellationToken>()), Times.Once);
            _mockApiService.Verify(s => s.GetSeasonPlayerStatsAsync(2024, 2, It.IsAny<CancellationToken>()), Times.Once);
            _mockApiService.Verify(s => s.GetSeasonPlayerStatsAsync(2024, 3, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region GetBulkGamePlayerStatsAsync Tests

        [Fact]
        public async Task GetBulkGamePlayerStatsAsync_ValidEventIds_ReturnsPlayerStats()
        {
            // Arrange
            var eventIds = new List<string> { "event1", "event2", "event3" };
            var options = new BulkOperationOptions
            {
                MaxConcurrency = 2,
                EnableProgressReporting = false
            };

            var mockPlayerStats = new List<PlayerStats>
            {
                CreateMockPlayerStats("player1", "event1"),
                CreateMockPlayerStats("player2", "event2"),
                CreateMockPlayerStats("player3", "event3")
            };

            _mockPlayerStatsService.Setup(s => s.ExtractBulkGamePlayerStatsAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockPlayerStats);

            // Act
            var result = await _service.GetBulkGamePlayerStatsAsync(eventIds, options);

            // Assert
            Assert.NotNull(result);
            var resultList = result.ToList();
            Assert.Equal(3, resultList.Count);

            // Verify player stats service was called
            _mockPlayerStatsService.Verify(s => s.ExtractBulkGamePlayerStatsAsync(
                It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(eventIds)),
                options.MaxConcurrency,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region StreamParseJsonAsync Tests

        [Fact]
        public async Task StreamParseJsonAsync_ValidJsonArray_ReturnsStreamedObjects()
        {
            // Arrange
            var jsonArray = """
                [
                    {"Id": "1", "Name": "Test1"},
                    {"Id": "2", "Name": "Test2"},
                    {"Id": "3", "Name": "Test3"}
                ]
                """;

            // Act
            var results = new List<TestObject>();
            await foreach (var item in _service.StreamParseJsonAsync<TestObject>(jsonArray))
            {
                results.Add(item);
            }

            // Assert
            Assert.Equal(3, results.Count);
            Assert.Equal("1", results[0].Id);
            Assert.Equal("Test1", results[0].Name);
            Assert.Equal("2", results[1].Id);
            Assert.Equal("Test2", results[1].Name);
        }

        [Fact]
        public async Task StreamParseJsonAsync_ValidJsonObject_ReturnsStreamedObjects()
        {
            // Arrange
            var jsonObject = """
                {
                    "data": [
                        {"Id": "1", "Name": "Test1"},
                        {"Id": "2", "Name": "Test2"}
                    ],
                    "metadata": {
                        "total": 2
                    }
                }
                """;

            // Act
            var results = new List<TestObject>();
            await foreach (var item in _service.StreamParseJsonAsync<TestObject>(jsonObject))
            {
                results.Add(item);
            }

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Equal("1", results[0].Id);
            Assert.Equal("Test1", results[0].Name);
        }

        [Fact]
        public async Task StreamParseJsonAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var jsonArray = """
                [
                    {"id": "1", "name": "Test1"},
                    {"id": "2", "name": "Test2"}
                ]
                """;

            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var item in _service.StreamParseJsonAsync<TestObject>(jsonArray, cts.Token))
                {
                    // Should not reach here
                }
            });
        }

        #endregion

        #region ProcessInBatchesAsync Tests

        [Fact]
        public async Task ProcessInBatchesAsync_ValidItems_ProcessesInBatches()
        {
            // Arrange
            var items = Enumerable.Range(1, 25).ToList(); // 25 items
            var options = new BulkOperationOptions
            {
                BatchSize = 10,
                MaxConcurrency = 2,
                EnableProgressReporting = false
            };

            var processedBatches = new ConcurrentBag<int>();
            Func<IEnumerable<int>, CancellationToken, Task<IEnumerable<string>>> processor =
                (batch, ct) =>
                {
                    var batchSize = batch.Count();
                    processedBatches.Add(batchSize);
                    return Task.FromResult(batch.Select(i => i.ToString()));
                };

            // Act
            var result = await _service.ProcessInBatchesAsync(items, processor, options);

            // Assert
            Assert.NotNull(result);
            var resultList = result.ToList();
            Assert.Equal(25, resultList.Count); // All items processed

            // Verify batching
            var batchSizes = processedBatches.ToList();
            Assert.Equal(3, batchSizes.Count); // Should have 3 batches: 10, 10, 5
            Assert.Equal(2, batchSizes.Count(size => size == 10)); // Two full batches
            Assert.Equal(1, batchSizes.Count(size => size == 5));  // One partial batch
        }

        [Fact]
        public async Task ProcessInBatchesAsync_WithProgressReporting_ReportsProgress()
        {
            // Arrange
            var items = Enumerable.Range(1, 15).ToList();
            var options = new BulkOperationOptions
            {
                BatchSize = 5,
                MaxConcurrency = 1,
                EnableProgressReporting = true
            };

            Func<IEnumerable<int>, CancellationToken, Task<IEnumerable<string>>> processor =
                (batch, ct) => Task.FromResult(batch.Select(i => i.ToString()));

            var progressReports = new List<BulkOperationProgress>();
            var progress = new Progress<BulkOperationProgress>(report => progressReports.Add(report));

            // Act
            var result = await _service.ProcessInBatchesAsync(items, processor, options, progress);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(progressReports);

            var finalReport = progressReports.Last(p => p.IsCompleted);
            Assert.Equal(3, finalReport.TotalItems); // 3 batches
            Assert.Equal(3, finalReport.CompletedItems);
        }

        #endregion

        #region Utility Methods Tests

        [Fact]
        public void GetCurrentMemoryUsage_ReturnsPositiveValue()
        {
            // Act
            var memoryUsage = _service.GetCurrentMemoryUsage();

            // Assert
            Assert.True(memoryUsage > 0);
        }

        [Fact]
        public void OptimizeMemoryUsage_DoesNotThrow()
        {
            // Act & Assert - Should not throw
            _service.OptimizeMemoryUsage();
        }

        [Theory]
        [InlineData(0, false)] // Invalid concurrency
        [InlineData(-1, false)] // Invalid concurrency
        [InlineData(5, true)] // Valid concurrency
        [InlineData(25, true)] // High but valid concurrency
        public void ValidateBulkOperationOptions_ConcurrencyValidation(int maxConcurrency, bool expectedValid)
        {
            // Arrange
            var options = new BulkOperationOptions
            {
                MaxConcurrency = maxConcurrency,
                BatchSize = 10,
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromSeconds(1),
                MaxMemoryThreshold = 1024 * 1024 * 1024,
                ProgressUpdateInterval = TimeSpan.FromSeconds(5)
            };

            // Act
            var result = _service.ValidateBulkOperationOptions(options);

            // Assert
            Assert.Equal(expectedValid, result.IsValid);
            if (!expectedValid)
            {
                Assert.Contains(result.ErrorMessages, msg => msg.Contains("MaxConcurrency"));
            }
        }

        [Fact]
        public void CreateOptimizedSemaphore_ReturnsConfiguredSemaphore()
        {
            // Arrange
            var maxConcurrency = 5;

            // Act
            using var semaphore = _service.CreateOptimizedSemaphore(maxConcurrency);

            // Assert
            Assert.NotNull(semaphore);
            Assert.Equal(5, semaphore.CurrentCount);
        }

        [Fact]
        public void CreateOptimizedSemaphore_HighConcurrency_LimitsToReasonableValue()
        {
            // Arrange
            var maxConcurrency = 50; // Very high concurrency

            // Act
            using var semaphore = _service.CreateOptimizedSemaphore(maxConcurrency);

            // Assert
            Assert.NotNull(semaphore);
            Assert.True(semaphore.CurrentCount <= 10); // Should be limited to 10
        }

        [Theory]
        [InlineData(100, 0, 0)] // No progress yet
        [InlineData(100, 50, 60)] // Half complete
        [InlineData(100, 100, 0)] // Complete
        [InlineData(100, 25, 180)] // Quarter complete
        public void EstimateTimeRemaining_CalculatesCorrectly(int totalItems, int completedItems, int elapsedSeconds)
        {
            // Arrange
            var elapsedTime = TimeSpan.FromSeconds(elapsedSeconds);

            // Act
            var estimated = _service.EstimateTimeRemaining(totalItems, completedItems, elapsedTime);

            // Assert
            if (completedItems == 0 || completedItems >= totalItems)
            {
                Assert.Equal(TimeSpan.Zero, estimated);
            }
            else
            {
                Assert.True(estimated > TimeSpan.Zero);
                // Rough validation of calculation
                var avgTimePerItem = elapsedTime.TotalSeconds / completedItems;
                var remainingItems = totalItems - completedItems;
                var expectedSeconds = avgTimePerItem * remainingItems;
                Assert.True(Math.Abs(estimated.TotalSeconds - expectedSeconds) < 1); // Within 1 second tolerance
            }
        }

        #endregion

        #region Helper Methods

        private static PlayerStats CreateMockPlayerStats(string playerId, string gameId)
        {
            return new PlayerStats
            {
                PlayerId = playerId,
                DisplayName = $"Player {playerId}",
                ShortName = $"P{playerId}",
                GameId = gameId,
                Season = 2024,
                Week = 1,
                SeasonType = 2,
                Statistics = new List<PlayerStatistic>
                {
                    new PlayerStatistic { Name = "rushingYards", Value = 100, DisplayValue = "100" },
                    new PlayerStatistic { Name = "touchdowns", Value = 2, DisplayValue = "2" }
                }
            };
        }

        public class TestObject
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        #endregion
    }
}