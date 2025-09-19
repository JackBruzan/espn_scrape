using ESPNScrape.Models.Espn;
using System.Text.Json;
using Xunit;

namespace ESPNScrape.Tests.Models.Espn
{
    public class SeasonTests
    {
        [Fact]
        public void Season_Serialization_RoundTrip_Success()
        {
            // Arrange
            var season = new Season
            {
                Year = 2025,
                DisplayName = "2025",
                StartDate = new DateTime(2025, 8, 1),
                EndDate = new DateTime(2026, 2, 15),
                SeasonType = 2,
                Weeks = new List<Week>
                {
                    new Week { WeekNumber = 1, SeasonType = 2, Year = 2025, Text = "Week 1" }
                }
            };

            // Act
            var json = JsonSerializer.Serialize(season);
            var deserialized = JsonSerializer.Deserialize<Season>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(season.Year, deserialized.Year);
            Assert.Equal(season.DisplayName, deserialized.DisplayName);
            Assert.Equal(season.Weeks.Count, deserialized.Weeks.Count);
        }

        [Fact]
        public void Season_Equality_WorksCorrectly()
        {
            // Arrange
            var season1 = new Season { Year = 2025, SeasonType = 2 };
            var season2 = new Season { Year = 2025, SeasonType = 2 };
            var season3 = new Season { Year = 2024, SeasonType = 2 };

            // Act & Assert
            Assert.Equal(season1, season2);
            Assert.NotEqual(season1, season3);
        }

        [Fact]
        public void Season_JsonPropertyNames_AreCorrect()
        {
            // Arrange
            var season = new Season
            {
                Year = 2025,
                DisplayName = "2025 NFL Season",
                StartDate = new DateTime(2025, 8, 1),
                EndDate = new DateTime(2026, 2, 15),
                SeasonType = 2
            };

            // Act
            var json = JsonSerializer.Serialize(season);

            // Assert
            Assert.Contains("\"year\":2025", json);
            Assert.Contains("\"displayName\":\"2025 NFL Season\"", json);
            Assert.Contains("\"seasonType\":2", json);
        }

        [Fact]
        public void Season_HashCode_ConsistentForEqualObjects()
        {
            // Arrange
            var season1 = new Season { Year = 2025, SeasonType = 2 };
            var season2 = new Season { Year = 2025, SeasonType = 2 };

            // Act & Assert
            Assert.Equal(season1.GetHashCode(), season2.GetHashCode());
        }

        [Fact]
        public void Season_HashCode_DifferentForDifferentObjects()
        {
            // Arrange
            var season1 = new Season { Year = 2025, SeasonType = 2 };
            var season2 = new Season { Year = 2024, SeasonType = 2 };

            // Act & Assert
            Assert.NotEqual(season1.GetHashCode(), season2.GetHashCode());
        }

        [Fact]
        public void Season_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var season = new Season();

            // Assert
            Assert.Equal(string.Empty, season.DisplayName);
            Assert.NotNull(season.Weeks);
            Assert.Empty(season.Weeks);
            Assert.Equal(0, season.Year);
            Assert.Equal(0, season.SeasonType);
        }
    }
}