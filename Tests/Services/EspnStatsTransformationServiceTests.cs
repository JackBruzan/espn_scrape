using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using ESPNScrape.Services;
using ESPNScrape.Models.Espn;
using ESPNScrape.Models.DataSync;

namespace ESPNScrape.Tests.Services
{
    public class EspnStatsTransformationServiceTests
    {
        private readonly Mock<ILogger<EspnStatsTransformationService>> _mockLogger;
        private readonly EspnStatsTransformationService _service;

        public EspnStatsTransformationServiceTests()
        {
            _mockLogger = new Mock<ILogger<EspnStatsTransformationService>>();
            _service = new EspnStatsTransformationService(_mockLogger.Object);
        }

        #region TransformPlayerStatsAsync Tests

        [Fact]
        public async Task TransformPlayerStatsAsync_WithValidPlayerStats_ShouldReturnDatabasePlayerStats()
        {
            // Arrange
            var espnStats = CreateSamplePlayerStats();

            // Act
            var result = await _service.TransformPlayerStatsAsync(espnStats);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(espnStats.PlayerId, result.EspnPlayerId);
            Assert.Equal(espnStats.GameId, result.EspnGameId);
            Assert.Equal(espnStats.DisplayName, result.Name);
            Assert.Equal(espnStats.Team.Abbreviation, result.Team);
            Assert.Equal(espnStats.Season, result.Season);
            Assert.Equal(espnStats.Week, result.Week);
            Assert.Equal(espnStats.Position.Abbreviation, result.Position);
            Assert.Equal(espnStats.Jersey, result.Jersey);
        }

        [Fact]
        public async Task TransformPlayerStatsAsync_WithPassingStats_ShouldOrganizePassingStatsCorrectly()
        {
            // Arrange
            var espnStats = CreateSamplePlayerStatsWithPassingStats();

            // Act
            var result = await _service.TransformPlayerStatsAsync(espnStats);

            // Assert
            Assert.NotNull(result.Passing);
            var passingStats = result.Passing as Dictionary<string, decimal>;
            Assert.NotNull(passingStats);
            Assert.True(passingStats.ContainsKey("passingyards"));
            Assert.True(passingStats.ContainsKey("passingtouchdowns"));
            Assert.Equal(300, passingStats["passingyards"]);
            Assert.Equal(2, passingStats["passingtouchdowns"]);
        }

        [Fact]
        public async Task TransformPlayerStatsAsync_WithNoStats_ShouldReturnNullStatCategories()
        {
            // Arrange
            var espnStats = CreateSamplePlayerStats();
            espnStats.Statistics = new List<PlayerStatistic>(); // Empty stats

            // Act
            var result = await _service.TransformPlayerStatsAsync(espnStats);

            // Assert
            Assert.Null(result.Passing);
            Assert.Null(result.Rushing);
            Assert.Null(result.Receiving);
            Assert.Null(result.Defensive);
            Assert.Null(result.Kicking);
            Assert.Null(result.Punting);
            Assert.Null(result.General);
        }

        #endregion

        #region TransformPlayerStatsBatchAsync Tests

        [Fact]
        public async Task TransformPlayerStatsBatchAsync_WithValidList_ShouldTransformAllSuccessfully()
        {
            // Arrange
            var espnStatsList = new List<PlayerStats>
            {
                CreateSamplePlayerStats("player1", "game1"),
                CreateSamplePlayerStats("player2", "game1"),
                CreateSamplePlayerStats("player3", "game1")
            };

            // Act
            var result = await _service.TransformPlayerStatsBatchAsync(espnStatsList);

            // Assert
            Assert.Equal(3, result.Count);
            Assert.All(result, r => Assert.False(string.IsNullOrEmpty(r.EspnPlayerId)));
        }

        [Fact]
        public async Task TransformPlayerStatsBatchAsync_WithEmptyList_ShouldReturnEmptyList()
        {
            // Arrange
            var emptyList = new List<PlayerStats>();

            // Act
            var result = await _service.TransformPlayerStatsBatchAsync(emptyList);

            // Assert
            Assert.Empty(result);
        }

        #endregion

        #region ValidatePlayerStats Tests

