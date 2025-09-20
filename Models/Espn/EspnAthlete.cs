using System.Text.Json.Serialization;

namespace ESPNScrape.Models.Espn
{
    /// <summary>
    /// ESPN API response for team roster/athletes - paginated list of athlete references
    /// </summary>
    public class EspnRosterResponse
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("pageIndex")]
        public int PageIndex { get; set; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }

        [JsonPropertyName("pageCount")]
        public int PageCount { get; set; }

        [JsonPropertyName("items")]
        public List<AthleteReference> Items { get; set; } = new();
    }

    /// <summary>
    /// Reference to an individual athlete
    /// </summary>
    public class AthleteReference
    {
        [JsonPropertyName("$ref")]
        public string Ref { get; set; } = string.Empty;
    }

    /// <summary>
    /// ESPN API response for individual athlete
    /// </summary>
    public class EspnAthleteResponse
    {
        [JsonPropertyName("athlete")]
        public EspnAthlete? Athlete { get; set; }
    }

    /// <summary>
    /// ESPN athlete/player data structure
    /// </summary>
    public class EspnAthlete
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("firstName")]
        public string FirstName { get; set; } = string.Empty;

        [JsonPropertyName("lastName")]
        public string LastName { get; set; } = string.Empty;

        [JsonPropertyName("fullName")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("shortName")]
        public string ShortName { get; set; } = string.Empty;

        [JsonPropertyName("jersey")]
        public string? Jersey { get; set; }

        [JsonPropertyName("position")]
        public EspnPosition? Position { get; set; }

        [JsonPropertyName("team")]
        public EspnTeam? Team { get; set; }

        [JsonPropertyName("height")]
        public double? Height { get; set; }

        [JsonPropertyName("age")]
        public int? Age { get; set; }

        [JsonPropertyName("birthDate")]
        public DateTime? BirthDate { get; set; }

        [JsonPropertyName("college")]
        public EspnCollege? College { get; set; }

        [JsonPropertyName("experience")]
        public EspnExperience? Experience { get; set; }

        [JsonPropertyName("headshot")]
        public EspnHeadshot? Headshot { get; set; }

        [JsonPropertyName("active")]
        public bool? Active { get; set; }

        [JsonPropertyName("status")]
        public EspnStatus? Status { get; set; }

        [JsonPropertyName("links")]
        public List<EspnLink> Links { get; set; } = new();
    }

    /// <summary>
    /// ESPN team information
    /// </summary>
    public class EspnTeam
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("shortDisplayName")]
        public string ShortDisplayName { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("nickname")]
        public string Nickname { get; set; } = string.Empty;

        [JsonPropertyName("color")]
        public string Color { get; set; } = string.Empty;

        [JsonPropertyName("alternateColor")]
        public string AlternateColor { get; set; } = string.Empty;

        [JsonPropertyName("logos")]
        public List<EspnLogo> Logos { get; set; } = new();
    }

    /// <summary>
    /// ESPN position information
    /// </summary>
    public class EspnPosition
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
    }

    /// <summary>
    /// ESPN college information
    /// </summary>
    public class EspnCollege
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("mascot")]
        public string Mascot { get; set; } = string.Empty;

        [JsonPropertyName("shortName")]
        public string ShortName { get; set; } = string.Empty;

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;
    }

    /// <summary>
    /// ESPN experience information
    /// </summary>
    public class EspnExperience
    {
        [JsonPropertyName("years")]
        public int? Years { get; set; }

        [JsonPropertyName("displayValue")]
        public string DisplayValue { get; set; } = string.Empty;
    }

    /// <summary>
    /// ESPN headshot information
    /// </summary>
    public class EspnHeadshot
    {
        [JsonPropertyName("href")]
        public string Href { get; set; } = string.Empty;

        [JsonPropertyName("alt")]
        public string Alt { get; set; } = string.Empty;
    }

    /// <summary>
    /// ESPN status information
    /// </summary>
    public class EspnStatus
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;
    }

    /// <summary>
    /// ESPN logo information
    /// </summary>
    public class EspnLogo
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
        public DateTime? LastUpdated { get; set; }
    }

    /// <summary>
    /// ESPN link information
    /// </summary>
    public class EspnLink
    {
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
}