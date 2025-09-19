using ESPNScrape.Models.Espn;
using System.Text.Json;
using Xunit;

namespace ESPNScrape.Tests.Models.Espn
{
    public class TeamTests
    {
        [Fact]
        public void Team_Serialization_RoundTrip_Success()
        {
            // Arrange
            var team = new Team
            {
                Id = "12",
                Uid = "s:20~l:28~t:12",
                Slug = "kansas-city-chiefs",
                Location = "Kansas City",
                Name = "Chiefs",
                Nickname = "Chiefs",
                Abbreviation = "KC",
                DisplayName = "Kansas City Chiefs",
                ShortDisplayName = "Chiefs",
                Color = "e31837",
                AlternateColor = "ffb612",
                IsActive = true,
                Logo = "https://a.espncdn.com/i/teamlogos/nfl/500/kc.png"
            };

            // Act
            var json = JsonSerializer.Serialize(team);
            var deserialized = JsonSerializer.Deserialize<Team>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(team.Id, deserialized.Id);
            Assert.Equal(team.DisplayName, deserialized.DisplayName);
            Assert.Equal(team.Abbreviation, deserialized.Abbreviation);
        }

        [Fact]
        public void Team_Equality_WorksCorrectly()
        {
            // Arrange
            var team1 = new Team { Id = "12" };
            var team2 = new Team { Id = "12" };
            var team3 = new Team { Id = "13" };

            // Act & Assert
            Assert.Equal(team1, team2);
            Assert.NotEqual(team1, team3);
        }

        [Fact]
        public void Team_JsonPropertyNames_AreCorrect()
        {
            // Arrange
            var team = new Team
            {
                Id = "12",
                DisplayName = "Kansas City Chiefs",
                Abbreviation = "KC",
                IsActive = true
            };

            // Act
            var json = JsonSerializer.Serialize(team);

            // Assert
            Assert.Contains("\"id\":\"12\"", json);
            Assert.Contains("\"displayName\":\"Kansas City Chiefs\"", json);
            Assert.Contains("\"abbreviation\":\"KC\"", json);
            Assert.Contains("\"isActive\":true", json);
        }
    }
}