        [Fact]
        public void ValidatePlayerStats_WithValidStats_ShouldReturnValidResult()
        {
            // Arrange
            var validStats = CreateSamplePlayerStats();

            // Act
            var result = _service.ValidatePlayerStats(validStats);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ValidatePlayerStats_WithMissingPlayerId_ShouldReturnInvalid()
        {
            // Arrange
            var invalidStats = CreateSamplePlayerStats();
            invalidStats.PlayerId = string.Empty;

            // Act
            var result = _service.ValidatePlayerStats(invalidStats);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("Player ID is required"));
        }

        [Fact]
        public void ValidatePlayerStats_WithMissingDisplayName_ShouldReturnInvalid()
        {
            // Arrange
            var invalidStats = CreateSamplePlayerStats();
            invalidStats.DisplayName = string.Empty;

            // Act
            var result = _service.ValidatePlayerStats(invalidStats);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("Player display name is required"));
        }

        [Fact]
        public void ValidatePlayerStats_WithInvalidSeason_ShouldReturnInvalid()
        {
            // Arrange
            var invalidStats = CreateSamplePlayerStats();
            invalidStats.Season = 1800; // Too old

            // Act
            var result = _service.ValidatePlayerStats(invalidStats);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("Season") && e.Contains("not in valid range"));
        }

        #endregion

        #region OrganizeStatsByCategory Tests

        [Fact]
        public void OrganizeStatsByCategory_WithMixedStats_ShouldCategorizeCorrectly()
        {
            // Arrange
            var statistics = new List<PlayerStatistic>
            {
                new PlayerStatistic { Name = "passingYards", Value = 300, Category = "passing" },
                new PlayerStatistic { Name = "rushingCarries", Value = 15, Category = "rushing" },
                new PlayerStatistic { Name = "receivingTargets", Value = 8, Category = "receiving" },
                new PlayerStatistic { Name = "totalTackles", Value = 5, Category = "defensive" }
            };

            // Act
            var result = _service.OrganizeStatsByCategory(statistics);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HasAnyStats);
            Assert.True(result.Passing.ContainsKey("passingyards"));
            Assert.True(result.Rushing.ContainsKey("rushingcarries"));
            Assert.True(result.Receiving.ContainsKey("receivingtargets"));
            Assert.True(result.Defensive.ContainsKey("totaltackles"));
        }

        [Fact]
        public void OrganizeStatsByCategory_WithEmptyList_ShouldReturnEmptyCategories()
        {
            // Arrange
            var emptyStatistics = new List<PlayerStatistic>();

            // Act
            var result = _service.OrganizeStatsByCategory(emptyStatistics);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.HasAnyStats);
            Assert.Empty(result.Passing);
            Assert.Empty(result.Rushing);
            Assert.Empty(result.Receiving);
        }

        #endregion

        #region Helper Methods

        private PlayerStats CreateSamplePlayerStats(string playerId = "12345", string gameId = "401547417")
        {
            return new PlayerStats
            {
                PlayerId = playerId,
                DisplayName = "John Doe",
                ShortName = "J. Doe",
                GameId = gameId,
                Season = 2024,
                Week = 1,
                SeasonType = 2,
                Jersey = "10",
                Team = new Team
                {
                    Id = "1",
                    Abbreviation = "KC",
                    DisplayName = "Kansas City Chiefs"
                },
                Position = new PlayerPosition
                {
                    Id = "1",
                    Name = "Quarterback",
                    Abbreviation = "QB",
                    DisplayName = "Quarterback"
                },
                Statistics = new List<PlayerStatistic>
                {
                    new PlayerStatistic
                    {
                        Name = "passingAttempts",
                        Value = 25,
                        Category = "passing"
                    }
                }
            };
        }

        private PlayerStats CreateSamplePlayerStatsWithPassingStats()
        {
            var stats = CreateSamplePlayerStats();
            stats.Statistics = new List<PlayerStatistic>
            {
                new PlayerStatistic { Name = "passingYards", Value = 300, Category = "passing" },
                new PlayerStatistic { Name = "passingTouchdowns", Value = 2, Category = "passing" },
                new PlayerStatistic { Name = "passingCompletions", Value = 20, Category = "passing" },
                new PlayerStatistic { Name = "passingAttempts", Value = 30, Category = "passing" }
            };
            return stats;
        }

        #endregion
    }
}