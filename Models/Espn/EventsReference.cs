using System.Text.Json.Serialization;

namespace ESPNScrape.Models.Espn
{
    public class EventsReference
    {
        [JsonPropertyName("$ref")]
        public string ApiReference { get; set; } = string.Empty;
    }
}