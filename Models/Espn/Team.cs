using System.Text.Json.Serialization;

namespace ESPNScrape.Models.Espn
{
    public class Team : IEquatable<Team>
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("uid")]
        public string Uid { get; set; } = string.Empty;

        [JsonPropertyName("slug")]
        public string Slug { get; set; } = string.Empty;

        [JsonPropertyName("location")]
        public string Location { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("nickname")]
        public string Nickname { get; set; } = string.Empty;

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("shortDisplayName")]
        public string ShortDisplayName { get; set; } = string.Empty;

        [JsonPropertyName("color")]
        public string Color { get; set; } = string.Empty;

        [JsonPropertyName("alternateColor")]
        public string AlternateColor { get; set; } = string.Empty;

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }

        [JsonPropertyName("venue")]
        public Venue? Venue { get; set; }

        [JsonPropertyName("links")]
        public List<TeamLink> Links { get; set; } = new();

        [JsonPropertyName("logo")]
        public string Logo { get; set; } = string.Empty;

        [JsonPropertyName("logos")]
        public List<TeamLogo> Logos { get; set; } = new();

        public bool Equals(Team? other) => other != null && Id == other.Id;
        public override bool Equals(object? obj) => Equals(obj as Team);
        public override int GetHashCode() => Id.GetHashCode();
    }

    public class TeamLink
    {
        [JsonPropertyName("language")]
        public string Language { get; set; } = string.Empty;

        [JsonPropertyName("rel")]
        public List<string> Rel { get; set; } = new();

        [JsonPropertyName("href")]
        public string Href { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("shortText")]
        public string ShortText { get; set; } = string.Empty;

        [JsonPropertyName("isExternal")]
        public bool IsExternal { get; set; }

        [JsonPropertyName("isPremium")]
        public bool IsPremium { get; set; }
    }

    public class TeamLogo
    {
        [JsonPropertyName("href")]
        public string Href { get; set; } = string.Empty;

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("alt")]
        public string Alt { get; set; } = string.Empty;

        [JsonPropertyName("rel")]
        public List<string> Rel { get; set; } = new();

        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; }
    }
}