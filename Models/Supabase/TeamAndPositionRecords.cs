using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ESPNScrape.Models.Supabase;

[Table("Teams")]
public class TeamRecord : BaseModel
{
    [PrimaryKey("id")]
    public long id { get; set; }

    [Column("abbreviation")]
    public string? abbreviation { get; set; }

    [Column("name")]
    public string? name { get; set; }
}

[Table("Positions")]
public class PositionRecord : BaseModel
{
    [PrimaryKey("id")]
    public long id { get; set; }

    [Column("name")]
    public string? name { get; set; }
}