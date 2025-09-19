using ESPNScrape.Models.Espn;
using ESPNScrape.Services;
using ESPNScrape.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace ESPNScrape.Tests.Services
{
    public class EspnScoreboardServiceTests
    {
        private readonly Mock<IEspnHttpService> _mockHttpService;
        private readonly Mock<ILogger<EspnScoreboardService>> _mockLogger;
        private readonly EspnScoreboardService _service;

        public EspnScoreboardServiceTests()
        {
            _mockHttpService = new Mock<IEspnHttpService>();
            _mockLogger = new Mock<ILogger<EspnScoreboardService>>();
            _service = new EspnScoreboardService(_mockHttpService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetScoreboardAsync_ValidParameters_ReturnsScoreboardData()
        {
            // Arrange
            var year = 2025;
            var week = 3;
            var seasonType = 2;
            var mockHtmlResponse = CreateMockEspnHtmlResponse();

            _mockHttpService.Setup(x => x.GetRawJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockHtmlResponse);

            // Act
            var result = await _service.GetScoreboardAsync(year, week, seasonType);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Events);
            Assert.NotNull(result.Season);
            Assert.NotNull(result.Week);
            Assert.NotNull(result.Leagues);
        }

        [Theory]
        [InlineData(1999, 1, 2)] // Year too early
        [InlineData(2030, 1, 2)] // Year too late
        [InlineData(2025, 0, 2)] // Week too low
        [InlineData(2025, 19, 2)] // Week too high for regular season
        [InlineData(2025, 1, 0)] // Invalid season type
        [InlineData(2025, 1, 4)] // Invalid season type
        public async Task GetScoreboardAsync_InvalidParameters_ThrowsArgumentException(int year, int week, int seasonType)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.GetScoreboardAsync(year, week, seasonType));
        }

        [Fact]
        public async Task GetScoreboardAsync_HttpServiceThrows_PropagatesException()
        {
            // Arrange
            _mockHttpService.Setup(x => x.GetRawJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("ESPN is down"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HttpRequestException>(() => _service.GetScoreboardAsync(2025, 3, 2));
            Assert.Equal("ESPN is down", exception.Message);
        }

        [Fact]
        public async Task GetScoreboardAsync_InvalidJson_ThrowsInvalidOperationException()
        {
            // Arrange
            var invalidHtml = "<html><body>No JSON here</body></html>";
            _mockHttpService.Setup(x => x.GetRawJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(invalidHtml);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetScoreboardAsync(2025, 3, 2));
        }

        [Fact]
        public async Task ExtractEventsAsync_ValidScoreboard_ReturnsEvents()
        {
            // Arrange
            var scoreboard = new ScoreboardData
            {
                Events = new List<GameEvent>
                {
                    new GameEvent { Id = "401547430", Name = "Test Game 1" },
                    new GameEvent { Id = "401547431", Name = "Test Game 2" }
                }
            };

            // Act
            var result = await _service.ExtractEventsAsync(scoreboard);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
            Assert.Equal("Test Game 1", result.First().Name);
        }

        [Fact]
        public async Task ExtractEventsAsync_NullScoreboard_ReturnsEmptyCollection()
        {
            // Act
            var result = await _service.ExtractEventsAsync(null);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task ExtractSeasonInfoAsync_ValidScoreboard_ReturnsSeason()
        {
            // Arrange
            var season = new Season { Year = 2025, DisplayName = "2025 NFL Season" };
            var scoreboard = new ScoreboardData { Season = season };

            // Act
            var result = await _service.ExtractSeasonInfoAsync(scoreboard);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2025, result.Year);
            Assert.Equal("2025 NFL Season", result.DisplayName);
        }

        [Fact]
        public async Task ExtractWeekInfoAsync_ValidScoreboard_ReturnsWeek()
        {
            // Arrange
            var week = new Week { WeekNumber = 3, Year = 2025, Label = "Week 3" };
            var scoreboard = new ScoreboardData { Week = week };

            // Act
            var result = await _service.ExtractWeekInfoAsync(scoreboard);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.WeekNumber);
            Assert.Equal(2025, result.Year);
            Assert.Equal("Week 3", result.Label);
        }

        [Fact]
        public async Task GetEventReferencesAsync_ValidScoreboard_ReturnsReferences()
        {
            // Arrange
            var scoreboard = new ScoreboardData
            {
                Events = new List<GameEvent>
                {
                    new GameEvent { Id = "401547430" },
                    new GameEvent { Id = "401547431" }
                }
            };

            // Act
            var result = await _service.GetEventReferencesAsync(scoreboard);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
            Assert.Contains("http://sports.core.api.espn.pvt/v2/sports/football/leagues/nfl/events/401547430", result);
            Assert.Contains("http://sports.core.api.espn.pvt/v2/sports/football/leagues/nfl/events/401547431", result);
        }

        [Fact]
        public async Task GetEventReferencesAsync_EventsWithEmptyIds_FiltersOutEmptyIds()
        {
            // Arrange
            var scoreboard = new ScoreboardData
            {
                Events = new List<GameEvent>
                {
                    new GameEvent { Id = "401547430" },
                    new GameEvent { Id = "" },
                    new GameEvent { Id = "401547431" }
                }
            };

            // Act
            var result = await _service.GetEventReferencesAsync(scoreboard);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
            Assert.DoesNotContain(result, r => r.Contains("events//")); // Should not contain empty event ID
        }

        [Theory]
        [InlineData(1, 1, 4)] // Preseason valid
        [InlineData(2, 1, 18)] // Regular season valid
        [InlineData(3, 1, 5)] // Postseason valid
        public async Task ValidateParameters_ValidRanges_DoesNotThrow(int seasonType, int weekMin, int weekMax)
        {
            // Arrange
            var mockHtml = CreateMockEspnHtmlResponse();
            _mockHttpService.Setup(x => x.GetRawJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockHtml);

            // Act & Assert - Should not throw for valid ranges
            await _service.GetScoreboardAsync(2025, weekMin, seasonType);
            await _service.GetScoreboardAsync(2025, weekMax, seasonType);
        }

        private string CreateMockEspnHtmlResponse()
        {
            var mockScoreboardData = new
            {
                page = new
                {
                    content = new
                    {
                        scoreboard = new
                        {
                            events = new[]
                            {
                                new
                                {
                                    id = "401547430",
                                    uid = "s:20~l:28~e:401547430",
                                    date = "2025-09-19T20:00Z",
                                    name = "Buffalo Bills at Miami Dolphins",
                                    shortName = "BUF @ MIA"
                                }
                            },
                            season = new
                            {
                                year = 2025,
                                displayName = "2025 NFL Season",
                                startDate = "2025-09-05T00:00Z",
                                endDate = "2026-02-09T00:00Z",
                                seasonType = 2
                            },
                            week = new
                            {
                                weekNumber = 3,
                                seasonType = 2,
                                text = "3",
                                label = "Week 3",
                                startDate = "2025-09-19T00:00Z",
                                endDate = "2025-09-24T00:00Z",
                                year = 2025,
                                isActive = true
                            },
                            leagues = new[]
                            {
                                new
                                {
                                    id = "28",
                                    name = "National Football League",
                                    abbreviation = "NFL",
                                    season = new
                                    {
                                        year = 2025,
                                        displayName = "2025 NFL Season"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var jsonString = JsonSerializer.Serialize(mockScoreboardData);
            return $"<html><head></head><body><script>window['__espnfitt__'] = {jsonString};</script></body></html>";
        }
    }
}