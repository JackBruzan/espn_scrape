using System.Text.Json.Serialization;

namespace ESPNScrape.Models.Espn
{
    public class GameStatus : IEquatable<GameStatus>
    {
        [JsonPropertyName("clock")]
        public double Clock { get; set; }

        [JsonPropertyName("displayClock")]
        public string DisplayClock { get; set; } = string.Empty;

        [JsonPropertyName("period")]
        public int Period { get; set; }

        [JsonPropertyName("type")]
        public StatusType? Type { get; set; }

        [JsonPropertyName("featuredAthletes")]
        public List<FeaturedAthlete> FeaturedAthletes { get; set; } = new();

        public bool Equals(GameStatus? other) =>
            other != null &&
            Clock == other.Clock &&
            Period == other.Period &&
            Type?.Id == other.Type?.Id;

        public override bool Equals(object? obj) => Equals(obj as GameStatus);
        public override int GetHashCode() => HashCode.Combine(Clock, Period, Type?.Id);
    }

    public class StatusType
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("completed")]
        public bool Completed { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("detail")]
        public string Detail { get; set; } = string.Empty;

        [JsonPropertyName("shortDetail")]
        public string ShortDetail { get; set; } = string.Empty;
    }

    public class FeaturedAthlete
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("shortName")]
        public string ShortName { get; set; } = string.Empty;

        [JsonPropertyName("headshot")]
        public string Headshot { get; set; } = string.Empty;

        [JsonPropertyName("jersey")]
        public string Jersey { get; set; } = string.Empty;

        [JsonPropertyName("position")]
        public AthletePosition? Position { get; set; }

        [JsonPropertyName("team")]
        public Team? Team { get; set; }

        [JsonPropertyName("playerId")]
        public string PlayerId { get; set; } = string.Empty;
    }

    public class AthletePosition
    {
        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;
    }
}