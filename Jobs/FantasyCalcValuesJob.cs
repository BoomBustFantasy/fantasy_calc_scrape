using FantasyCalcScrape.Models;
using FantasyCalcScrape.Services.Interfaces;
using Quartz;

namespace FantasyCalcScrape.Jobs;

[DisallowConcurrentExecution]
public class FantasyCalcValuesJob : IJob
{
    private readonly ILogger<FantasyCalcValuesJob> _logger;
    private readonly IFantasyCalcApiService _valuesService;
    private readonly ISupabaseDatabaseService _databaseService;

    public FantasyCalcValuesJob(
        ILogger<FantasyCalcValuesJob> logger,
        IFantasyCalcApiService valuesService,
        ISupabaseDatabaseService databaseService)
    {
        _logger = logger;
        _valuesService = valuesService;
        _databaseService = databaseService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var jobKey = context.JobDetail.Key;
        _logger.LogInformation("Starting Fantasy Calc Values Job: {JobKey}", jobKey);

        try
        {
            // Fetch redraft values from Fantasy Calc API
            _logger.LogInformation("Fetching redraft values from Fantasy Calculator API...");
            var redraftResponse = await _valuesService.GetRedraftValuesAsync();

            if (!redraftResponse.Success)
            {
                _logger.LogError("Failed to fetch redraft values: {Error}", redraftResponse.Error);
                return;
            }

            _logger.LogInformation("Successfully fetched {Count} redraft values from Fantasy Calculator",
                redraftResponse.Values.Count);

            // Update players in Supabase with redraft values matched by Sleeper ID
            var updatedCount = await UpdatePlayerRedraftValues(redraftResponse.Values);

            _logger.LogInformation("Fantasy Calc Values Job completed successfully: {JobKey}. " +
                "Fetched {FetchedCount} values, updated {UpdatedCount} players in database",
                jobKey, redraftResponse.Values.Count, updatedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fantasy Calc Values Job failed: {JobKey}", jobKey);
            throw;
        }
    }

    private async Task<int> UpdatePlayerRedraftValues(List<FantasyCalcPlayer> fantasyCalcPlayers)
    {
        try
        {
            // Build dictionary of Sleeper ID to redraft value and fantasy calc player ID
            var playerValues = new Dictionary<string, (int redraftValue, int fantasyCalcPlayerId)>();

            foreach (var fantasyPlayer in fantasyCalcPlayers)
            {
                // Skip if no Sleeper ID to match on
                if (string.IsNullOrEmpty(fantasyPlayer.Player.SleeperId))
                {
                    continue;
                }

                playerValues[fantasyPlayer.Player.SleeperId] = (fantasyPlayer.RedraftValue, fantasyPlayer.Player.Id);
            }

            if (playerValues.Count == 0)
            {
                _logger.LogWarning("No players with Sleeper IDs found to update");
                return 0;
            }

            // Use the proper batch update method
            var updatedCount = await _databaseService.UpdatePlayerRedraftValuesAsync(playerValues);

            _logger.LogInformation("Updated {Count} players with Fantasy Calculator redraft values", updatedCount);
            return updatedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating player redraft values");
            throw;
        }
    }
}