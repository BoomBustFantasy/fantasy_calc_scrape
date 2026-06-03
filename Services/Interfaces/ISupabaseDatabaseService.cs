namespace FantasyCalcScrape.Services.Interfaces;

/// <summary>
/// Interface for updating player data in Supabase
/// </summary>
public interface ISupabaseDatabaseService
{
    /// <summary>
    /// Updates a player's redraft value by Sleeper ID
    /// </summary>
    /// <param name="sleeperId">The player's Sleeper ID</param>
    /// <param name="redraftValue">The new redraft value</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> UpdatePlayerRedraftValueAsync(string sleeperId, int redraftValue);

    /// <summary>
    /// Updates multiple players' redraft values and fantasy calc player IDs in a batch operation
    /// </summary>
    /// <param name="playerValues">Dictionary of Sleeper ID to tuple of (redraft value, fantasy calc player ID)</param>
    /// <returns>Number of players successfully updated</returns>
    Task<int> UpdatePlayerRedraftValuesAsync(Dictionary<string, (int redraftValue, int fantasyCalcPlayerId)> playerValues);

    /// <summary>
    /// Updates multiple players' dynasty values and fantasy calc player IDs in a batch operation
    /// </summary>
    /// <param name="playerValues">Dictionary of Sleeper ID to tuple of (dynasty value, fantasy calc player ID)</param>
    /// <returns>Number of players successfully updated</returns>
    Task<int> UpdatePlayerDynastyValuesAsync(Dictionary<string, (int dynastyValue, int fantasyCalcPlayerId)> playerValues);

    /// <summary>
    /// Inserts historical trades from Fantasy Calculator into the database
    /// </summary>
    /// <param name="trades">List of trades to insert</param>
    /// <returns>Number of trades successfully inserted</returns>
    Task<int> InsertHistoricalTradesAsync(List<Models.FantasyCalcTrade> trades);
}