using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FantasyCalcScrape.HealthChecks;

public class FantasyCalcApiHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FantasyCalcApiHealthCheck> _logger;

    public FantasyCalcApiHealthCheck(
        HttpClient httpClient,
        ILogger<FantasyCalcApiHealthCheck> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Configure HttpClient if not already done
        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri("https://api.fantasycalc.com/");
        }
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Starting Fantasy Calc API health check");

            // Check if we can make a basic connection to Fantasy Calc API
            using var response = await _httpClient.GetAsync("values/current?isDynasty=false&numQbs=1&numTeams=12&ppr=1");

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Fantasy Calc API health check passed");
                return HealthCheckResult.Healthy("Fantasy Calc API is responsive");
            }

            _logger.LogWarning("Fantasy Calc API health check failed with status: {StatusCode}",
                response.StatusCode);
            return HealthCheckResult.Unhealthy($"Fantasy Calc API returned status: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fantasy Calc API health check failed");
            return HealthCheckResult.Unhealthy("Fantasy Calc API is not accessible", ex);
        }
    }
}