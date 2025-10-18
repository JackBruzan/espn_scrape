using ESPNScrape.Models;
using ESPNScrape.Models.Espn;

namespace ESPNScrape.Services.DataRetrieval.Interfaces
{
    /// <summary>
    /// Combined interface for Data Retrieval services
    /// </summary>
    public interface IEspnDataRetrievalService : IEspnScoreboardService, IEspnBoxScoreService, IEspnScheduleService
    {
    }

    /// <summary>
    /// ESPN Scoreboard service interface  
    /// </summary>
    public interface IEspnScoreboardService
    {
        Task<ScoreboardData> GetScoreboardAsync(int year, int week, int seasonType = 2, CancellationToken cancellationToken = default);
        Task<IEnumerable<GameEvent>> ExtractEventsAsync(ScoreboardData scoreboard, CancellationToken cancellationToken = default);
        Task<Season> ExtractSeasonInfoAsync(ScoreboardData scoreboard, CancellationToken cancellationToken = default);
        Task<Week> ExtractWeekInfoAsync(ScoreboardData scoreboard, CancellationToken cancellationToken = default);
        Task<IEnumerable<string>> GetEventReferencesAsync(ScoreboardData scoreboard, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// ESPN Box Score service interface
    /// </summary>
    public interface IEspnBoxScoreService
    {
        Task<BoxScore?> GetBoxScoreDataAsync(string gameId, CancellationToken cancellationToken = default);
        Task<BoxScore?> GetLiveBoxScoreDataAsync(string gameId, CancellationToken cancellationToken = default);
        Task<(TeamBoxScore homeTeam, TeamBoxScore awayTeam)?> ParseTeamStatsAsync(string boxScoreJson, CancellationToken cancellationToken = default);
        Task<GameInfo?> ExtractGameMetadataAsync(string boxScoreJson, CancellationToken cancellationToken = default);
        string GetBoxScoreUrl(string gameId);
        Task<List<PlayerStats>> ParsePlayerStatsAsync(string boxScoreJson, CancellationToken cancellationToken = default);
        Task<Dictionary<string, object>?> GetTeamOffensiveStatsAsync(string gameId, string teamId, CancellationToken cancellationToken = default);
        Task<Dictionary<string, object>?> GetTeamDefensiveStatsAsync(string gameId, string teamId, CancellationToken cancellationToken = default);
        Task<bool> IsGameLiveAsync(string gameId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// ESPN Schedule service interface
    /// </summary>
    public interface IEspnScheduleService
    {
        Task<Schedule> GetScheduleAsync(int year, int week, int seasonType = 2);
        Task<List<GameEvent>> ExtractGamesFromScheduleAsync(Schedule schedule);
    }
}