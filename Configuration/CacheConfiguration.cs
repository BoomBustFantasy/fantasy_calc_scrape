namespace FantasyCalcScrape.Configuration;

public class CacheConfiguration
{
    public int DefaultCacheTimeoutMinutes { get; set; } = 15;
    public int MaxCacheSize { get; set; } = 1000;
    public bool EnableCaching { get; set; } = true;
}