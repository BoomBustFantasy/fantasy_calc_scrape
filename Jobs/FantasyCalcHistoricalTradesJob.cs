using FantasyCalcScrape.Services.Interfaces;
using Quartz;

namespace FantasyCalcScrape.Jobs;

[DisallowConcurrentExecution]
public class FantasyCalcHistoricalTradesJob : IJob
{
    private readonly ILogger<FantasyCalcHistoricalTradesJob> _logger;
    private readonly IFantasyCalcApiService _apiService;
    private readonly ISupabaseDatabaseService _databaseService;

    public FantasyCalcHistoricalTradesJob(
        ILogger<FantasyCalcHistoricalTradesJob> logger,
        IFantasyCalcApiService apiService,
        ISupabaseDatabaseService databaseService)
    {
        _logger = logger;
        _apiService = apiService;
        _databaseService = databaseService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var jobKey = context.JobDetail.Key;
        _logger.LogInformation("Starting Fantasy Calc Historical Trades Job: {JobKey}", jobKey);

        try
        {
            // Define different league configurations to fetch more diverse trades
            var configurations = new[]
            {
                // Superflex Dynasty (most common)
                new { isDynasty = true, numTeams = 12, ppr = 1.0m, numQbs = 2, minPlayers = 2, maxPlayers = 8 },
                new { isDynasty = true, numTeams = 10, ppr = 1.0m, numQbs = 2, minPlayers = 2, maxPlayers = 8 },
                new { isDynasty = true, numTeams = 12, ppr = 0.5m, numQbs = 2, minPlayers = 2, maxPlayers = 8 },
                
                // 1QB Dynasty
                new { isDynasty = true, numTeams = 12, ppr = 1.0m, numQbs = 1, minPlayers = 2, maxPlayers = 8 },
                new { isDynasty = true, numTeams = 10, ppr = 1.0m, numQbs = 1, minPlayers = 2, maxPlayers = 8 },
                
                // Different team sizes
                new { isDynasty = true, numTeams = 14, ppr = 1.0m, numQbs = 2, minPlayers = 2, maxPlayers = 8 },
            };

            int totalFetched = 0;
            int totalInserted = 0;

            foreach (var config in configurations)
            {
                _logger.LogInformation("Fetching trades for: Dynasty={Dynasty}, Teams={Teams}, PPR={Ppr}, QBs={Qbs}",
                    config.isDynasty, config.numTeams, config.ppr, config.numQbs);

                var tradesResponse = await _apiService.GetTradesAsync(
                    isDynasty: config.isDynasty,
                    numTeams: config.numTeams,
                    ppr: config.ppr,
                    numQbs: config.numQbs,
                    minPlayers: config.minPlayers,
                    maxPlayers: config.maxPlayers
                );

                if (!tradesResponse.Success)
                {
                    _logger.LogWarning("Failed to fetch trades for config: {Error}", tradesResponse.Error);
                    continue;
                }

                totalFetched += tradesResponse.Trades.Count;

                // Store trades in Supabase
                var insertedCount = await _databaseService.InsertHistoricalTradesAsync(tradesResponse.Trades);
                totalInserted += insertedCount;

                _logger.LogInformation("Config completed: Fetched {Fetched}, Inserted {Inserted}",
                    tradesResponse.Trades.Count, insertedCount);

                // Small delay between API calls to be respectful
                await Task.Delay(500);
            }

            _logger.LogInformation("Fantasy Calc Historical Trades Job completed successfully: {JobKey}. " +
                "Total fetched {TotalFetched} trades, inserted {TotalInserted} new trades across {ConfigCount} configurations",
                jobKey, totalFetched, totalInserted, configurations.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fantasy Calc Historical Trades Job failed: {JobKey}", jobKey);
            throw;
        }
    }
}
