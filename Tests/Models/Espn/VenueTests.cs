using ESPNScrape.Models.Espn;
using System.Text.Json;
using Xunit;

namespace ESPNScrape.Tests.Models.Espn
{
    public class VenueTests
    {
        [Fact]
        public void Venue_Serialization_RoundTrip_Success()
        {
            // Arrange
            var venue = new Venue
            {
                Id = "3883",
                FullName = "GEHA Field at Arrowhead Stadium",
                Capacity = 76416,
                Grass = true,
                Dome = false,
                Address = new VenueAddress
                {
                    City = "Kansas City",
                    State = "MO",
                    ZipCode = "64129"
                }
            };

            // Act
            var json = JsonSerializer.Serialize(venue);
            var deserialized = JsonSerializer.Deserialize<Venue>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(venue.Id, deserialized.Id);
            Assert.Equal(venue.FullName, deserialized.FullName);
            Assert.Equal(venue.Capacity, deserialized.Capacity);
            Assert.NotNull(deserialized.Address);
            Assert.Equal(venue.Address.City, deserialized.Address.City);
        }

        [Fact]
        public void Venue_Equality_WorksCorrectly()
        {
            // Arrange
            var venue1 = new Venue { Id = "3883" };
            var venue2 = new Venue { Id = "3883" };
            var venue3 = new Venue { Id = "3884" };

            // Act & Assert
            Assert.Equal(venue1, venue2);
            Assert.NotEqual(venue1, venue3);
        }

        [Fact]
        public void Venue_JsonPropertyNames_AreCorrect()
        {
            // Arrange
            var venue = new Venue
            {
                Id = "3883",
                FullName = "GEHA Field at Arrowhead Stadium",
                Capacity = 76416,
                Grass = true,
                Dome = false
            };

            // Act
            var json = JsonSerializer.Serialize(venue);

            // Assert
            Assert.Contains("\"id\":\"3883\"", json);
            Assert.Contains("\"fullName\":\"GEHA Field at Arrowhead Stadium\"", json);
            Assert.Contains("\"capacity\":76416", json);
            Assert.Contains("\"grass\":true", json);
            Assert.Contains("\"dome\":false", json);
        }
    }
}