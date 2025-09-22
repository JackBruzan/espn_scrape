using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ESPNScrape.Models.Supabase;

[Table("Players")]
public class PlayerRecord : BaseModel
{
    [PrimaryKey("id")]
    public long id { get; set; }

    [Column("first_name")]
    public string? first_name { get; set; }

    [Column("last_name")]
    public string? last_name { get; set; }

    [Column("espn_player_id")]
    public string? espn_player_id { get; set; }

    [Column("team_id")]
    public long? team_id { get; set; }

    [Column("position_id")]
    public long? position_id { get; set; }

    [Column("active")]
    public bool active { get; set; }

    [Column("created_at")]
    public DateTime created_at { get; set; }

    [Column("updated_at")]
    public DateTime updated_at { get; set; }
}