using System.Text.Json.Serialization;

namespace ESPNScrape.Models.Espn
{
    /// <summary>
    /// Represents a complete box score with team and player statistics for an ESPN NFL game
    /// </summary>
    public class BoxScore : IEquatable<BoxScore>
    {
        [JsonPropertyName("gameId")]
        public string GameId { get; set; } = string.Empty;

        [JsonPropertyName("teams")]
        public List<TeamBoxScore> Teams { get; set; } = new();

        [JsonPropertyName("players")]
        public List<PlayerStats> Players { get; set; } = new();

        [JsonPropertyName("gameInfo")]
        public GameInfo GameInfo { get; set; } = new();

        [JsonPropertyName("drives")]
        public List<Drive> Drives { get; set; } = new();

        [JsonPropertyName("scoringPlays")]
        public List<ScoringPlay> ScoringPlays { get; set; } = new();

        [JsonPropertyName("timeouts")]
        public Dictionary<string, List<Timeout>> Timeouts { get; set; } = new();

        [JsonPropertyName("lastModified")]
        public DateTime LastModified { get; set; }

        public bool Equals(BoxScore? other) => other != null && GameId == other.GameId;
        public override bool Equals(object? obj) => Equals(obj as BoxScore);
        public override int GetHashCode() => GameId.GetHashCode();
    }

    /// <summary>
    /// Team-level statistics within a box score
    /// </summary>
    public class TeamBoxScore
    {
        [JsonPropertyName("team")]
        public Team Team { get; set; } = new();

        [JsonPropertyName("statistics")]
        public List<BoxScoreTeamStatistic> Statistics { get; set; } = new();

        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("linescores")]
        public List<BoxScoreLineScore> LineScores { get; set; } = new();
    }

    /// <summary>
    /// Individual team statistic within a box score
    /// </summary>
    public class BoxScoreTeamStatistic
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("displayValue")]
        public string DisplayValue { get; set; } = string.Empty;
    }

    /// <summary>
    /// Quarter/period scoring information
    /// </summary>
    public class BoxScoreLineScore
    {
        [JsonPropertyName("period")]
        public int Period { get; set; }

        [JsonPropertyName("value")]
        public int Value { get; set; }

        [JsonPropertyName("displayValue")]
        public string DisplayValue { get; set; } = string.Empty;
    }

    /// <summary>
    /// General game information for box score context
    /// </summary>
    public class GameInfo
    {
        [JsonPropertyName("attendance")]
        public int? Attendance { get; set; }

        [JsonPropertyName("officials")]
        public List<Official> Officials { get; set; } = new();

        [JsonPropertyName("weather")]
        public Weather? Weather { get; set; }

        [JsonPropertyName("venue")]
        public Venue Venue { get; set; } = new();
    }

    /// <summary>
    /// Game official information
    /// </summary>
    public class Official
    {
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("position")]
        public string Position { get; set; } = string.Empty;
    }

    /// <summary>
    /// Weather conditions during the game
    /// </summary>
    public class Weather
    {
        [JsonPropertyName("temperature")]
        public int? Temperature { get; set; }

        [JsonPropertyName("conditions")]
        public string Conditions { get; set; } = string.Empty;

        [JsonPropertyName("windSpeed")]
        public string WindSpeed { get; set; } = string.Empty;

        [JsonPropertyName("humidity")]
        public string Humidity { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a scoring drive in the game
    /// </summary>
    public class Drive
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("team")]
        public Team Team { get; set; } = new();

        [JsonPropertyName("plays")]
        public int Plays { get; set; }

        [JsonPropertyName("yards")]
        public int Yards { get; set; }

        [JsonPropertyName("timeElapsed")]
        public string TimeElapsed { get; set; } = string.Empty;

        [JsonPropertyName("result")]
        public string Result { get; set; } = string.Empty;
    }

    /// <summary>
    /// Individual scoring play information
    /// </summary>
    public class ScoringPlay
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("team")]
        public Team Team { get; set; } = new();

        [JsonPropertyName("period")]
        public int Period { get; set; }

        [JsonPropertyName("clock")]
        public string Clock { get; set; } = string.Empty;

        [JsonPropertyName("scoreValue")]
        public int ScoreValue { get; set; }
    }

    /// <summary>
    /// Team timeout information
    /// </summary>
    public class Timeout
    {
        [JsonPropertyName("period")]
        public int Period { get; set; }

        [JsonPropertyName("clock")]
        public string Clock { get; set; } = string.Empty;

        [JsonPropertyName("team")]
        public Team Team { get; set; } = new();
    }
}