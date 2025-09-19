using ESPNScrape.Models.Espn;
using ESPNScrape.Services;
using ESPNScrape.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace ESPNScrape.Tests.Services
{
    public class EspnPlayerStatsServiceTests
    {
        private readonly Mock<IEspnHttpService> _mockHttpService;
        private readonly Mock<ILogger<EspnPlayerStatsService>> _mockLogger;
        private readonly EspnPlayerStatsService _service;

        public EspnPlayerStatsServiceTests()
        {
            _mockHttpService = new Mock<IEspnHttpService>();
            _mockLogger = new Mock<ILogger<EspnPlayerStatsService>>();
            _service = new EspnPlayerStatsService(_mockHttpService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task ExtractGamePlayerStatsAsync_ValidEventId_ReturnsPlayerStats()
        {
            // Arrange
            var eventId = "401547549";
            var mockBoxScoreHtml = CreateMockBoxScoreHtml();

            _mockHttpService.Setup(s => s.GetRawJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockBoxScoreHtml);

            // Act
            var result = await _service.ExtractGamePlayerStatsAsync(eventId);

            // Assert
            Assert.NotNull(result);
            var playerStatsList = result.ToList();
            Assert.NotEmpty(playerStatsList);

            // Verify HTTP service was called with correct URL
            _mockHttpService.Verify(s => s.GetRawJsonAsync(
                It.Is<string>(url => url.Contains(eventId)),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExtractGamePlayerStatsAsync_EmptyEventId_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.ExtractGamePlayerStatsAsync(string.Empty));
        }

        [Fact]
        public async Task ExtractGamePlayerStatsAsync_NullEventId_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.ExtractGamePlayerStatsAsync(null!));
        }

        [Fact]
        public async Task ExtractGamePlayerStatsAsync_HttpServiceFailure_ThrowsException()
        {
            // Arrange
            var eventId = "401547549";
            _mockHttpService.Setup(s => s.GetRawJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("ESPN API error"));

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                _service.ExtractGamePlayerStatsAsync(eventId));
        }

        [Fact]
        public async Task ParsePlayerStatsFromJsonAsync_ValidJsonData_ReturnsParsedStats()
        {
            // Arrange
            var boxScoreJson = CreateMockBoxScoreJson();
            var gameInfo = CreateMockGameInfo();

            // Act
            var result = await _service.ParsePlayerStatsFromJsonAsync(boxScoreJson, gameInfo);

            // Assert
            Assert.NotNull(result);
            var playerStatsList = result.ToList();
            Assert.NotEmpty(playerStatsList);

            // Verify first player has required fields
            var firstPlayer = playerStatsList.First();
            Assert.False(string.IsNullOrEmpty(firstPlayer.PlayerId));
            Assert.False(string.IsNullOrEmpty(firstPlayer.DisplayName));
            Assert.NotNull(firstPlayer.Position);
            Assert.Equal(gameInfo.Id, firstPlayer.GameId);
        }

        [Fact]
        public async Task ParsePlayerStatsFromJsonAsync_EmptyJson_ReturnsEmptyCollection()
        {
            // Arrange
            var gameInfo = CreateMockGameInfo();

            // Act
            var result = await _service.ParsePlayerStatsFromJsonAsync(string.Empty, gameInfo);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task ParsePlayerStatsFromJsonAsync_InvalidJson_ThrowsException()
        {
            // Arrange
            var invalidJson = "{ invalid json }";
            var gameInfo = CreateMockGameInfo();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.ParsePlayerStatsFromJsonAsync(invalidJson, gameInfo));
        }

        [Fact]
        public async Task ExtractPlayerIdsAsync_ValidJson_ReturnsPlayerIds()
        {
            // Arrange
            var boxScoreJson = CreateMockBoxScoreJson();

            // Act
            var result = await _service.ExtractPlayerIdsAsync(boxScoreJson);

            // Assert
            Assert.NotNull(result);
            var playerIds = result.ToList();
            Assert.NotEmpty(playerIds);

            // Verify all returned IDs are non-empty strings
            Assert.All(playerIds, id => Assert.False(string.IsNullOrEmpty(id)));
        }

        [Fact]
        public async Task ExtractPlayerIdsAsync_EmptyJson_ReturnsEmptyCollection()
        {
            // Act
            var result = await _service.ExtractPlayerIdsAsync(string.Empty);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task MapEspnPlayerDataAsync_ValidPlayerData_ReturnsMappedStats()
        {
            // Arrange
            var playerData = CreateMockEspnPlayerData();
            var position = CreateMockPlayerPosition("QB");
            var gameContext = CreateMockGameInfo();

            // Act
            var result = await _service.MapEspnPlayerDataAsync(playerData, position, gameContext);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(gameContext.Id, result.GameId);
            Assert.Equal(position, result.Position);
            Assert.False(string.IsNullOrEmpty(result.PlayerId));
            Assert.False(string.IsNullOrEmpty(result.DisplayName));
        }

        [Fact]
        public async Task MapEspnPlayerDataAsync_NullPlayerData_HandlesGracefully()
        {
            // Arrange
            var position = CreateMockPlayerPosition("RB");
            var gameContext = CreateMockGameInfo();

            // Act
            var result = await _service.MapEspnPlayerDataAsync(null, position, gameContext);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(gameContext.Id, result.GameId);
            Assert.Equal(position, result.Position);
            Assert.Equal(string.Empty, result.PlayerId);
            Assert.Equal(string.Empty, result.DisplayName);
        }

        [Theory]
        [InlineData("Tom Brady", "Tom Brady")]
        [InlineData("TOM BRADY", "Tom Brady")]
        [InlineData("tom brady", "Tom Brady")]
        [InlineData("Tom  Brady", "Tom Brady")]
        [InlineData("Tom-Brady", "Tom-Brady")]
        [InlineData("Tom O'Brady", "Tom O'Brady")]
        public async Task NormalizePlayerNameAsync_VariousFormats_ReturnsNormalizedName(string inputName, string expectedName)
        {
            // Act
            var result = await _service.NormalizePlayerNameAsync(inputName);

            // Assert
            Assert.NotNull(result);
            Assert.False(string.IsNullOrWhiteSpace(result));
            Assert.Equal(expectedName, result);
        }

        [Fact]
        public async Task NormalizePlayerNameAsync_EmptyName_ReturnsEmpty()
        {
            // Act
            var result = await _service.NormalizePlayerNameAsync(string.Empty);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public async Task NormalizePlayerNameAsync_NullName_ReturnsEmpty()
        {
            // Act
            var result = await _service.NormalizePlayerNameAsync(null!);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public async Task ExtractTeamStatsAsync_ValidTeamId_ReturnsTeamPlayerStats()
        {
            // Arrange
            var boxScoreJson = CreateMockBoxScoreJson();
            var teamId = "1";

            // Act
            var result = await _service.ExtractTeamStatsAsync(boxScoreJson, teamId);

            // Assert
            Assert.NotNull(result);
            var playerStatsList = result.ToList();

            // Should return stats for players on the specified team
            Assert.All(playerStatsList, stats => Assert.Equal(teamId, stats.Team.Id));
        }

        [Fact]
        public async Task ExtractTeamStatsAsync_InvalidTeamId_ReturnsEmptyCollection()
        {
            // Arrange
            var boxScoreJson = CreateMockBoxScoreJson();
            var invalidTeamId = "999";

            // Act
            var result = await _service.ExtractTeamStatsAsync(boxScoreJson, invalidTeamId);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task HandleMissingDataAsync_NullPlayerData_CreatesDefaultStats()
        {
            // Arrange
            var position = CreateMockPlayerPosition("WR");

            // Act
            var result = await _service.HandleMissingDataAsync(null!, position);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(position, result.Position);
            Assert.False(string.IsNullOrEmpty(result.PlayerId));
            Assert.Equal("Unknown Player", result.DisplayName);
            Assert.NotEmpty(result.Statistics);
        }

        [Theory]
        [InlineData("QB")]
        [InlineData("RB")]
        [InlineData("WR")]
        [InlineData("TE")]
        [InlineData("DEF")]
        public async Task HandleMissingDataAsync_DifferentPositions_CreatesAppropriateDefaults(string positionAbbr)
        {
            // Arrange
            var position = CreateMockPlayerPosition(positionAbbr);

            // Act
            var result = await _service.HandleMissingDataAsync(null!, position);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(position, result.Position);
            Assert.NotEmpty(result.Statistics);

            // Should have position-appropriate default statistics
            Assert.All(result.Statistics, stat => Assert.Equal(0, stat.Value));
        }

        [Fact]
        public async Task ValidatePlayerStatsAsync_ValidStats_ReturnsTrue()
        {
            // Arrange
            var playerStats = CreateValidPlayerStats();

            // Act
            var result = await _service.ValidatePlayerStatsAsync(playerStats);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ValidatePlayerStatsAsync_NullStats_ReturnsFalse()
        {
            // Act
            var result = await _service.ValidatePlayerStatsAsync(null!);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ValidatePlayerStatsAsync_MissingRequiredFields_ReturnsFalse()
        {
            // Arrange
            var playerStats = new PlayerStats
            {
                // Missing PlayerId, DisplayName, and Position
                GameId = "123"
            };

            // Act
            var result = await _service.ValidatePlayerStatsAsync(playerStats);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ValidatePlayerStatsAsync_NegativeStatsNotAllowed_ReturnsFalse()
        {
            // Arrange
            var playerStats = CreateValidPlayerStats();
            playerStats.Statistics.Add(new PlayerStatistic
            {
                Name = "passingYards",
                Value = -100 // Invalid negative value
            });

            // Act
            var result = await _service.ValidatePlayerStatsAsync(playerStats);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ValidatePlayerStatsAsync_ExtremelyHighStats_ReturnsFalse()
        {
            // Arrange
            var playerStats = CreateValidPlayerStats();
            playerStats.Statistics.Add(new PlayerStatistic
            {
                Name = "completions",
                Value = 2000 // Unrealistically high value
            });

            // Act
            var result = await _service.ValidatePlayerStatsAsync(playerStats);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ValidatePlayerStatsAsync_AllowedNegativeStats_ReturnsTrue()
        {
            // Arrange
            var playerStats = CreateValidPlayerStats();
            playerStats.Statistics.Add(new PlayerStatistic
            {
                Name = "sackYards",
                Value = -15 // Valid negative value for sack yards
            });

            // Act
            var result = await _service.ValidatePlayerStatsAsync(playerStats);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ValidatePlayerStatsAsync_AllowedHighValueStats_ReturnsTrue()
        {
            // Arrange
            var playerStats = CreateValidPlayerStats();
            playerStats.Statistics.Add(new PlayerStatistic
            {
                Name = "passingYards",
                Value = 500 // Valid high value for passing yards
            });

            // Act
            var result = await _service.ValidatePlayerStatsAsync(playerStats);

            // Assert
            Assert.True(result);
        }

        #region Helper Methods

        private string CreateMockBoxScoreHtml()
        {
            var jsonData = CreateMockBoxScoreJson();
            return $@"
                <html>
                <body>
                    <script>
                        window['__espnfitt__'] = {jsonData};
                    </script>
                </body>
                </html>";
        }

        private string CreateMockBoxScoreJson()
        {
            var boxScoreData = new
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
                                displayName = "Team One",
                                abbreviation = "T1"
                            },
                            statistics = new[]
                            {
                                new
                                {
                                    athletes = new[]
                                    {
                                        new
                                        {
                                            athlete = new
                                            {
                                                id = "12345",
                                                displayName = "Test Player",
                                                shortName = "T. Player",
                                                jersey = "12",
                                                position = new
                                                {
                                                    id = "1",
                                                    name = "Quarterback",
                                                    displayName = "Quarterback",
                                                    abbreviation = "QB"
                                                }
                                            },
                                            stats = new
                                            {
                                                passingYards = 250,
                                                completions = 20,
                                                attempts = 30,
                                                touchdowns = 2
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                gamepackageJSON = new
                {
                    header = new
                    {
                        season = new
                        {
                            year = 2025,
                            seasonType = 2
                        },
                        week = new
                        {
                            weekNumber = 3
                        }
                    }
                }
            };

            return JsonSerializer.Serialize(boxScoreData);
        }

        private GameEvent CreateMockGameInfo()
        {
            return new GameEvent
            {
                Id = "401547549",
                Season = new Season { Year = 2025, SeasonType = 2 },
                Week = new Week { WeekNumber = 3 }
            };
        }

        private dynamic CreateMockEspnPlayerData()
        {
            return new
            {
                id = "12345",
                displayName = "Test Player",
                shortName = "T. Player",
                jersey = "12",
                team = new
                {
                    id = "1",
                    displayName = "Test Team",
                    name = "Test Team",
                    abbreviation = "TT"
                },
                statistics = new
                {
                    passingYards = 250,
                    completions = 20
                }
            };
        }

        private PlayerPosition CreateMockPlayerPosition(string abbreviation)
        {
            return new PlayerPosition
            {
                Id = "1",
                Name = GetPositionName(abbreviation),
                DisplayName = GetPositionName(abbreviation),
                Abbreviation = abbreviation,
                Leaf = true
            };
        }

        private string GetPositionName(string abbreviation)
        {
            return abbreviation switch
            {
                "QB" => "Quarterback",
                "RB" => "Running Back",
                "WR" => "Wide Receiver",
                "TE" => "Tight End",
                "DEF" => "Defense",
                _ => "Unknown"
            };
        }

        private PlayerStats CreateValidPlayerStats()
        {
            return new PlayerStats
            {
                PlayerId = "12345",
                DisplayName = "Test Player",
                ShortName = "T. Player",
                Position = CreateMockPlayerPosition("QB"),
                GameId = "401547549",
                Season = 2025,
                Week = 3,
                SeasonType = 2,
                Statistics = new List<PlayerStatistic>
                {
                    new PlayerStatistic
                    {
                        Name = "passingYards",
                        DisplayName = "Passing Yards",
                        Category = "passing",
                        Value = 250,
                        DisplayValue = "250"
                    },
                    new PlayerStatistic
                    {
                        Name = "completions",
                        DisplayName = "Completions",
                        Category = "passing",
                        Value = 20,
                        DisplayValue = "20"
                    }
                }
            };
        }

        #endregion
    }
}