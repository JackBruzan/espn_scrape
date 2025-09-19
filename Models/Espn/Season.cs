using System.Text.Json.Serialization;

namespace ESPNScrape.Models.Espn
{
    public class Season : IEquatable<Season>
    {
        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("startDate")]
        public DateTime StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public DateTime EndDate { get; set; }

        [JsonPropertyName("weeks")]
        public List<Week> Weeks { get; set; } = new();

        [JsonPropertyName("seasonType")]
        public int SeasonType { get; set; }

        public bool Equals(Season? other) => other != null && Year == other.Year && SeasonType == other.SeasonType;
        public override bool Equals(object? obj) => Equals(obj as Season);
        public override int GetHashCode() => HashCode.Combine(Year, SeasonType);
    }
}