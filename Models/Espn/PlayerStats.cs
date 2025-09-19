using System.Text.Json.Serialization;

namespace ESPNScrape.Models.Espn
{
    /// <summary>
    /// Represents comprehensive player statistics for an ESPN NFL game
    /// </summary>
    public class PlayerStats : IEquatable<PlayerStats>
    {
        [JsonPropertyName("playerId")]
        public string PlayerId { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("shortName")]
        public string ShortName { get; set; } = string.Empty;

        [JsonPropertyName("team")]
        public Team Team { get; set; } = new();

        [JsonPropertyName("position")]
        public PlayerPosition Position { get; set; } = new();

        [JsonPropertyName("jersey")]
        public string Jersey { get; set; } = string.Empty;

        [JsonPropertyName("statistics")]
        public List<PlayerStatistic> Statistics { get; set; } = new();

        [JsonPropertyName("gameId")]
        public string GameId { get; set; } = string.Empty;

        [JsonPropertyName("season")]
        public int Season { get; set; }

        [JsonPropertyName("week")]
        public int Week { get; set; }

        [JsonPropertyName("seasonType")]
        public int SeasonType { get; set; }

        public bool Equals(PlayerStats? other) =>
            other != null && PlayerId == other.PlayerId && GameId == other.GameId;

        public override bool Equals(object? obj) => Equals(obj as PlayerStats);

        public override int GetHashCode() => HashCode.Combine(PlayerId, GameId);
    }

    /// <summary>
    /// Individual player statistic entry
    /// </summary>
    public class PlayerStatistic
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("shortDisplayName")]
        public string ShortDisplayName { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        [JsonPropertyName("displayValue")]
        public string DisplayValue { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;
    }

    /// <summary>
    /// Player position information
    /// </summary>
    public class PlayerPosition
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;

        [JsonPropertyName("leaf")]
        public bool Leaf { get; set; }

        [JsonPropertyName("parent")]
        public PlayerPositionParent? Parent { get; set; }
    }

    /// <summary>
    /// Parent position category (e.g., Offense, Defense, Special Teams)
    /// </summary>
    public class PlayerPositionParent
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;
    }
}