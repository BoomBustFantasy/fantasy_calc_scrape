using Supabase;
using FantasyCalcScrape.Services.Interfaces;
using FantasyCalcScrape.Models.Supa;

namespace FantasyCalcScrape.Services;

/// <summary>
/// Service to update player redraft values in the Supabase Players table
/// </summary>
public class SupabaseDatabaseService : ISupabaseDatabaseService
{
    private readonly ILogger<SupabaseDatabaseService> _logger;
    private readonly Client _supabaseClient;

    public SupabaseDatabaseService(ILogger<SupabaseDatabaseService> logger, IConfiguration configuration)
    {
        _logger = logger;

        var url = configuration["Supabase:Url"] ?? throw new InvalidOperationException("Supabase URL not configured");
        var serviceRoleKey = configuration["Supabase:ServiceRoleKey"] ?? throw new InvalidOperationException("Supabase Service Role Key not configured");

        _supabaseClient = new Client(url, serviceRoleKey);
    }

    /// <summary>
    /// Updates a player's redraft value by Sleeper ID
    /// </summary>
    /// <param name="sleeperId">The player's Sleeper ID as string</param>
    /// <param name="redraftValue">The new redraft value</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> UpdatePlayerRedraftValueAsync(string sleeperId, int redraftValue)
    {
        try
        {
            // Convert string Sleeper ID to long for database lookup
            if (!long.TryParse(sleeperId, out long sleeperIdLong))
            {
                _logger.LogWarning("Invalid Sleeper ID format: {SleeperId}", sleeperId);
                return false;
            }

            _logger.LogDebug("Looking for player with Sleeper ID {SleeperId} (converted to {SleeperIdLong})", sleeperId, sleeperIdLong);

            // First, try to find the player to see if they exist
            var findResult = await _supabaseClient
                .From<Player>()
                .Where(p => p.SleeperId == sleeperIdLong)
                .Get();

            if (findResult?.Models == null || !findResult.Models.Any())
            {
                _logger.LogWarning("No player found with Sleeper ID {SleeperId}", sleeperId);
                return false;
            }

            var player = findResult.Models.First();
            _logger.LogInformation("Found player: {FirstName} {LastName} (ID: {PlayerId}) with Sleeper ID {SleeperId}",
                player.FirstName, player.LastName, player.Id, sleeperId);

            // Update the player's redraft value
            _logger.LogDebug("Updating player {SleeperId} redraft value to {RedraftValue}", sleeperId, redraftValue);

            var result = await _supabaseClient
                .From<Player>()
                .Where(p => p.SleeperId == sleeperIdLong)
                .Set(p => p.FantasyCalcRedraftValue, redraftValue)
                .Update();

            if (result?.Models is not null && result.Models.Any())
            {
                _logger.LogDebug("Successfully updated redraft value for player {SleeperId}", sleeperId);
                return true;
            }
            else
            {
                _logger.LogWarning("Update failed for player with Sleeper ID {SleeperId}", sleeperId);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating redraft value for player {SleeperId}", sleeperId);
            return false;
        }
    }

    /// <summary>
    /// Updates multiple players' redraft values in a single batch operation
    /// </summary>
    /// <param name="playerValues">Dictionary of Sleeper ID to redraft value</param>
    /// <returns>Number of players successfully updated</returns>
    public async Task<int> UpdatePlayerRedraftValuesAsync(Dictionary<string, int> playerValues)
    {
        if (!playerValues.Any())
        {
            _logger.LogInformation("No player values to update");
            return 0;
        }

        try
        {
            _logger.LogInformation("Performing batch update for {Count} players", playerValues.Count);

            // First, get all players that match the Sleeper IDs in one query
            var sleeperIds = new List<long>();
            foreach (var kvp in playerValues)
            {
                if (long.TryParse(kvp.Key, out long sleeperId))
                {
                    sleeperIds.Add(sleeperId);
                }
                else
                {
                    _logger.LogWarning("Invalid Sleeper ID format: {SleeperId}", kvp.Key);
                }
            }

            if (!sleeperIds.Any())
            {
                _logger.LogWarning("No valid Sleeper IDs to update");
                return 0;
            }

            // Paginate through all players with Sleeper IDs (Supabase has a 1000 row limit by default)
            var allPlayers = new List<Player>();
            int pageSize = 1000;
            int offset = 0;
            bool hasMore = true;

            _logger.LogInformation("Fetching all players with Sleeper IDs using pagination...");

            while (hasMore)
            {
                var pageResult = await _supabaseClient
                    .From<Player>()
                    .Where(p => p.SleeperId != null)
                    .Range(offset, offset + pageSize - 1)
                    .Get();

                if (pageResult?.Models == null || !pageResult.Models.Any())
                {
                    hasMore = false;
                    break;
                }

                allPlayers.AddRange(pageResult.Models);
                _logger.LogDebug("Fetched page with {PageCount} players, total so far: {TotalCount}",
                    pageResult.Models.Count, allPlayers.Count);

                if (pageResult.Models.Count < pageSize)
                {
                    hasMore = false; // Last page
                }
                else
                {
                    offset += pageSize;
                }
            }

            _logger.LogInformation("Successfully fetched {TotalPlayers} players from database with non-null Sleeper IDs", allPlayers.Count);

            // Filter to only players we have values for
            var playersResult = allPlayers.Where(p => p.SleeperId.HasValue && sleeperIds.Contains(p.SleeperId.Value));
            var foundSleeperIds = playersResult.Select(p => p.SleeperId!.Value).ToHashSet();
            var missingSleeperIds = sleeperIds.Except(foundSleeperIds).ToList();

            _logger.LogInformation("Fantasy Calculator provided {TotalIds} Sleeper IDs, found {FoundCount} in database, missing {MissingCount}",
                sleeperIds.Count, foundSleeperIds.Count, missingSleeperIds.Count);

            if (missingSleeperIds.Any())
            {
                _logger.LogWarning("Missing Sleeper IDs from database: {MissingIds}", string.Join(", ", missingSleeperIds.Take(20)));
                if (missingSleeperIds.Count > 20)
                {
                    _logger.LogWarning("... and {RemainingCount} more missing IDs", missingSleeperIds.Count - 20);
                }
            }

            if (!playersResult.Any())
            {
                _logger.LogWarning("No players found matching the provided Sleeper IDs");
                return 0;
            }

            // Check for duplicate Sleeper IDs
            var sleeperIdCounts = playersResult.GroupBy(p => p.SleeperId).ToDictionary(g => g.Key, g => g.Count());
            var duplicates = sleeperIdCounts.Where(kvp => kvp.Value > 1).ToList();

            if (duplicates.Any())
            {
                _logger.LogWarning("Found {DuplicateCount} duplicate Sleeper IDs in database: {Duplicates}",
                    duplicates.Count, string.Join(", ", duplicates.Select(d => $"{d.Key}({d.Value}x)")));
            }

            // Since Upsert has issues with calculated properties, let's do individual updates in a loop
            // but with minimal database calls by batching the query preparation
            int successCount = 0;
            var updateTasks = new List<Task<bool>>();

            foreach (var player in playersResult)
            {
                if (player.SleeperId.HasValue && playerValues.TryGetValue(player.SleeperId.Value.ToString(), out int redraftValue))
                {
                    _logger.LogDebug("Preparing update for {FirstName} {LastName} (Sleeper ID: {SleeperId}) = {Value}",
                        player.FirstName, player.LastName, player.SleeperId, redraftValue);

                    // Add the update task to batch
                    var updateTask = UpdateSinglePlayerRedraftValue(player.SleeperId.Value, redraftValue);
                    updateTasks.Add(updateTask);
                }
            }

            if (!updateTasks.Any())
            {
                _logger.LogWarning("No players prepared for update");
                return 0;
            }

            // Execute all updates concurrently
            var results = await Task.WhenAll(updateTasks);
            var updatedCount = results.Count(r => r);
            _logger.LogInformation("Batch update completed: {UpdatedCount} players updated", updatedCount);
            return updatedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing batch update for player redraft values");
            return 0;
        }
    }

    private async Task<bool> UpdateSinglePlayerRedraftValue(long sleeperId, int redraftValue)
    {
        try
        {
            var result = await _supabaseClient
                .From<Player>()
                .Where(p => p.SleeperId == sleeperId)
                .Set(p => p.FantasyCalcRedraftValue, redraftValue)
                .Update();

            return result?.Models is not null && result.Models.Any();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating redraft value for player with Sleeper ID {SleeperId}", sleeperId);
            return false;
        }
    }
}

