using System.Text.Json;
using System.Web;
using FantasyCalcScrape.Models;
using FantasyCalcScrape.Services.Interfaces;

namespace FantasyCalcScrape.Services;

public class FantasyCalcApiService : IFantasyCalcApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FantasyCalcApiService> _logger;
    private const string BaseValuesEndpoint = "values/current";

    public FantasyCalcApiService(
        HttpClient httpClient,
        ILogger<FantasyCalcApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Configure the HttpClient directly
        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri("https://api.fantasycalc.com/");
        }

        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "FantasyCalcScraper/1.0");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }
    }

    public async Task<FantasyCalcValueResponse> GetCurrentValuesAsync(FantasyCalcApiSettings? settings = null)
    {
        settings ??= new FantasyCalcApiSettings();

        try
        {
            _logger.LogInformation("Fetching Fantasy Calc values for settings: {Settings}",
                JsonSerializer.Serialize(settings));

            var queryString = BuildQueryString(settings);
            var endpoint = $"{BaseValuesEndpoint}?{queryString}";

            var response = await _httpClient.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Fantasy Calc API returned error status: {StatusCode}", response.StatusCode);
                return new FantasyCalcValueResponse
                {
                    Success = false,
                    Error = $"API returned status: {response.StatusCode}"
                };
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = ParseApiResponse(content);

            if (result.Success)
            {
                _logger.LogInformation("Successfully retrieved {Count} player values",
                    result.Values.Count);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Fantasy Calc values");
            return new FantasyCalcValueResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<FantasyCalcValueResponse> GetRedraftValuesAsync(int numQbs = 1, int numTeams = 12, decimal ppr = 1.0m)
    {
        var settings = new FantasyCalcApiSettings
        {
            IsDynasty = false,
            NumQbs = numQbs,
            NumTeams = numTeams,
            Ppr = ppr
        };

        return await GetCurrentValuesAsync(settings);
    }

    public async Task<FantasyCalcValueResponse> GetDynastyValuesAsync(int numQbs = 1, int numTeams = 12, decimal ppr = 1.0m)
    {
        var settings = new FantasyCalcApiSettings
        {
            IsDynasty = true,
            NumQbs = numQbs,
            NumTeams = numTeams,
            Ppr = ppr
        };

        return await GetCurrentValuesAsync(settings);
    }

    private string BuildQueryString(FantasyCalcApiSettings settings)
    {
        var queryParams = HttpUtility.ParseQueryString(string.Empty);

        queryParams["isDynasty"] = settings.IsDynasty.ToString().ToLower();
        queryParams["numQbs"] = settings.NumQbs.ToString();
        queryParams["numTeams"] = settings.NumTeams.ToString();
        queryParams["ppr"] = settings.Ppr.ToString("0.0");

        if (!string.IsNullOrEmpty(settings.Superflex))
            queryParams["superflex"] = settings.Superflex;

        if (!string.IsNullOrEmpty(settings.Te_premium))
            queryParams["te_premium"] = settings.Te_premium;

        return queryParams.ToString() ?? string.Empty;
    }

    private FantasyCalcValueResponse ParseApiResponse(string jsonContent)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // The API returns a direct array of FantasyCalcPlayer objects
            var players = JsonSerializer.Deserialize<List<FantasyCalcPlayer>>(jsonContent, options);

            return new FantasyCalcValueResponse
            {
                Success = true,
                LastUpdated = DateTime.UtcNow,
                Values = players ?? new List<FantasyCalcPlayer>()
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Fantasy Calc API response");
            return new FantasyCalcValueResponse
            {
                Success = false,
                Error = "Failed to parse API response"
            };
        }
    }

    public async Task<FantasyCalcTradeResponse> GetTradesAsync(
        bool isDynasty = true,
        int numTeams = 12,
        decimal ppr = 1.0m,
        int numQbs = 2,
        int minPlayers = 2,
        int maxPlayers = 8)
    {
        try
        {
            _logger.LogInformation("Fetching Fantasy Calc trades: isDynasty={IsDynasty}, numTeams={NumTeams}, ppr={Ppr}, numQbs={NumQbs}",
                isDynasty, numTeams, ppr, numQbs);

            var queryParams = HttpUtility.ParseQueryString(string.Empty);
            queryParams["isDynasty"] = isDynasty.ToString().ToLower();
            queryParams["numTeams"] = numTeams.ToString();
            queryParams["ppr"] = ppr.ToString("0.0");
            queryParams["numQbs"] = numQbs.ToString();
            queryParams["minPlayers"] = minPlayers.ToString();
            queryParams["maxPlayers"] = maxPlayers.ToString();

            var queryString = queryParams.ToString() ?? string.Empty;
            var endpoint = $"trades?{queryString}";

            var response = await _httpClient.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Fantasy Calc Trades API returned error status: {StatusCode}", response.StatusCode);
                return new FantasyCalcTradeResponse
                {
                    Success = false,
                    Error = $"API returned status: {response.StatusCode}"
                };
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = ParseTradesApiResponse(content);

            if (result.Success)
            {
                _logger.LogInformation("Successfully retrieved {Count} trades",
                    result.Trades.Count);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Fantasy Calc trades");
            return new FantasyCalcTradeResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private FantasyCalcTradeResponse ParseTradesApiResponse(string jsonContent)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // The API returns a direct array of FantasyCalcTrade objects
            var trades = JsonSerializer.Deserialize<List<FantasyCalcTrade>>(jsonContent, options);

            return new FantasyCalcTradeResponse
            {
                Success = true,
                LastUpdated = DateTime.UtcNow,
                Trades = trades ?? new List<FantasyCalcTrade>()
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Fantasy Calc Trades API response");
            return new FantasyCalcTradeResponse
            {
                Success = false,
                Error = "Failed to parse trades API response"
            };
        }
    }
}