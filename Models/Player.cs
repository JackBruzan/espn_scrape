using System.Text.Json.Serialization;

namespace ESPNScrape.Models;

public class PlayerResponse
{
    [JsonPropertyName("items")]
    public List<Player> Items { get; set; } = new();
}

public class Player
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("team")]
    public Team? Team { get; set; }

    [JsonPropertyName("position")]
    public Position? Position { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }
}

public class Team
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}

public class Position
{
    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}
