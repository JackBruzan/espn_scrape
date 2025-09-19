namespace ESPNScrape.Configuration
{
    public class CacheConfiguration
    {
        public int DefaultTtlMinutes { get; set; } = 30;
        public int SeasonDataTtlHours { get; set; } = 24;
        public int CompletedGameTtlMinutes { get; set; } = 60;
        public int LiveGameTtlSeconds { get; set; } = 30;
        public int PlayerStatsTtlMinutes { get; set; } = 15;
        public int TeamDataTtlHours { get; set; } = 12;
        public bool EnableCacheWarming { get; set; } = true;
        public int MaxCacheSize { get; set; } = 1000;
    }
}