using System.Text.Json.Serialization;

namespace ESPNScrape.Models.Espn
{
    // Type alias for consistency with the TICKET-005 specification
    using Event = GameEvent;

    public class ScoreboardData
    {
        [JsonPropertyName("events")]
        public IEnumerable<Event> Events { get; set; } = new List<Event>();

        [JsonPropertyName("season")]
        public Season Season { get; set; } = new Season();

        [JsonPropertyName("week")]
        public Week Week { get; set; } = new Week();

        [JsonPropertyName("leagues")]
        public IEnumerable<League> Leagues { get; set; } = new List<League>();
    }

    public class League
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;

        [JsonPropertyName("season")]
        public Season Season { get; set; } = new Season();
    }
}