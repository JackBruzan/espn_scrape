using ESPNScrape.Models.Espn;

namespace ESPNScrape.Services.Interfaces
{
    public interface IEspnScoreboardService
    {
        Task<ScoreboardData> GetScoreboardAsync(int year, int week, int seasonType = 2, CancellationToken cancellationToken = default);
        Task<IEnumerable<GameEvent>> ExtractEventsAsync(ScoreboardData scoreboard, CancellationToken cancellationToken = default);
        Task<Season> ExtractSeasonInfoAsync(ScoreboardData scoreboard, CancellationToken cancellationToken = default);
        Task<Week> ExtractWeekInfoAsync(ScoreboardData scoreboard, CancellationToken cancellationToken = default);
        Task<IEnumerable<string>> GetEventReferencesAsync(ScoreboardData scoreboard, CancellationToken cancellationToken = default);
    }
}