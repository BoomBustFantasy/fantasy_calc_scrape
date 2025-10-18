namespace FantasyCalcScrape.Configuration;

public class ResilienceConfiguration
{
    public int MaxRetryAttempts { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 30;
    public int DelayBetweenRetriesSeconds { get; set; } = 2;
}