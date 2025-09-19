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

        #region Bulk Operations Tests

        [Fact]
        public async Task ExtractBulkGamePlayerStatsAsync_MultipleGames_ProcessesInParallel()
        {
            // Arrange
            var eventIds = new List<string> { "event1", "event2", "event3" };
            var maxConcurrency = 2;

            var mockBoxScoreHtml = CreateMockBoxScoreHtml();
            _mockHttpService.Setup(s => s.GetRawJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockBoxScoreHtml);

            // Act
            var result = await _service.ExtractBulkGamePlayerStatsAsync(eventIds, maxConcurrency);

            // Assert
            Assert.NotNull(result);
            var resultList = result.ToList();
            Assert.NotEmpty(resultList);

            // Verify HTTP service was called for each event ID
            foreach (var eventId in eventIds)
            {
                _mockHttpService.Verify(s => s.GetRawJsonAsync(
                    It.Is<string>(url => url.Contains(eventId)),
                    It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [Fact]
        public async Task ExtractBulkGamePlayerStatsAsync_EmptyEventIds_ReturnsEmptyResult()
        {
            // Arrange
            var eventIds = new List<string>();

            // Act
            var result = await _service.ExtractBulkGamePlayerStatsAsync(eventIds);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);

            // Verify no HTTP calls were made
            _mockHttpService.Verify(s => s.GetRawJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExtractBulkGamePlayerStatsAsync_SomeGamesFail_ContinuesProcessing()
        {
            // Arrange
            var eventIds = new List<string> { "event1", "event2", "event3" };
            var mockBoxScoreHtml = CreateMockBoxScoreHtml();

            _mockHttpService.Setup(s => s.GetRawJsonAsync(
                It.Is<string>(url => url.Contains("event1")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockBoxScoreHtml);

            _mockHttpService.Setup(s => s.GetRawJsonAsync(
                It.Is<string>(url => url.Contains("event2")), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Failed to fetch event2"));

            _mockHttpService.Setup(s => s.GetRawJsonAsync(
                It.Is<string>(url => url.Contains("event3")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockBoxScoreHtml);

            // Act
            var result = await _service.ExtractBulkGamePlayerStatsAsync(eventIds);

            // Assert
            Assert.NotNull(result);
            var resultList = result.ToList();
            Assert.NotEmpty(resultList); // Should have stats from successful games

            // Verify all HTTP calls were attempted
            _mockHttpService.Verify(s => s.GetRawJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        [Fact]
        public async Task ExtractBulkGamePlayerStatsAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var eventIds = new List<string> { "event1", "event2" };
            using var cts = new CancellationTokenSource();

            _mockHttpService.Setup(s => s.GetRawJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string url, CancellationToken ct) =>
                {
                    cts.Cancel(); // Cancel during execution
                    ct.ThrowIfCancellationRequested();
                    return Task.FromResult(CreateMockBoxScoreHtml());
                });

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                _service.ExtractBulkGamePlayerStatsAsync(eventIds, 1, cts.Token));
        }

        [Fact]
        public async Task StreamParsePlayerStatsAsync_ValidBoxScoreStream_ReturnsStreamedStats()
        {
            // Arrange
            var gameInfo = CreateMockGameEvent();
            var boxScoreJson = CreateMockBoxScoreJsonForStreaming();
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(boxScoreJson));

            // Act
            var results = new List<PlayerStats>();
            await foreach (var playerStats in _service.StreamParsePlayerStatsAsync(stream, gameInfo))
            {
                results.Add(playerStats);
            }

            // Assert
            Assert.NotNull(results);
            Assert.NotEmpty(results);
            Assert.All(results, stats =>
            {
                Assert.NotNull(stats.PlayerId);
                Assert.NotNull(stats.DisplayName);
                Assert.Equal(gameInfo.Id, stats.GameId);
            });
        }

        [Fact]
        public async Task StreamParsePlayerStatsAsync_EmptyStream_ReturnsEmpty()
        {
            // Arrange
            var gameInfo = CreateMockGameEvent();
            var emptyJson = "{}";
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(emptyJson));

            // Act
            var results = new List<PlayerStats>();
            await foreach (var playerStats in _service.StreamParsePlayerStatsAsync(stream, gameInfo))
            {
                results.Add(playerStats);
            }

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task ValidateBulkPlayerStatsAsync_ValidStats_ReturnsCorrectValidation()
        {
            // Arrange
            var playerStatsCollection = new List<PlayerStats>
            {
                CreateMockPlayerStats("player1", "game1"),
                CreateMockPlayerStats("player2", "game2")
            };

            // Act
            var result = await _service.ValidateBulkPlayerStatsAsync(playerStatsCollection);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.All(result.Values, isValid => Assert.True(isValid));
        }

        [Fact]
        public async Task ValidateBulkPlayerStatsAsync_EmptyCollection_ReturnsEmptyResult()
        {
            // Arrange
            var playerStatsCollection = new List<PlayerStats>();

            // Act
            var result = await _service.ValidateBulkPlayerStatsAsync(playerStatsCollection);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task ValidateBulkPlayerStatsAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var playerStatsCollection = new List<PlayerStats>
            {
                CreateMockPlayerStats("player1", "game1")
            };
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                _service.ValidateBulkPlayerStatsAsync(playerStatsCollection, cts.Token));
        }

        #endregion

        #region Helper Methods for Bulk Tests

        private static GameEvent CreateMockGameEvent()
        {
            return new GameEvent
            {
                Id = "test-game-id",
                Name = "Test Game",
                ShortName = "TEST",
                Date = DateTime.Now,
                Status = new GameStatus { DisplayClock = "Final" },
                Season = new Season { Year = 2024, SeasonType = 2 },
                Week = new Week { WeekNumber = 1, Year = 2024 }
            };
        }

        private static PlayerStats CreateMockPlayerStats(string playerId, string gameId)
        {
            return new PlayerStats
            {
                PlayerId = playerId,
                DisplayName = $"Player {playerId}",
                ShortName = $"P. {playerId}",
                Position = new PlayerPosition { Name = "Quarterback", Abbreviation = "QB" },
                GameId = gameId,
                Season = 2024,
                Week = 1,
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
                        Name = "touchdowns",
                        DisplayName = "Touchdowns",
                        Category = "passing",
                        Value = 2,
                        DisplayValue = "2"
                    }
                }
            };
        }

        private static string CreateMockBoxScoreJsonForStreaming()
        {
            return """
                {
                    "boxscore": {
                        "players": [
                            {
                                "team": {
                                    "id": "1",
                                    "displayName": "Team A"
                                },
                                "statistics": [
                                    {
                                        "athletes": [
                                            {
                                                "athlete": {
                                                    "id": "player1",
                                                    "displayName": "Player One",
                                                    "shortName": "P. One",
                                                    "position": {
                                                        "name": "Quarterback",
                                                        "abbreviation": "QB"
                                                    }
                                                },
                                                "stats": [
                                                    {
                                                        "name": "passingYards",
                                                        "value": "300"
                                                    },
                                                    {
                                                        "name": "touchdowns",
                                                        "value": "2"
                                                    }
                                                ]
                                            },
                                            {
                                                "athlete": {
                                                    "id": "player2",
                                                    "displayName": "Player Two",
                                                    "shortName": "P. Two",
                                                    "position": {
                                                        "name": "Running Back",
                                                        "abbreviation": "RB"
                                                    }
                                                },
                                                "stats": [
                                                    {
                                                        "name": "rushingYards",
                                                        "value": "150"
                                                    },
                                                    {
                                                        "name": "touchdowns",
                                                        "value": "1"
                                                    }
                                                ]
                                            }
                                        ]
                                    }
                                ]
                            }
                        ]
                    }
                }
                """;
        }

        #endregion
    }
}