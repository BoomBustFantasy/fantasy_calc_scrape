using FantasyCalcScrape.Models;
using FantasyCalcScrape.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FantasyCalcScrape.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FantasyCalcController : ControllerBase
{
    private readonly IFantasyCalcApiService _valuesService;
    private readonly ILogger<FantasyCalcController> _logger;

    public FantasyCalcController(
        IFantasyCalcApiService valuesService,
        ILogger<FantasyCalcController> logger)
    {
        _valuesService = valuesService;
        _logger = logger;
    }

    /// <summary>
    /// Get current redraft player values from Fantasy Calculator
    /// </summary>
    [HttpGet("values/redraft")]
    public async Task<ActionResult<FantasyCalcValueResponse>> GetRedraftValues(
        [FromQuery] int numQbs = 1,
        [FromQuery] int numTeams = 12,
        [FromQuery] decimal ppr = 1.0m)
    {
        try
        {
            var response = await _valuesService.GetRedraftValuesAsync(numQbs, numTeams, ppr);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving redraft values");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get current dynasty player values from Fantasy Calculator
    /// </summary>
    [HttpGet("values/dynasty")]
    public async Task<ActionResult<FantasyCalcValueResponse>> GetDynastyValues(
        [FromQuery] int numQbs = 1,
        [FromQuery] int numTeams = 12,
        [FromQuery] decimal ppr = 1.0m)
    {
        try
        {
            var response = await _valuesService.GetDynastyValuesAsync(numQbs, numTeams, ppr);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dynasty values");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}