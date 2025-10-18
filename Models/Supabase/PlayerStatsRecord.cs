using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Text.Json.Serialization;

namespace ESPNScrape.Models.Supabase
{
    /// <summary>
    /// Database record for storing player statistics from ESPN (matches existing PlayerStats table)
    /// </summary>
    [Table("PlayerStats")]
    public class PlayerStatsRecord : BaseModel
    {
        /// <summary>
        /// Unique identifier for this stats record
        /// </summary>
        [PrimaryKey("id", false)]
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// Reference to the player these stats belong to
        /// </summary>
        [Column("player_id")]
        [JsonPropertyName("player_id")]
        public long? PlayerId { get; set; }

        /// <summary>
        /// ESPN player ID for reference
        /// </summary>
        [Column("espn_player_id")]
        [JsonPropertyName("espn_player_id")]
        public string? EspnPlayerId { get; set; }

        /// <summary>
        /// ESPN game ID these stats are from
        /// </summary>
        [Column("espn_game_id")]
        [JsonPropertyName("espn_game_id")]
        public string? EspnGameId { get; set; }

        /// <summary>
        /// NFL season year
        /// </summary>
        [Column("season")]
        [JsonPropertyName("season")]
        public int? Season { get; set; }

        /// <summary>
        /// NFL week number
        /// </summary>
        [Column("week")]
        [JsonPropertyName("week")]
        public int? Week { get; set; }

        /// <summary>
        /// Player name
        /// </summary>
        [Column("name")]
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Player code (used in composite primary key)
        /// </summary>
        [Column("player_code")]
        [JsonPropertyName("player_code")]
        public string PlayerCode { get; set; } = string.Empty;

        /// <summary>
        /// Team name/abbreviation
        /// </summary>
        [Column("team")]
        [JsonPropertyName("team")]
        public string Team { get; set; } = string.Empty;

        /// <summary>
        /// Game date (used in composite primary key)
        /// </summary>
        [Column("game_date")]
        [JsonPropertyName("game_date")]
        public DateTime GameDate { get; set; }

        /// <summary>
        /// Game location (Home/Away)
        /// </summary>
        [Column("game_location")]
        [JsonPropertyName("game_location")]
        public string GameLocation { get; set; } = string.Empty;

        /// <summary>
        /// Passing statistics as JSONB
        /// </summary>
        [Column("passing")]
        [JsonPropertyName("passing")]
        public object? Passing { get; set; }

        /// <summary>
        /// Rushing statistics as JSONB
        /// </summary>
        [Column("rushing")]
        [JsonPropertyName("rushing")]
        public object? Rushing { get; set; }

        /// <summary>
        /// Receiving statistics as JSONB
        /// </summary>
        [Column("receiving")]
        [JsonPropertyName("receiving")]
        public object? Receiving { get; set; }

        /// <summary>
        /// Total number of fumbles by the player in this game
        /// </summary>
        [Column("fumbles")]
        [JsonPropertyName("fumbles")]
        public int? Fumbles { get; set; }

        /// <summary>
        /// Number of fumbles lost by the player in this game
        /// </summary>
        [Column("fumbles_lost")]
        [JsonPropertyName("fumbles_lost")]
        public int? FumblesLost { get; set; }

        /// <summary>
        /// When this record was created
        /// </summary>
        [Column("created_at")]
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When this record was last updated
        /// </summary>
        [Column("updated_at")]
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}