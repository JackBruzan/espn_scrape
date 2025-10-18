using ESPNScrape.Models;
using ESPNScrape.Models.Espn;
using ESPNScrape.Models.Supabase;

namespace ESPNScrape.Services.DataProcessing.Interfaces
{
    /// <summary>
    /// Combined interface for Data Processing services
    /// </summary>
    public interface IEspnDataProcessingService : IEspnDataMappingService, IEspnPlayerMatchingService, IEspnStatsTransformationService
    {
    }

    /// <summary>
    /// ESPN data mapping and transformation service
    /// </summary>
    public interface IEspnDataMappingService
    {
        Player MapEspnPlayerToSupabasePlayer(PlayerStats espnPlayer);
        List<Player> MapEspnPlayersToSupabasePlayers(List<PlayerStats> espnPlayers);
        string GetTeamAbbreviation(string espnTeamId);
        string GetTeamDisplayName(string espnTeamId);
        (string abbreviation, string displayName) GetTeamInfo(string espnTeamId);
    }

    /// <summary>
    /// Player matching service interface
    /// </summary>
    public interface IEspnPlayerMatchingService
    {
        Task<Player?> FindPlayerAsync(string name, string? team = null, string? position = null);
        Task<List<Player>> FindPotentialMatchesAsync(string name, double threshold = 0.8);
        Task<Player> CreateOrUpdatePlayerAsync(Player player);
        Task<Dictionary<string, Player>> BulkMatchPlayersAsync(List<string> playerNames);
    }

    /// <summary>
    /// Stats transformation service interface
    /// </summary>
    public interface IEspnStatsTransformationService
    {
        Task<List<PlayerStatistic>> TransformPlayerStatsAsync(List<PlayerStats> espnStats);
        Task<PlayerStatistic> MapStatisticAsync(string category, string name, object value, string playerId);
        Task<List<TeamStatistic>> TransformTeamStatsAsync(Dictionary<string, object> espnStats, string teamId);
        bool IsValidStatistic(string category, object value);
    }
}