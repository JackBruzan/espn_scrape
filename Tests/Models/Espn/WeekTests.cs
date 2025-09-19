using ESPNScrape.Models.Espn;
using System.Text.Json;
using Xunit;

namespace ESPNScrape.Tests.Models.Espn
{
    public class WeekTests
    {
        [Fact]
        public void Week_Serialization_RoundTrip_Success()
        {
            // Arrange
            var week = new Week
            {
                WeekNumber = 3,
                SeasonType = 2,
                Text = "Week 3",
                Label = "Week 3",
                StartDate = new DateTime(2025, 9, 15),
                EndDate = new DateTime(2025, 9, 21),
                Url = "/nfl/schedule/_/week/3/year/2025/seasontype/2",
                IsActive = true,
                Year = 2025,
                Events = new EventsReference { ApiReference = "http://sports.core.api.espn.pvt/v2/sports/football/leagues/nfl/seasons/2025/types/2/weeks/3/events" }
            };

            // Act
            var json = JsonSerializer.Serialize(week);
            var deserialized = JsonSerializer.Deserialize<Week>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(week.WeekNumber, deserialized.WeekNumber);
            Assert.Equal(week.SeasonType, deserialized.SeasonType);
            Assert.Equal(week.Text, deserialized.Text);
            Assert.Equal(week.Year, deserialized.Year);
            Assert.NotNull(deserialized.Events);
        }

        [Fact]
        public void Week_Equality_WorksCorrectly()
        {
            // Arrange
            var week1 = new Week { WeekNumber = 3, SeasonType = 2, Year = 2025 };
            var week2 = new Week { WeekNumber = 3, SeasonType = 2, Year = 2025 };
            var week3 = new Week { WeekNumber = 4, SeasonType = 2, Year = 2025 };

            // Act & Assert
            Assert.Equal(week1, week2);
            Assert.NotEqual(week1, week3);
        }

        [Fact]
        public void Week_JsonPropertyNames_AreCorrect()
        {
            // Arrange
            var week = new Week
            {
                WeekNumber = 1,
                SeasonType = 2,
                Text = "Week 1",
                IsActive = true,
                Year = 2025
            };

            // Act
            var json = JsonSerializer.Serialize(week);

            // Assert
            Assert.Contains("\"weekNumber\":1", json);
            Assert.Contains("\"seasonType\":2", json);
            Assert.Contains("\"text\":\"Week 1\"", json);
            Assert.Contains("\"isActive\":true", json);
            Assert.Contains("\"year\":2025", json);
        }
    }
}