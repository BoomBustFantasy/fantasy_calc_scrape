using FantasyCalcScrape.Models.Supa;
using FantasyCalcScrape.Services.Interfaces;
using Supabase;

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

        // Initialize Supabase client with proper options
        var options = new SupabaseOptions
        {
            AutoConnectRealtime = false,
            AutoRefreshToken = false
        };

        _supabaseClient = new Client(url, serviceRoleKey, options);

        // Initialize the client (required before use)
        _supabaseClient.InitializeAsync().Wait();

        _logger.LogInformation("Supabase client initialized successfully");
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


            var player = findResult.Models.FirstOrDefault();
            if (player == null)
            {
                _logger.LogWarning("No player found with Sleeper ID {SleeperId}", sleeperId);
                return false;
            }

            _logger.LogInformation("Found player: {FirstName} {LastName} (ID: {PlayerId}) with Sleeper ID {SleeperId}",
                player.FirstName ?? "Unknown", player.LastName ?? "Unknown", player.Id, sleeperId);


            // Update the player's redraft value
            _logger.LogDebug("Updating player {SleeperId} redraft value to {RedraftValue}", sleeperId, redraftValue);

            var updateResult = await _supabaseClient
                .From<Player>()
                .Where(p => p.SleeperId == sleeperIdLong)
                .Set(p => p.FantasyCalcRedraftValue, redraftValue)
                .Update();

            if (updateResult?.Models?.Any() == true)
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
    /// Updates multiple players' redraft values and fantasy calc player IDs in a single batch operation
    /// </summary>
    /// <param name="playerValues">Dictionary of Sleeper ID to tuple of (redraft value, fantasy calc player ID)</param>
    /// <returns>Number of players successfully updated</returns>
    public async Task<int> UpdatePlayerRedraftValuesAsync(Dictionary<string, (int redraftValue, int fantasyCalcPlayerId)> playerValues)
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
            var sleeperIdCounts = playersResult.GroupBy(p => p.SleeperId).ToDictionary(g => g.Key ?? 0, g => g.Count());
            var duplicates = sleeperIdCounts.Where(kvp => kvp.Value > 1).ToList();

            if (duplicates.Any())
            {
                _logger.LogWarning("Found {DuplicateCount} duplicate Sleeper IDs in database: {Duplicates}",
                    duplicates.Count, string.Join(", ", duplicates.Select(d => $"{d.Key}({d.Value}x)")));
            }

            // Since Upsert has issues with calculated properties, let's do individual updates in a loop
            // but with minimal database calls by batching the query preparation
            var updateTasks = new List<Task<bool>>();

            foreach (var player in playersResult)
            {
                if (player.SleeperId.HasValue && playerValues.TryGetValue(player.SleeperId.Value.ToString(), out var playerData))
                {
                    _logger.LogDebug("Preparing update for {FirstName} {LastName} (Sleeper ID: {SleeperId}) - Redraft Value: {RedraftValue}, Fantasy Calc ID: {FantasyCalcId}",
                        player.FirstName, player.LastName, player.SleeperId, playerData.redraftValue, playerData.fantasyCalcPlayerId);

                    // Add the update task to batch
                    var updateTask = UpdateSinglePlayerRedraftValue(player.SleeperId.Value, playerData.redraftValue, playerData.fantasyCalcPlayerId);
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

    private async Task<bool> UpdateSinglePlayerRedraftValue(long sleeperId, int redraftValue, int fantasyCalcPlayerId)
    {
        try
        {
            var updateResult = await _supabaseClient
                .From<Player>()
                .Where(p => p.SleeperId == sleeperId)
                .Set(p => p.FantasyCalcRedraftValue, redraftValue)
                .Set(p => p.FantasyCalcPlayerId, fantasyCalcPlayerId)
                .Update();

            return updateResult?.Models?.Any() == true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating redraft value and fantasy calc player ID for player with Sleeper ID {SleeperId}", sleeperId);
            return false;
        }
    }

    /// <summary>
    /// Updates multiple players' dynasty values and fantasy calc player IDs in a single batch operation
    /// </summary>
    /// <param name="playerValues">Dictionary of Sleeper ID to tuple of (dynasty value, fantasy calc player ID)</param>
    /// <returns>Number of players successfully updated</returns>
    public async Task<int> UpdatePlayerDynastyValuesAsync(Dictionary<string, (int dynastyValue, int fantasyCalcPlayerId)> playerValues)
    {
        if (!playerValues.Any())
        {
            _logger.LogInformation("No player dynasty values to update");
            return 0;
        }

        try
        {
            _logger.LogInformation("Performing batch update for {Count} players with dynasty values", playerValues.Count);

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
            var sleeperIdCounts = playersResult.GroupBy(p => p.SleeperId).ToDictionary(g => g.Key ?? 0, g => g.Count());
            var duplicates = sleeperIdCounts.Where(kvp => kvp.Value > 1).ToList();

            if (duplicates.Any())
            {
                _logger.LogWarning("Found {DuplicateCount} duplicate Sleeper IDs in database: {Duplicates}",
                    duplicates.Count, string.Join(", ", duplicates.Select(d => $"{d.Key}({d.Value}x)")));
            }

            // Do individual updates concurrently
            var updateTasks = new List<Task<bool>>();

            foreach (var player in playersResult)
            {
                if (player.SleeperId.HasValue && playerValues.TryGetValue(player.SleeperId.Value.ToString(), out var playerData))
                {
                    _logger.LogDebug("Preparing update for {FirstName} {LastName} (Sleeper ID: {SleeperId}) - Dynasty Value: {DynastyValue}, Fantasy Calc ID: {FantasyCalcId}",
                        player.FirstName, player.LastName, player.SleeperId, playerData.dynastyValue, playerData.fantasyCalcPlayerId);

                    // Add the update task to batch
                    var updateTask = UpdateSinglePlayerDynastyValue(player.SleeperId.Value, playerData.dynastyValue, playerData.fantasyCalcPlayerId);
                    updateTasks.Add(updateTask);
                }
            }

            if (!updateTasks.Any())
            {
                _logger.LogWarning("No players prepared for update with dynasty values");
                return 0;
            }

            // Execute all updates concurrently
            var results = await Task.WhenAll(updateTasks);
            var updatedCount = results.Count(r => r);
            _logger.LogInformation("Batch dynasty update completed: {UpdatedCount} players updated", updatedCount);
            return updatedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing batch update for player dynasty values");
            return 0;
        }
    }

    public async Task<int> UpsertFantasyCalcDynastyValuesAsync(List<FantasyCalcScrape.Models.FantasyCalcPlayer> fantasyCalcPlayers, FantasyCalcScrape.Models.FantasyCalcApiSettings settings)
    {
        if (!fantasyCalcPlayers.Any())
        {
            _logger.LogInformation("No Fantasy Calc dynasty values to upsert");
            return 0;
        }

        try
        {
            var playerLookup = await GetPlayerLookupDictionary();
            var normalizedRows = new List<FantasyCalcScrape.Models.Supa.FantasyCalcPlayerValue>();
            var tePremium = NormalizeTePremium(settings.Te_premium);
            var updatedAt = DateTime.UtcNow;

            foreach (var fantasyPlayer in fantasyCalcPlayers)
            {
                if (string.IsNullOrWhiteSpace(fantasyPlayer.Player.SleeperId))
                {
                    continue;
                }

                if (!playerLookup.TryGetValue(fantasyPlayer.Player.SleeperId, out var playerId))
                {
                    _logger.LogDebug(
                        "Skipping Fantasy Calc player {FantasyCalcPlayerId} because Sleeper ID {SleeperId} was not found in Players",
                        fantasyPlayer.Player.Id, fantasyPlayer.Player.SleeperId);
                    continue;
                }

                normalizedRows.Add(new FantasyCalcScrape.Models.Supa.FantasyCalcPlayerValue
                {
                    PlayerId = playerId,
                    Mode = "DYN",
                    NumTeams = settings.NumTeams,
                    NumQbs = settings.NumQbs,
                    Ppr = settings.Ppr,
                    TePremium = tePremium,
                    FantasyCalcPlayerId = fantasyPlayer.Player.Id,
                    Value = fantasyPlayer.Value,
                    OverallRank = fantasyPlayer.OverallRank,
                    PositionRank = fantasyPlayer.PositionRank,
                    UpdatedAt = updatedAt
                });
            }

            if (!normalizedRows.Any())
            {
                _logger.LogWarning("No normalized Fantasy Calc dynasty rows were matched to internal players");
                return 0;
            }

            const string conflictColumns = "player_id,mode,num_teams,num_qbs,ppr,te_premium";
            var upsertedCount = 0;

            foreach (var batch in normalizedRows.Chunk(200))
            {
                var upsertResult = await _supabaseClient
                    .From<FantasyCalcScrape.Models.Supa.FantasyCalcPlayerValue>()
                    .Upsert(batch.ToList(), new Supabase.Postgrest.QueryOptions { OnConflict = conflictColumns });

                if (upsertResult?.Models?.Any() == true)
                {
                    upsertedCount += upsertResult.Models.Count;
                }
            }

            _logger.LogInformation(
                "Upserted {Count} normalized Fantasy Calc dynasty rows for {NumTeams} teams, {NumQbs} QB, PPR {Ppr}, TE premium {TePremium}",
                upsertedCount, settings.NumTeams, settings.NumQbs, settings.Ppr, tePremium);

            return upsertedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting normalized Fantasy Calc dynasty values");
            return 0;
        }
    }

    public async Task<FantasyCalcScrape.Models.Supa.FantasyCalcPlayerValue?> GetFantasyCalcDynastyValueAsync(long playerId, FantasyCalcScrape.Models.FantasyCalcApiSettings settings)
    {
        if (!settings.IsDynasty)
        {
            _logger.LogWarning("Requested Fantasy Calc value lookup for non-dynasty settings; only dynasty is supported");
            return null;
        }

        try
        {
            var tePremium = NormalizeTePremium(settings.Te_premium);

            var result = await _supabaseClient
                .From<FantasyCalcScrape.Models.Supa.FantasyCalcPlayerValue>()
                .Where(value =>
                    value.PlayerId == playerId &&
                    value.Mode == "DYN" &&
                    value.NumTeams == settings.NumTeams &&
                    value.NumQbs == settings.NumQbs &&
                    value.Ppr == settings.Ppr &&
                    value.TePremium == tePremium)
                .Get();

            return result?.Models?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error reading Fantasy Calc dynasty value for player {PlayerId} and format {NumTeams}/{NumQbs}/{Ppr}/{TePremium}",
                playerId, settings.NumTeams, settings.NumQbs, settings.Ppr, settings.Te_premium);
            return null;
        }
    }

    private static string NormalizeTePremium(string? tePremium)
    {
        return string.IsNullOrWhiteSpace(tePremium) ? "NOTEP" : tePremium.Trim().ToUpperInvariant();
    }

    private async Task<bool> UpdateSinglePlayerDynastyValue(long sleeperId, int dynastyValue, int fantasyCalcPlayerId)
    {
        try
        {
            var updateResult = await _supabaseClient
                .From<Player>()
                .Where(p => p.SleeperId == sleeperId)
                .Set(p => p.FantasyCalcDynastyValue, dynastyValue)
                .Set(p => p.FantasyCalcPlayerId, fantasyCalcPlayerId)
                .Update();

            return updateResult?.Models?.Any() == true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating dynasty value and fantasy calc player ID for player with Sleeper ID {SleeperId}", sleeperId);
            return false;
        }
    }

    /// <summary>
    /// Updates dynasty values for draft picks matched by full name (first_name + ' ' + last_name).
    /// Picks in the database have no sleeper_id, so they cannot be matched by Sleeper ID.
    /// </summary>
    public async Task<int> UpdatePickDynastyValuesAsync(Dictionary<string, int> pickValues)
    {
        if (!pickValues.Any())
        {
            _logger.LogInformation("No pick dynasty values to update");
            return 0;
        }

        try
        {
            _logger.LogInformation("Fetching draft pick players (null sleeper_id) to match against {Count} FC pick values", pickValues.Count);

            // Picks in the DB have no sleeper_id — fetch them all
            var allPicks = new List<Player>();
            int pageSize = 1000;
            int offset = 0;
            bool hasMore = true;

            while (hasMore)
            {
                var pageResult = await _supabaseClient
                    .From<Player>()
                    .Where(p => p.SleeperId == null)
                    .Range(offset, offset + pageSize - 1)
                    .Get();

                if (pageResult?.Models == null || !pageResult.Models.Any())
                {
                    hasMore = false;
                    break;
                }

                allPicks.AddRange(pageResult.Models);

                if (pageResult.Models.Count < pageSize)
                    hasMore = false;
                else
                    offset += pageSize;
            }

            _logger.LogInformation("Fetched {Count} players with null sleeper_id from database", allPicks.Count);

            // Build secondary map for generic round picks: "2026 1st" -> value, keyed as "2026-1"
            var ordinalToRound = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["1st"] = 1,
                ["2nd"] = 2,
                ["3rd"] = 3,
                ["4th"] = 4,
                ["5th"] = 5,
                ["6th"] = 6,
                ["7th"] = 7,
                ["8th"] = 8,
                ["9th"] = 9,
                ["10th"] = 10
            };

            var roundPickMap = new Dictionary<string, int>();
            foreach (var kvp in pickValues)
            {
                var parts = kvp.Key.Split(' ');
                if (parts.Length == 2 && parts[0].Length == 4 && char.IsDigit(parts[0][0])
                    && ordinalToRound.TryGetValue(parts[1], out int roundNum))
                {
                    roundPickMap[$"{parts[0]}-{roundNum}"] = kvp.Value;
                }
            }

            _logger.LogInformation("Built round pick map with {Count} entries (e.g. '2026-1', '2027-2')", roundPickMap.Count);

            var updateTasks = new List<Task<bool>>();
            var tiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Early", "Mid", "Late" };

            foreach (var pick in allPicks)
            {
                var fullName = $"{pick.FirstName} {pick.LastName}".Trim();

                if (pickValues.TryGetValue(fullName, out int dynastyValue))
                {
                    // Exact match (e.g. "2026 Pick 1.01")
                    _logger.LogDebug("Preparing update for pick '{PickName}' (ID: {PickId}) - Dynasty Value: {DynastyValue}",
                        fullName, pick.Id, dynastyValue);
                    updateTasks.Add(UpdateSinglePickDynastyValue(pick.Id, dynastyValue));
                }
                else
                {
                    // Fallback: match tier picks like "2026 Mid 1st" -> "2026-1"
                    var parts = fullName.Split(' ');
                    if (parts.Length == 3 && parts[0].Length == 4 && char.IsDigit(parts[0][0])
                        && tiers.Contains(parts[1]) && ordinalToRound.TryGetValue(parts[2], out int round)
                        && roundPickMap.TryGetValue($"{parts[0]}-{round}", out int tierValue))
                    {
                        _logger.LogDebug("Preparing tier pick update for '{PickName}' (ID: {PickId}) - Dynasty Value: {DynastyValue}",
                            fullName, pick.Id, tierValue);
                        updateTasks.Add(UpdateSinglePickDynastyValue(pick.Id, tierValue));
                    }
                }
            }

            if (!updateTasks.Any())
            {
                _logger.LogWarning("No picks in database matched any Fantasy Calc pick names");
                return 0;
            }

            var results = await Task.WhenAll(updateTasks);
            var updatedCount = results.Count(r => r);
            _logger.LogInformation("Pick dynasty update completed: {UpdatedCount} picks updated", updatedCount);
            return updatedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing batch update for pick dynasty values");
            return 0;
        }
    }

    private async Task<bool> UpdateSinglePickDynastyValue(long pickId, int dynastyValue)
    {
        try
        {
            var updateResult = await _supabaseClient
                .From<Player>()
                .Where(p => p.Id == pickId)
                .Set(p => p.FantasyCalcDynastyValue, dynastyValue)
                .Update();

            return updateResult?.Models?.Any() == true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating dynasty value for pick with ID {PickId}", pickId);
            return false;
        }
    }

    /// <summary>
    /// Inserts historical trades from Fantasy Calculator into the database
    /// </summary>
    /// <param name="trades">List of trades to insert</param>
    /// <returns>Number of trades successfully inserted</returns>
    public async Task<int> InsertHistoricalTradesAsync(List<Models.FantasyCalcTrade> trades)
    {
        if (!trades.Any())
        {
            _logger.LogInformation("No trades to insert");
            return 0;
        }

        try
        {
            _logger.LogInformation("Inserting {Count} historical trades", trades.Count);

            // Get all existing trade IDs to avoid duplicates
            var existingTradeIds = new HashSet<string>();
            int offset = 0;
            int pageSize = 1000;
            bool hasMore = true;

            while (hasMore)
            {
                var existingTrades = await _supabaseClient
                    .From<HistoricalTrade>()
                    .Select("fantasy_calc_trade_id")
                    .Range(offset, offset + pageSize - 1)
                    .Get();

                if (existingTrades?.Models == null || !existingTrades.Models.Any())
                {
                    hasMore = false;
                    break;
                }

                foreach (var trade in existingTrades.Models)
                {
                    existingTradeIds.Add(trade.FantasyCalcTradeId);
                }

                if (existingTrades.Models.Count < pageSize)
                {
                    hasMore = false;
                }
                else
                {
                    offset += pageSize;
                }
            }

            _logger.LogInformation("Found {Count} existing trades in database", existingTradeIds.Count);

            // Filter to only new trades
            var newTrades = trades.Where(t => !existingTradeIds.Contains(t.Id)).ToList();

            if (!newTrades.Any())
            {
                _logger.LogInformation("All trades already exist in database");
                return 0;
            }

            _logger.LogInformation("Inserting {NewCount} new trades (skipping {SkipCount} existing)",
                newTrades.Count, trades.Count - newTrades.Count);

            // Get all players and draft picks to map sleeper IDs to player IDs
            var playerLookup = await GetPlayerLookupDictionary();
            var draftPickLookup = await GetDraftPickLookupDictionary();

            int insertedCount = 0;

            foreach (var trade in newTrades)
            {
                try
                {
                    // Skip trades involving 5th or 6th round picks
                    var allPlayers = trade.Side1.Concat(trade.Side2);
                    var has5thOr6thRoundPick = allPlayers.Any(p =>
                        p.Position == "PICK" &&
                        (p.Name.Contains("5th", StringComparison.OrdinalIgnoreCase) ||
                         p.Name.Contains("6th", StringComparison.OrdinalIgnoreCase)));

                    if (has5thOr6thRoundPick)
                    {
                        _logger.LogDebug("Skipping trade {TradeId} - contains 5th or 6th round pick", trade.Id);
                        continue;
                    }

                    // Create HistoricalTrade record
                    var historicalTrade = new HistoricalTrade
                    {
                        FantasyCalcTradeId = trade.Id,
                        TradeDate = trade.Date,
                        NumTeams = trade.NumTeams,
                        NumQbs = (int)trade.NumQbs,
                        Ppr = trade.Ppr,
                        IsDynasty = trade.IsDynasty,
                        NumStarters = trade.NumStarters,
                        RosterSize = trade.RosterSize,
                        TePremium = trade.TePremium,
                        NumSuperflex = trade.NumSuperflex,
                        SiteLeagueId = trade.SiteLeagueId
                    };

                    // Insert the trade
                    var insertedTrade = await _supabaseClient
                        .From<HistoricalTrade>()
                        .Insert(historicalTrade);

                    if (insertedTrade?.Models?.Any() != true)
                    {
                        _logger.LogWarning("Failed to insert trade {TradeId}", trade.Id);
                        continue;
                    }

                    var insertedTradeModel = insertedTrade.Models.First();
                    if (!insertedTradeModel.Id.HasValue)
                    {
                        _logger.LogWarning("Inserted trade {TradeId} has no ID", trade.Id);
                        continue;
                    }

                    var insertedTradeId = insertedTradeModel.Id.Value;

                    // Insert assets for side 1
                    await InsertTradeAssets(insertedTradeId, trade.Side1, 1, playerLookup, draftPickLookup);

                    // Insert assets for side 2
                    await InsertTradeAssets(insertedTradeId, trade.Side2, 2, playerLookup, draftPickLookup);

                    insertedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error inserting trade {TradeId}", trade.Id);
                }
            }

            _logger.LogInformation("Successfully inserted {Count} historical trades", insertedCount);
            return insertedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inserting historical trades");
            return 0;
        }
    }

    private async Task<Dictionary<string, int>> GetPlayerLookupDictionary()
    {
        var playerLookup = new Dictionary<string, int>();
        int offset = 0;
        int pageSize = 1000;
        bool hasMore = true;

        _logger.LogInformation("Building player lookup dictionary...");

        while (hasMore)
        {
            var players = await _supabaseClient
                .From<Player>()
                .Where(p => p.SleeperId != null)
                .Select("id, sleeper_id")
                .Range(offset, offset + pageSize - 1)
                .Get();

            if (players?.Models == null || !players.Models.Any())
            {
                hasMore = false;
                break;
            }

            foreach (var player in players.Models)
            {
                if (player.SleeperId.HasValue)
                {
                    playerLookup[player.SleeperId.Value.ToString()] = (int)player.Id;
                }
            }

            if (players.Models.Count < pageSize)
            {
                hasMore = false;
            }
            else
            {
                offset += pageSize;
            }
        }

        _logger.LogInformation("Built lookup dictionary with {Count} players", playerLookup.Count);
        return playerLookup;
    }

    /// <summary>
    /// Builds a lookup dictionary for draft picks by their name (e.g., "2026 Pick 1.07" or "2026 1.07")
    /// </summary>
    private async Task<Dictionary<string, int>> GetDraftPickLookupDictionary()
    {
        var pickLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var midPickLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int offset = 0;
        int pageSize = 1000;
        bool hasMore = true;

        _logger.LogInformation("Building draft pick lookup dictionary by name...");

        while (hasMore)
        {
            // Fetch all players, then filter for picks in memory
            // Supabase Postgrest has issues with numeric comparisons in LINQ
            var players = await _supabaseClient
                .From<Player>()
                .Select("id, position_id, first_name, last_name")
                .Range(offset, offset + pageSize - 1)
                .Get();

            if (players?.Models == null || !players.Models.Any())
            {
                hasMore = false;
                break;
            }

            _logger.LogDebug("Fetched {Count} players at offset {Offset}", players.Models.Count, offset);

            // Filter for draft picks (position_id == 5) in memory
            var picks = players.Models.Where(p => p.PositionId == 5).ToList();

            _logger.LogDebug("Found {PickCount} picks out of {PlayerCount} players", picks.Count, players.Models.Count);

            // Log first few picks to see format
            foreach (var pick in picks.Take(5))
            {
                _logger.LogInformation("Sample pick from DB: ID={Id}, PositionId={Pos}, FirstName='{First}', LastName='{Last}'",
                    pick.Id, pick.PositionId, pick.FirstName ?? "NULL", pick.LastName ?? "NULL");
            }

            foreach (var pick in picks)
            {
                var fullName = $"{pick.FirstName} {pick.LastName}".Trim();
                var firstNameOnly = pick.FirstName?.Trim() ?? "";

                // Add full name mapping (e.g., "2026 Pick 1.07")
                if (!string.IsNullOrWhiteSpace(fullName) && !pickLookup.ContainsKey(fullName))
                {
                    pickLookup[fullName] = (int)pick.Id;
                    _logger.LogDebug("Added pick to lookup: '{PickName}' -> DB ID: {DbId}", fullName, pick.Id);
                }

                // Also add first name only mapping (e.g., "2026 1.07")
                if (!string.IsNullOrWhiteSpace(firstNameOnly) && !pickLookup.ContainsKey(firstNameOnly))
                {
                    pickLookup[firstNameOnly] = (int)pick.Id;
                }

                // Track mid picks for generic round mapping
                // e.g., "2027 Mid 4th" should map "2027 4th" to this pick
                if (!string.IsNullOrWhiteSpace(fullName) && fullName.Contains("Mid", StringComparison.OrdinalIgnoreCase))
                {
                    // Pattern: "YYYY Mid Xth" or "YYYY Mid Xnd" or "YYYY Mid Xrd" or "YYYY Mid Xst"
                    var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        var year = parts[0];
                        // Look for the part after "Mid"
                        var midIndex = Array.FindIndex(parts, p => p.Equals("Mid", StringComparison.OrdinalIgnoreCase));
                        if (midIndex >= 0 && midIndex + 1 < parts.Length)
                        {
                            var round = parts[midIndex + 1]; // e.g., "4th", "1st", "2nd", "3rd"
                            var genericKey = $"{year} {round}";
                            if (!midPickLookup.ContainsKey(genericKey))
                            {
                                midPickLookup[genericKey] = (int)pick.Id;
                                _logger.LogDebug("Added mid pick mapping: '{GenericKey}' -> '{PickName}' (DB ID: {DbId})",
                                    genericKey, fullName, pick.Id);
                            }
                        }
                    }
                }
            }

            if (players.Models.Count < pageSize)
            {
                hasMore = false;
            }
            else
            {
                offset += pageSize;
            }
        }

        _logger.LogInformation("Built draft pick lookup dictionary with {Count} entries ({MidCount} mid-round mappings)",
            pickLookup.Count, midPickLookup.Count);

        // Add mid pick mappings for generic round lookups
        // e.g., "2027 4th" maps to the ID of "2027 Mid 4th"
        foreach (var kvp in midPickLookup)
        {
            if (!pickLookup.ContainsKey(kvp.Key))
            {
                pickLookup[kvp.Key] = kvp.Value;
                _logger.LogDebug("Added generic round mapping: '{Key}' -> DB ID: {DbId}", kvp.Key, kvp.Value);
            }
        }

        // Log some sample picks for debugging
        var samplePicks = pickLookup.Take(10).Select(p => p.Key).ToList();
        if (samplePicks.Any())
        {
            _logger.LogInformation("Sample picks in lookup: {Samples}", string.Join(", ", samplePicks));
        }

        return pickLookup;
    }

    private async Task InsertTradeAssets(int tradeId, List<Models.TradePlayer> players, int side, Dictionary<string, int> playerLookup, Dictionary<string, int> draftPickLookup)
    {
        foreach (var player in players)
        {
            int? playerId = null;

            // Check if this is a draft pick
            if (player.Position == "PICK")
            {
                // Try to find pick by name (e.g., "2026 Pick 1.07" or "2026 1st")
                if (!string.IsNullOrEmpty(player.Name) && draftPickLookup.TryGetValue(player.Name, out int foundPickId))
                {
                    playerId = foundPickId;
                    _logger.LogDebug("Matched draft pick: '{PickName}' -> DB ID: {DbId}",
                        player.Name, foundPickId);
                }
                else
                {
                    // Show similar picks to help debug
                    var similarPicks = draftPickLookup.Keys
                        .Where(k => k.Contains(player.Name?.Split(' ').FirstOrDefault() ?? "", StringComparison.OrdinalIgnoreCase))
                        .Take(5)
                        .ToList();

                    _logger.LogWarning("Could not find draft pick in database: '{PickName}'. Similar picks: {Similar}",
                        player.Name, similarPicks.Any() ? string.Join(", ", similarPicks) : "none");
                    continue;
                }
            }
            else
            {
                // Try to find player by sleeper ID
                if (!string.IsNullOrEmpty(player.SleeperId) && playerLookup.TryGetValue(player.SleeperId, out int foundPlayerId))
                {
                    playerId = foundPlayerId;
                }
                else
                {
                    _logger.LogWarning("Could not find player in database: {PlayerName} (Sleeper ID: {SleeperId})",
                        player.Name, player.SleeperId);
                    continue;
                }
            }

            var asset = new HistoricalTradeAsset
            {
                HistoricalTradeId = tradeId,
                PlayerId = playerId,
                Side = side
            };

            try
            {
                await _supabaseClient
                    .From<HistoricalTradeAsset>()
                    .Insert(asset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting trade asset for player {PlayerName}", player.Name);
            }
        }
    }
}
