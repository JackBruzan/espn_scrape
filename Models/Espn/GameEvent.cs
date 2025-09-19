using System.Text.Json.Serialization;

namespace ESPNScrape.Models.Espn
{
    public class GameEvent : IEquatable<GameEvent>
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("uid")]
        public string Uid { get; set; } = string.Empty;

        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("shortName")]
        public string ShortName { get; set; } = string.Empty;

        [JsonPropertyName("season")]
        public Season? Season { get; set; }

        [JsonPropertyName("week")]
        public Week? Week { get; set; }

        [JsonPropertyName("competitions")]
        public List<Competition> Competitions { get; set; } = new();

        [JsonPropertyName("status")]
        public GameStatus? Status { get; set; }

        [JsonPropertyName("venue")]
        public Venue? Venue { get; set; }

        [JsonPropertyName("timeValid")]
        public bool TimeValid { get; set; }

        public bool Equals(GameEvent? other) => other != null && Id == other.Id;
        public override bool Equals(object? obj) => Equals(obj as GameEvent);
        public override int GetHashCode() => Id.GetHashCode();
    }

    public class Competition
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("uid")]
        public string Uid { get; set; } = string.Empty;

        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

        [JsonPropertyName("attendance")]
        public int? Attendance { get; set; }

        [JsonPropertyName("type")]
        public CompetitionType? Type { get; set; }

        [JsonPropertyName("timeValid")]
        public bool TimeValid { get; set; }

        [JsonPropertyName("neutralSite")]
        public bool NeutralSite { get; set; }

        [JsonPropertyName("conferenceCompetition")]
        public bool ConferenceCompetition { get; set; }

        [JsonPropertyName("playByPlayAvailable")]
        public bool PlayByPlayAvailable { get; set; }

        [JsonPropertyName("recent")]
        public bool Recent { get; set; }

        [JsonPropertyName("venue")]
        public Venue? Venue { get; set; }

        [JsonPropertyName("competitors")]
        public List<Competitor> Competitors { get; set; } = new();

        [JsonPropertyName("status")]
        public GameStatus? Status { get; set; }
    }

    public class CompetitionType
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;
    }

    public class Competitor
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("uid")]
        public string Uid { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("order")]
        public int Order { get; set; }

        [JsonPropertyName("homeAway")]
        public string HomeAway { get; set; } = string.Empty;

        [JsonPropertyName("team")]
        public Team? Team { get; set; }

        [JsonPropertyName("score")]
        public string Score { get; set; } = string.Empty;

        [JsonPropertyName("linescores")]
        public List<LineScore> LineScores { get; set; } = new();

        [JsonPropertyName("statistics")]
        public List<TeamStatistic> Statistics { get; set; } = new();

        [JsonPropertyName("records")]
        public List<TeamRecord> Records { get; set; } = new();
    }

    public class LineScore
    {
        [JsonPropertyName("value")]
        public int Value { get; set; }

        [JsonPropertyName("displayValue")]
        public string DisplayValue { get; set; } = string.Empty;
    }

    public class TeamStatistic
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;

        [JsonPropertyName("displayValue")]
        public string DisplayValue { get; set; } = string.Empty;
    }

    public class TeamRecord
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;
    }
}