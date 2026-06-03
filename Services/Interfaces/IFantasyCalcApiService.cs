using FantasyCalcScrape.Models;

namespace FantasyCalcScrape.Services.Interfaces;

public interface IFantasyCalcApiService
{
    /// <summary>
    /// Gets current player values from Fantasy Calculator API
    /// </summary>
    Task<FantasyCalcValueResponse> GetCurrentValuesAsync(FantasyCalcApiSettings? settings = null);

    /// <summary>
    /// Gets current values for redraft leagues (non-dynasty)
    /// </summary>
    Task<FantasyCalcValueResponse> GetRedraftValuesAsync(int numQbs = 1, int numTeams = 12, decimal ppr = 1.0m);

    /// <summary>
    /// Gets current values for dynasty leagues
    /// </summary>
    Task<FantasyCalcValueResponse> GetDynastyValuesAsync(int numQbs = 1, int numTeams = 12, decimal ppr = 1.0m);

    /// <summary>
    /// Gets historical trades from Fantasy Calculator API
    /// </summary>
    Task<FantasyCalcTradeResponse> GetTradesAsync(
        bool isDynasty = true,
        int numTeams = 12,
        decimal ppr = 1.0m,
        int numQbs = 2,
        int minPlayers = 2,
        int maxPlayers = 8);
}