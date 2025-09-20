namespace ESPNScrape.Models.Supabase;

public class PlayerRecord
{
    public long id { get; set; }
    public string? first_name { get; set; }
    public string? last_name { get; set; }
    public string? espn_player_id { get; set; }
    public long? team_id { get; set; }
    public long? position_id { get; set; }
    public bool active { get; set; }
    public DateTime created_at { get; set; }
    public DateTime updated_at { get; set; }
}