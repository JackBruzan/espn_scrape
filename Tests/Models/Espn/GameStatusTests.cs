using ESPNScrape.Models.Espn;
using System.Text.Json;
using Xunit;

namespace ESPNScrape.Tests.Models.Espn
{
    public class GameStatusTests
    {
        [Fact]
        public void GameStatus_Serialization_RoundTrip_Success()
        {
            // Arrange
            var gameStatus = new GameStatus
            {
                Clock = 0.0,
                DisplayClock = "0:00",
                Period = 4,
                Type = new StatusType
                {
                    Id = "3",
                    Name = "STATUS_FINAL",
                    State = "post",
                    Completed = true,
                    Description = "Final",
                    Detail = "Final",
                    ShortDetail = "Final"
                }
            };

            // Act
            var json = JsonSerializer.Serialize(gameStatus);
            var deserialized = JsonSerializer.Deserialize<GameStatus>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(gameStatus.Clock, deserialized.Clock);
            Assert.Equal(gameStatus.DisplayClock, deserialized.DisplayClock);
            Assert.Equal(gameStatus.Period, deserialized.Period);
            Assert.NotNull(deserialized.Type);
            Assert.Equal(gameStatus.Type.Id, deserialized.Type.Id);
        }

        [Fact]
        public void GameStatus_Equality_WorksCorrectly()
        {
            // Arrange
            var status1 = new GameStatus
            {
                Clock = 0.0,
                Period = 4,
                Type = new StatusType { Id = "3" }
            };
            var status2 = new GameStatus
            {
                Clock = 0.0,
                Period = 4,
                Type = new StatusType { Id = "3" }
            };
            var status3 = new GameStatus
            {
                Clock = 15.0,
                Period = 3,
                Type = new StatusType { Id = "2" }
            };

            // Act & Assert
            Assert.Equal(status1, status2);
            Assert.NotEqual(status1, status3);
        }

        [Fact]
        public void GameStatus_JsonPropertyNames_AreCorrect()
        {
            // Arrange
            var gameStatus = new GameStatus
            {
                Clock = 0.0,
                DisplayClock = "0:00",
                Period = 4
            };

            // Act
            var json = JsonSerializer.Serialize(gameStatus);

            // Assert
            Assert.Contains("\"clock\":0", json);
            Assert.Contains("\"displayClock\":\"0:00\"", json);
            Assert.Contains("\"period\":4", json);
        }
    }
}