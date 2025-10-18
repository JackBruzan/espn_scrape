using ESPNScrape.Models.Espn;

namespace ESPNScrape.Services.Core.Interfaces
{
    /// <summary>
    /// Combined interface for Core ESPN API services
    /// </summary>
    public interface IEspnCoreService : IEspnApiService, IEspnCoreApiService
    {
    }

    /// <summary>
    /// High-level ESPN API orchestration service
    /// </summary>
    public interface IEspnApiService
    {
        Task<ScoreboardData> GetScoreboardAsync(int year, int week, int seasonType = 2);
        Task<BoxScore> GetBoxScoreAsync(string gameId);
        Task<IEnumerable<GameEvent>> GetEventsAsync(int year, int week, int seasonType = 2);
        Task<Season> GetSeasonInfoAsync(int year, int seasonType = 2);
        Task<Week> GetWeekInfoAsync(int year, int week, int seasonType = 2);
    }

    /// <summary>
    /// Low-level ESPN Core API access service
    /// </summary>
    public interface IEspnCoreApiService
    {
        Task<string> GetScheduleAsync(int year, int week, int seasonType = 2);
        Task<string> GetOddsAsync(string eventId, string competitionId);
        Task<T> GetApiDataAsync<T>(string endpoint) where T : class;
        Task<string> GetRawApiDataAsync(string endpoint);
    }
}