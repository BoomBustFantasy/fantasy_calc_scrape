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
    private static readonly int[] SupportedTeamSizes = [8, 10, 12, 14];
    private static readonly int[] SupportedQbCounts = [1, 2];
    private static readonly decimal[] SupportedPprs = [0.0m, 0.5m, 1.0m];
    private static readonly string[] SupportedTePremiums = ["none", "te+", "te++"];

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

            var redraftUpdatedCount = await UpdatePlayerRedraftValues(redraftResponse.Values);
            var normalizedRedraftUpdatedCount = await UpsertNormalizedRedraftValues();
            var normalizedDynastyUpdatedCount = await UpsertNormalizedDynastyValues();

            _logger.LogInformation("Fantasy Calc Values Job completed successfully: {JobKey}. " +
                "Redraft: fetched {RedraftFetched}, updated {RedraftUpdated} players. " +
                "Normalized redraft rows upserted: {NormalizedRedraftUpdated}. " +
                "Normalized dynasty rows upserted: {NormalizedDynastyUpdated}.",
                jobKey, redraftResponse.Values.Count, redraftUpdatedCount,
                normalizedRedraftUpdatedCount, normalizedDynastyUpdatedCount);
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
            var playerValues = new Dictionary<string, (int redraftValue, int fantasyCalcPlayerId)>();

            foreach (var fantasyPlayer in fantasyCalcPlayers)
            {
                if (string.IsNullOrEmpty(fantasyPlayer.Player.SleeperId))
                    continue;

                playerValues[fantasyPlayer.Player.SleeperId] = (fantasyPlayer.RedraftValue, fantasyPlayer.Player.Id);
            }

            if (playerValues.Count == 0)
            {
                _logger.LogWarning("No players with Sleeper IDs found to update");
                return 0;
            }

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

    private async Task<int> UpsertNormalizedDynastyValues()
    {
        try
        {
            var normalizedSettings = BuildNormalizedSettings(isDynasty: true).ToList();
            var totalUpserted = 0;

            foreach (var settings in normalizedSettings)
            {
                _logger.LogInformation(
                    "Fetching normalized dynasty values for {NumTeams} teams, {NumQbs} QB, PPR {Ppr}, TE premium {TePremium}",
                    settings.NumTeams, settings.NumQbs, settings.Ppr, settings.Te_premium);

                var response = await _valuesService.GetCurrentValuesAsync(settings);

                if (!response.Success)
                {
                    _logger.LogError(
                        "Failed to fetch normalized dynasty values for {NumTeams} teams, {NumQbs} QB, PPR {Ppr}, TE premium {TePremium}: {Error}",
                        settings.NumTeams, settings.NumQbs, settings.Ppr, settings.Te_premium, response.Error);
                    return 0;
                }

                var upsertedCount = await _databaseService.UpsertFantasyCalcDynastyValuesAsync(response.Values, settings);
                totalUpserted += upsertedCount;

                _logger.LogInformation(
                    "Upserted {Count} normalized dynasty rows for {NumTeams} teams, {NumQbs} QB, PPR {Ppr}, TE premium {TePremium}",
                    upsertedCount, settings.NumTeams, settings.NumQbs, settings.Ppr, settings.Te_premium);
            }

            return totalUpserted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting normalized dynasty values");
            throw;
        }
    }

    private async Task<int> UpsertNormalizedRedraftValues()
    {
        try
        {
            var normalizedSettings = BuildNormalizedSettings(isDynasty: false).ToList();
            var totalUpserted = 0;

            foreach (var settings in normalizedSettings)
            {
                _logger.LogInformation(
                    "Fetching normalized redraft values for {NumTeams} teams, {NumQbs} QB, PPR {Ppr}, TE premium {TePremium}",
                    settings.NumTeams, settings.NumQbs, settings.Ppr, settings.Te_premium);

                var response = await _valuesService.GetCurrentValuesAsync(settings);

                if (!response.Success)
                {
                    _logger.LogError(
                        "Failed to fetch normalized redraft values for {NumTeams} teams, {NumQbs} QB, PPR {Ppr}, TE premium {TePremium}: {Error}",
                        settings.NumTeams, settings.NumQbs, settings.Ppr, settings.Te_premium, response.Error);
                    return 0;
                }

                var upsertedCount = await _databaseService.UpsertFantasyCalcRedraftValuesAsync(response.Values, settings);
                totalUpserted += upsertedCount;

                _logger.LogInformation(
                    "Upserted {Count} normalized redraft rows for {NumTeams} teams, {NumQbs} QB, PPR {Ppr}, TE premium {TePremium}",
                    upsertedCount, settings.NumTeams, settings.NumQbs, settings.Ppr, settings.Te_premium);
            }

            return totalUpserted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting normalized redraft values");
            throw;
        }
    }

    private static IEnumerable<FantasyCalcApiSettings> BuildNormalizedSettings(bool isDynasty)
    {
        foreach (var numTeams in SupportedTeamSizes)
        {
            foreach (var numQbs in SupportedQbCounts)
            {
                foreach (var ppr in SupportedPprs)
                {
                    foreach (var tePremium in SupportedTePremiums)
                    {
                        yield return new FantasyCalcApiSettings
                        {
                            IsDynasty = isDynasty,
                            NumTeams = numTeams,
                            NumQbs = numQbs,
                            Ppr = ppr,
                            Te_premium = tePremium
                        };
                    }
                }
            }
        }
    }
}