using ESPNScrape.Models.Espn;
using System.Text.Json;
using Xunit;

namespace ESPNScrape.Tests.Models.Espn
{
    public class EventsReferenceTests
    {
        [Fact]
        public void EventsReference_Serialization_RoundTrip_Success()
        {
            // Arrange
            var eventsRef = new EventsReference
            {
                ApiReference = "http://sports.core.api.espn.pvt/v2/sports/football/leagues/nfl/seasons/2025/types/2/weeks/3/events"
            };

            // Act
            var json = JsonSerializer.Serialize(eventsRef);
            var deserialized = JsonSerializer.Deserialize<EventsReference>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(eventsRef.ApiReference, deserialized.ApiReference);
        }

        [Fact]
        public void EventsReference_JsonPropertyNames_AreCorrect()
        {
            // Arrange
            var eventsRef = new EventsReference
            {
                ApiReference = "http://sports.core.api.espn.pvt/v2/sports/football/leagues/nfl/seasons/2025/types/2/weeks/3/events"
            };

            // Act
            var json = JsonSerializer.Serialize(eventsRef);

            // Assert
            Assert.Contains("\"$ref\":", json);
            Assert.Contains("http://sports.core.api.espn.pvt/v2/sports/football/leagues/nfl/seasons/2025/types/2/weeks/3/events", json);
        }

        [Fact]
        public void EventsReference_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var eventsRef = new EventsReference();

            // Assert
            Assert.Equal(string.Empty, eventsRef.ApiReference);
        }
    }
}