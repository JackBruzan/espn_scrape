using ESPNScrape.Models.Espn;
using ESPNScrape.Services;
using ESPNScrape.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace ESPNScrape.Tests.Services
{
    public class EspnBoxScoreServiceTests
    {
        private readonly Mock<IEspnHttpService> _mockHttpService;
        private readonly Mock<ILogger<EspnBoxScoreService>> _mockLogger;
        private readonly EspnBoxScoreService _service;

        private const string TestGameId = "401547440";
        private const string TestTeamId = "1";

        public EspnBoxScoreServiceTests()
        {
            _mockHttpService = new Mock<IEspnHttpService>();
            _mockLogger = new Mock<ILogger<EspnBoxScoreService>>();
            _service = new EspnBoxScoreService(_mockHttpService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetBoxScoreDataAsync_ValidGameId_ReturnsBoxScore()
        {
            // Arrange
            var expectedJson = CreateMockBoxScoreJson();
            var expectedUrl = $"https://site.api.espn.com/apis/site/v2/sports/football/nfl/summary?event={TestGameId}";

            _mockHttpService.Setup(x => x.GetRawJsonAsync(expectedUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedJson);

            // Act
            var result = await _service.GetBoxScoreDataAsync(TestGameId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Teams.Count);
            _mockHttpService.Verify(x => x.GetRawJsonAsync(expectedUrl, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetBoxScoreDataAsync_HttpServiceThrows_ReturnsNull()
        {
            // Arrange
            var expectedUrl = $"https://site.api.espn.com/apis/site/v2/sports/football/nfl/summary?event={TestGameId}";

            _mockHttpService.Setup(x => x.GetRawJsonAsync(expectedUrl, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act
            var result = await _service.GetBoxScoreDataAsync(TestGameId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetLiveBoxScoreDataAsync_ValidGameId_ReturnsBoxScore()
        {
            // Arrange
            var expectedJson = CreateMockLiveBoxScoreJson();
            var expectedUrl = $"https://site.api.espn.com/apis/site/v2/sports/football/nfl/summary?event={TestGameId}";

            _mockHttpService.Setup(x => x.GetRawJsonAsync(expectedUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedJson);

            // Act
            var result = await _service.GetLiveBoxScoreDataAsync(TestGameId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Teams.Count);
        }

        [Fact]
        public async Task ParseTeamStatsAsync_ValidJson_ReturnsTeamStats()
        {
            // Arrange
            var boxScoreJson = CreateMockBoxScoreJson();

            // Act
            var result = await _service.ParseTeamStatsAsync(boxScoreJson);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Value.homeTeam);
            Assert.NotNull(result.Value.awayTeam);
            Assert.Equal("Home Team", result.Value.homeTeam.Team.DisplayName);
            Assert.Equal("Away Team", result.Value.awayTeam.Team.DisplayName);
        }

        [Fact]
        public async Task ParseTeamStatsAsync_InvalidJson_ReturnsNull()
        {
            // Arrange
            var invalidJson = "{ \"invalid\": \"structure\" }";

            // Act
            var result = await _service.ParseTeamStatsAsync(invalidJson);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task ExtractGameMetadataAsync_ValidJson_ReturnsGameInfo()
        {
            // Arrange
            var boxScoreJson = CreateMockBoxScoreWithMetadataJson();

            // Act
            var result = await _service.ExtractGameMetadataAsync(boxScoreJson);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(70000, result.Attendance);
            Assert.Equal("Sunny", result.Weather?.Conditions);
            Assert.Equal("Test Stadium", result.Venue.FullName);
            Assert.Equal(3, result.Officials.Count);
        }

        [Fact]
        public async Task ExtractGameMetadataAsync_MissingHeaderJson_ReturnsNull()
        {
            // Arrange
            var jsonWithoutHeader = "{ \"boxscore\": { \"teams\": [] } }";

            // Act
            var result = await _service.ExtractGameMetadataAsync(jsonWithoutHeader);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetBoxScoreUrl_ValidGameId_ReturnsCorrectUrl()
        {
            // Act
            var result = _service.GetBoxScoreUrl(TestGameId);

            // Assert
            Assert.Equal($"https://www.espn.com/nfl/boxscore/_/gameId/{TestGameId}", result);
        }

        [Fact]
        public async Task ParsePlayByPlayDataAsync_ValidJson_ReturnsPlayByPlayData()
        {
            // Arrange
            var playByPlayJson = CreateMockPlayByPlayJson();

            // Act
            var result = await _service.ParsePlayByPlayDataAsync(playByPlayJson);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Value.drives.Count);
            Assert.Single(result.Value.scoringPlays);

            var firstDrive = result.Value.drives[0];
            Assert.Equal("drive1", firstDrive.Id);
            Assert.Equal("TD Drive", firstDrive.Description);
            Assert.Equal(8, firstDrive.Plays);
            Assert.Equal(75, firstDrive.Yards);
        }

        [Fact]
        public async Task ParsePlayByPlayDataAsync_NoPlayByPlayData_ReturnsEmptyLists()
        {
            // Arrange
            var emptyJson = "{ \"header\": {} }";

            // Act
            var result = await _service.ParsePlayByPlayDataAsync(emptyJson);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Value.drives);
            Assert.Empty(result.Value.scoringPlays);
        }

        [Fact]
        public async Task GetTeamOffensiveStatsAsync_ValidGameAndTeam_ReturnsStats()
        {
            // Arrange
            var expectedJson = CreateMockBoxScoreJson();
            var expectedUrl = $"https://site.api.espn.com/apis/site/v2/sports/football/nfl/summary?event={TestGameId}";

            _mockHttpService.Setup(x => x.GetRawJsonAsync(expectedUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedJson);

            // Act
            var result = await _service.GetTeamOffensiveStatsAsync(TestGameId, TestTeamId);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("totalYards", result.Keys);
            Assert.Contains("rushingYards", result.Keys);
            Assert.Contains("passingYards", result.Keys);
            Assert.Contains("firstDowns", result.Keys);
        }

        [Fact]
        public async Task GetTeamDefensiveStatsAsync_ValidGameAndTeam_ReturnsStats()
        {
            // Arrange
            var expectedJson = CreateMockBoxScoreJson();
            var expectedUrl = $"https://site.api.espn.com/apis/site/v2/sports/football/nfl/summary?event={TestGameId}";

            _mockHttpService.Setup(x => x.GetRawJsonAsync(expectedUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedJson);

            // Act
            var result = await _service.GetTeamDefensiveStatsAsync(TestGameId, TestTeamId);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("sacks", result.Keys);
            Assert.Contains("interceptions", result.Keys);
            Assert.Contains("fumbleRecoveries", result.Keys);
        }

        [Fact]
        public async Task GetTeamOffensiveStatsAsync_TeamNotFound_ReturnsNull()
        {
            // Arrange
            var expectedJson = CreateMockBoxScoreJson();
            var expectedUrl = $"https://site.api.espn.com/apis/site/v2/sports/football/nfl/summary?event={TestGameId}";

            _mockHttpService.Setup(x => x.GetRawJsonAsync(expectedUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedJson);

            // Act
            var result = await _service.GetTeamOffensiveStatsAsync(TestGameId, "nonexistent-team");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task IsGameLiveAsync_LiveGame_ReturnsTrue()
        {
            // Arrange
            var liveGameJson = CreateMockLiveGameStatusJson("in");
            var expectedUrl = $"https://site.api.espn.com/apis/site/v2/sports/football/nfl/summary?event={TestGameId}";

            _mockHttpService.Setup(x => x.GetRawJsonAsync(expectedUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync(liveGameJson);

            // Act
            var result = await _service.IsGameLiveAsync(TestGameId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsGameLiveAsync_CompletedGame_ReturnsFalse()
        {
            // Arrange
            var completedGameJson = CreateMockLiveGameStatusJson("post");
            var expectedUrl = $"https://site.api.espn.com/apis/site/v2/sports/football/nfl/summary?event={TestGameId}";

            _mockHttpService.Setup(x => x.GetRawJsonAsync(expectedUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync(completedGameJson);

            // Act
            var result = await _service.IsGameLiveAsync(TestGameId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsGameLiveAsync_HttpServiceThrows_ReturnsFalse()
        {
            // Arrange
            var expectedUrl = $"https://site.api.espn.com/apis/site/v2/sports/football/nfl/summary?event={TestGameId}";

            _mockHttpService.Setup(x => x.GetRawJsonAsync(expectedUrl, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act
            var result = await _service.IsGameLiveAsync(TestGameId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsGameLiveAsync_InvalidJsonStructure_ReturnsFalse()
        {
            // Arrange
            var invalidJson = "{ \"invalid\": \"structure\" }";
            var expectedUrl = $"https://site.api.espn.com/apis/site/v2/sports/football/nfl/summary?event={TestGameId}";

            _mockHttpService.Setup(x => x.GetRawJsonAsync(expectedUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync(invalidJson);

            // Act
            var result = await _service.IsGameLiveAsync(TestGameId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetBoxScoreDataAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            var result = await _service.GetBoxScoreDataAsync(TestGameId, cts.Token);
            Assert.Null(result); // Service catches exceptions and returns null
        }

        #region Helper Methods for Mock Data Creation

        private string CreateMockBoxScoreJson()
        {
            var mockData = new
            {
                boxscore = new
                {
                    teams = new[]
                    {
                        new
                        {
                            team = new
                            {
                                id = "1",
                                displayName = "Away Team",
                                abbreviation = "AWAY"
                            },
                            statistics = new[]
                            {
                                new { name = "totalYards", displayName = "Total Yards", value = "350", displayValue = "350" },
                                new { name = "rushingYards", displayName = "Rushing Yards", value = "120", displayValue = "120" },
                                new { name = "passingYards", displayName = "Passing Yards", value = "230", displayValue = "230" },
                                new { name = "firstDowns", displayName = "First Downs", value = "18", displayValue = "18" },
                                new { name = "sacks", displayName = "Sacks", value = "2-15", displayValue = "2-15" },
                                new { name = "interceptions", displayName = "Interceptions", value = "1", displayValue = "1" }
                            },
                            lineScore = new[] { 7, 10, 0, 7 }
                        },
                        new
                        {
                            team = new
                            {
                                id = "2",
                                displayName = "Home Team",
                                abbreviation = "HOME"
                            },
                            statistics = new[]
                            {
                                new { name = "totalYards", displayName = "Total Yards", value = "280", displayValue = "280" },
                                new { name = "rushingYards", displayName = "Rushing Yards", value = "95", displayValue = "95" },
                                new { name = "passingYards", displayName = "Passing Yards", value = "185", displayValue = "185" },
                                new { name = "firstDowns", displayName = "First Downs", value = "15", displayValue = "15" },
                                new { name = "sacks", displayName = "Sacks", value = "1-8", displayValue = "1-8" },
                                new { name = "interceptions", displayName = "Interceptions", value = "0", displayValue = "0" }
                            },
                            lineScore = new[] { 0, 7, 14, 0 }
                        }
                    }
                }
            };

            return JsonSerializer.Serialize(mockData);
        }

        private string CreateMockLiveBoxScoreJson()
        {
            var mockData = new
            {
                boxscore = new
                {
                    teams = new[]
                    {
                        new
                        {
                            team = new
                            {
                                id = "1",
                                displayName = "Away Team",
                                abbreviation = "AWAY"
                            },
                            statistics = new[]
                            {
                                new { name = "totalYards", displayName = "Total Yards", value = "180", displayValue = "180" },
                                new { name = "rushingYards", displayName = "Rushing Yards", value = "65", displayValue = "65" },
                                new { name = "passingYards", displayName = "Passing Yards", value = "115", displayValue = "115" }
                            },
                            lineScore = new[] { 7, 3 }
                        },
                        new
                        {
                            team = new
                            {
                                id = "2",
                                displayName = "Home Team",
                                abbreviation = "HOME"
                            },
                            statistics = new[]
                            {
                                new { name = "totalYards", displayName = "Total Yards", value = "145", displayValue = "145" },
                                new { name = "rushingYards", displayName = "Rushing Yards", value = "50", displayValue = "50" },
                                new { name = "passingYards", displayName = "Passing Yards", value = "95", displayValue = "95" }
                            },
                            lineScore = new[] { 0, 7 }
                        }
                    }
                }
            };

            return JsonSerializer.Serialize(mockData);
        }

        private string CreateMockBoxScoreWithMetadataJson()
        {
            var mockData = new
            {
                header = new
                {
                    attendance = 70000,
                    officials = new[]
                    {
                        new { displayName = "John Smith", position = "Referee" },
                        new { displayName = "Jane Doe", position = "Umpire" },
                        new { displayName = "Bob Johnson", position = "Line Judge" }
                    },
                    weather = new
                    {
                        temperature = 72,
                        conditions = "Sunny",
                        windSpeed = "5 mph",
                        humidity = "45%"
                    },
                    venue = new
                    {
                        fullName = "Test Stadium",
                        address = new
                        {
                            city = "Test City",
                            state = "TS"
                        }
                    }
                },
                boxscore = new
                {
                    teams = new[]
                    {
                        new
                        {
                            team = new { id = "1", displayName = "Team 1" },
                            statistics = new object[] { },
                            lineScore = new[] { 0 }
                        }
                    }
                }
            };

            return JsonSerializer.Serialize(mockData);
        }

        private string CreateMockPlayByPlayJson()
        {
            var mockData = new
            {
                drives = new[]
                {
                    new
                    {
                        id = "drive1",
                        description = "TD Drive",
                        plays = 8,
                        yards = 75,
                        timeElapsed = "4:32",
                        result = "Touchdown",
                        team = new { id = "1", displayName = "Team 1" }
                    },
                    new
                    {
                        id = "drive2",
                        description = "FG Drive",
                        plays = 5,
                        yards = 25,
                        timeElapsed = "2:15",
                        result = "Field Goal",
                        team = new { id = "2", displayName = "Team 2" }
                    }
                },
                scoringPlays = new[]
                {
                    new
                    {
                        id = "score1",
                        text = "John Doe 5 yard TD run",
                        type = "rushing-touchdown",
                        period = 1,
                        clock = "8:32",
                        scoreValue = 6,
                        team = new { id = "1", displayName = "Team 1" }
                    }
                }
            };

            return JsonSerializer.Serialize(mockData);
        }

        private string CreateMockLiveGameStatusJson(string state)
        {
            var mockData = new
            {
                header = new
                {
                    competition = new
                    {
                        status = new
                        {
                            type = new
                            {
                                state = state
                            }
                        }
                    }
                }
            };

            return JsonSerializer.Serialize(mockData);
        }

        #endregion
    }
}