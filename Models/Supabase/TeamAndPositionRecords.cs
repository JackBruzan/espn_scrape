namespace ESPNScrape.Models.Supabase;

public class TeamRecord
{
    public long id { get; set; }
    public string? abbreviation { get; set; }
    public string? name { get; set; }
}

public class PositionRecord
{
    public long id { get; set; }
    public string? name { get; set; }
}