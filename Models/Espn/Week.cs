using System.Text.Json.Serialization;

namespace ESPNScrape.Models.Espn
{
    public class Week : IEquatable<Week>
    {
        [JsonPropertyName("weekNumber")]
        public int WeekNumber { get; set; }

        [JsonPropertyName("seasonType")]
        public int SeasonType { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("startDate")]
        public DateTime StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public DateTime EndDate { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }

        [JsonPropertyName("events")]
        public EventsReference? Events { get; set; }

        [JsonPropertyName("year")]
        public int Year { get; set; }

        public bool Equals(Week? other) => other != null && WeekNumber == other.WeekNumber && SeasonType == other.SeasonType && Year == other.Year;
        public override bool Equals(object? obj) => Equals(obj as Week);
        public override int GetHashCode() => HashCode.Combine(WeekNumber, SeasonType, Year);
    }
}