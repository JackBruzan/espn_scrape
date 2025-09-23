using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ESPNScrape.Models.Supabase;

[Table("Schedule")]
public class ScheduleRecord : BaseModel
{
    [PrimaryKey("id")]
    public long id { get; set; }

    [Column("espn_game_id")]
    public string? espn_game_id { get; set; }

    [Column("home_team_id")]
    public long? home_team_id { get; set; }

    [Column("away_team_id")]
    public long? away_team_id { get; set; }

    [Column("game_time")]
    public DateTime game_time { get; set; }

    [Column("week")]
    public int week { get; set; }

    [Column("year")]
    public int year { get; set; }

    [Column("season_type")]
    public int season_type { get; set; } = 2; // Default to regular season

    [Column("betting_line")]
    public decimal? betting_line { get; set; }

    [Column("over_under")]
    public decimal? over_under { get; set; }

    [Column("home_implied_points")]
    public decimal? home_implied_points { get; set; }

    [Column("away_implied_points")]
    public decimal? away_implied_points { get; set; }

    [Column("created_at")]
    public DateTime created_at { get; set; }

    [Column("updated_at")]
    public DateTime updated_at { get; set; }
}