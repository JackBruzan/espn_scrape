using System.Text.Json.Serialization;

namespace ESPNScrape.Models.Espn
{
    public class Venue : IEquatable<Venue>
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("fullName")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("address")]
        public VenueAddress? Address { get; set; }

        [JsonPropertyName("capacity")]
        public int Capacity { get; set; }

        [JsonPropertyName("grass")]
        public bool Grass { get; set; }

        [JsonPropertyName("dome")]
        public bool Dome { get; set; }

        [JsonPropertyName("images")]
        public List<VenueImage> Images { get; set; } = new();

        public bool Equals(Venue? other) => other != null && Id == other.Id;
        public override bool Equals(object? obj) => Equals(obj as Venue);
        public override int GetHashCode() => Id.GetHashCode();
    }

    public class VenueAddress
    {
        [JsonPropertyName("city")]
        public string City { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("zipCode")]
        public string ZipCode { get; set; } = string.Empty;
    }

    public class VenueImage
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
    }
}