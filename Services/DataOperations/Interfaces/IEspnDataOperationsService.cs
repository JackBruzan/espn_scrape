using ESPNScrape.Models;
using ESPNScrape.Models.Espn;
using ESPNScrape.Models.Supabase;
using ESPNScrape.Models.DataSync;

namespace ESPNScrape.Services.DataOperations.Interfaces
{
    /// <summary>
    /// Combined interface for Data Operations services
    /// </summary>
    public interface IEspnDataOperationsService :
        IEspnBulkOperationsService,
        IEspnDataSyncService,
        IEspnPlayerStatsService,
        IEspnScrapingService,
        ISupabaseDatabaseService
    {
    }

    /// <summary>
    /// Bulk operations service interface
    /// </summary>
    public interface IEspnBulkOperationsService
    {
        Task<int> BulkInsertPlayersAsync(List<Player> players);
        Task<int> BulkUpdatePlayersAsync(List<Player> players);
        Task<int> BulkInsertPlayerStatsAsync(List<PlayerStatistic> stats);
        Task<int> BulkInsertGamesAsync(List<GameEvent> games);
        Task<BulkOperationProgress> ProcessBulkOperationAsync<T>(List<T> items, Func<List<T>, Task<int>> operation) where T : class;
    }

    /// <summary>
    /// Data synchronization service interface
    /// </summary>
    public interface IEspnDataSyncService
    {
        Task<SyncResult> SyncPlayersAsync(int year, int week, int seasonType = 2);
        Task<SyncResult> SyncPlayerStatsAsync(DateTime startDate, DateTime endDate);
        Task<SyncResult> SyncGamesAsync(int year, int week, int seasonType = 2);
        Task<SyncResult> FullSyncAsync(int year, int week, int seasonType = 2);
    }

    /// <summary>
    /// Player statistics service interface
    /// </summary>
    public interface IEspnPlayerStatsService
    {
        Task<List<PlayerStatistic>> GetPlayerStatsAsync(string playerId, int? year = null, int? week = null);
        Task<List<PlayerStatistic>> GetTeamStatsAsync(string teamId, int? year = null, int? week = null);
        Task<PlayerStatistic> SavePlayerStatisticAsync(PlayerStatistic statistic);
        Task<List<PlayerStatistic>> SavePlayerStatisticsAsync(List<PlayerStatistic> statistics);
    }

    /// <summary>
    /// ESPN scraping service interface (combines scraping and image download)
    /// </summary>
    public interface IEspnScrapingService
    {
        Task ScrapeImagesAsync();
        Task<List<Player>> GetActivePlayersAsync();
        Task DownloadPlayerImageAsync(string playerId, string playerName);
    }

    /// <summary>
    /// Image download service interface
    /// </summary>
    public interface IImageDownloadService
    {
        Task<bool> DownloadImageAsync(string url, string filePath);
        Task<byte[]> GetImageBytesAsync(string url);
        Task<string> SaveImageAsync(byte[] imageData, string fileName);
        Task<bool> ImageExistsAsync(string filePath);
    }

    /// <summary>
    /// Supabase database service interface
    /// </summary>
    public interface ISupabaseDatabaseService
    {
        Task<List<T>> GetAllAsync<T>() where T : class, new();
        Task<T?> GetByIdAsync<T>(object id) where T : class;
        Task<T> InsertAsync<T>(T entity) where T : class;
        Task<T> UpdateAsync<T>(T entity) where T : class;
        Task DeleteAsync<T>(object id) where T : class;
        Task<List<T>> BulkInsertAsync<T>(List<T> entities) where T : class;
    }
}