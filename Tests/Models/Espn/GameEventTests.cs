using ESPNScrape.Models.Espn;
using System.Text.Json;
using Xunit;

namespace ESPNScrape.Tests.Models.Espn
{
    public class GameEventTests
    {
        [Fact]
        public void GameEvent_Serialization_RoundTrip_Success()
        {
            // Arrange
            var gameEvent = new GameEvent
            {
                Id = "401671696",
                Uid = "s:20~l:28~e:401671696",
                Date = new DateTime(2025, 9, 14, 20, 15, 0),
                Name = "Kansas City Chiefs at Baltimore Ravens",
                ShortName = "KC @ BAL",
                TimeValid = true,
                Competitions = new List<Competition>
                {
                    new Competition
                    {
                        Id = "401671696",
                        Date = new DateTime(2025, 9, 14, 20, 15, 0),
                        TimeValid = true,
                        NeutralSite = false
                    }
                }
            };

            // Act
            var json = JsonSerializer.Serialize(gameEvent);
            var deserialized = JsonSerializer.Deserialize<GameEvent>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(gameEvent.Id, deserialized.Id);
            Assert.Equal(gameEvent.Name, deserialized.Name);
            Assert.Equal(gameEvent.Competitions.Count, deserialized.Competitions.Count);
        }

        [Fact]
        public void GameEvent_Equality_WorksCorrectly()
        {
            // Arrange
            var event1 = new GameEvent { Id = "401671696" };
            var event2 = new GameEvent { Id = "401671696" };
            var event3 = new GameEvent { Id = "401671697" };

            // Act & Assert
            Assert.Equal(event1, event2);
            Assert.NotEqual(event1, event3);
        }

        [Fact]
        public void GameEvent_JsonPropertyNames_AreCorrect()
        {
            // Arrange
            var gameEvent = new GameEvent
            {
                Id = "401671696",
                Name = "Test Game",
                ShortName = "TEST",
                TimeValid = true
            };

            // Act
            var json = JsonSerializer.Serialize(gameEvent);

            // Assert
            Assert.Contains("\"id\":\"401671696\"", json);
            Assert.Contains("\"name\":\"Test Game\"", json);
            Assert.Contains("\"shortName\":\"TEST\"", json);
            Assert.Contains("\"timeValid\":true", json);
        }
    }
}