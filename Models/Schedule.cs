using System.Text.Json.Serialization;

namespace ESPNScrape.Models;

public class Schedule
{
    public string EspnGameId { get; set; } = string.Empty;
    public long? HomeTeamId { get; set; }
    public long? AwayTeamId { get; set; }
    public string HomeTeamName { get; set; } = string.Empty; // Temporary for parsing, not stored
    public string AwayTeamName { get; set; } = string.Empty; // Temporary for parsing, not stored
    public DateTime GameTime { get; set; }
    public int Week { get; set; }
    public int Year { get; set; }
    public int SeasonType { get; set; } = 2; // Regular season default

    // Basic betting data (existing database columns)
    public decimal? BettingLine { get; set; }
    public decimal? OverUnder { get; set; }
    public decimal? HomeImpliedPoints { get; set; }
    public decimal? AwayImpliedPoints { get; set; }

    // Enhanced betting data from ESPN API (in-memory only, not persisted until DB is updated)
    [JsonIgnore] // Don't try to persist these until DB schema is updated
    public string EspnCompetitionId { get; set; } = string.Empty; // For API calls
    [JsonIgnore]
    public int? HomeMoneyline { get; set; } // e.g., -140, +120
    [JsonIgnore]
    public int? AwayMoneyline { get; set; }
    [JsonIgnore]
    public string? SpreadOdds { get; set; } // e.g., "-110", "EVEN"
    [JsonIgnore]
    public string? OverOdds { get; set; }
    [JsonIgnore]
    public string? UnderOdds { get; set; }
    [JsonIgnore]
    public string BettingProvider { get; set; } = "ESPN BET";
    [JsonIgnore]
    public decimal? OddsOpenSpread { get; set; } // Opening point spread
    [JsonIgnore]
    public decimal? OddsOpenTotal { get; set; } // Opening over/under total
    [JsonIgnore]
    public DateTime? OddsLastUpdated { get; set; }

    /// <summary>
    /// Calculates implied points for both teams based on betting line and over/under
    /// </summary>
    public void CalculateImpliedPoints()
    {
        if (BettingLine.HasValue && OverUnder.HasValue)
        {
            // For positive line (home team favored): home gets more points
            // For negative line (away team favored): away gets more points
            HomeImpliedPoints = (OverUnder.Value - BettingLine.Value) / 2;
            AwayImpliedPoints = (OverUnder.Value + BettingLine.Value) / 2;
        }
    }
}