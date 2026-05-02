using CourseCommander.DTOs;
using CourseCommander.Services;
using Microsoft.AspNetCore.Mvc;

namespace CourseCommander.Controllers;

[ApiController]
[Route("api/trends")]
public class TrendsController : ControllerBase
{
    private readonly TrendService _trendService;

    public TrendsController(TrendService trendService)
    {
        _trendService = trendService;
    }

    [HttpGet("rounds")]
    public async Task<ActionResult<IEnumerable<TrendPointDto>>> GetRoundsTrend(
        [FromQuery] int days = 7,
        [FromQuery] DateOnly? endDate = null)
    {
        return Ok(await _trendService.GetRoundsTrendAsync(days, endDate));
    }

    [HttpGet("revenue")]
    public async Task<ActionResult<IEnumerable<TrendPointDto>>> GetRevenueTrend(
        [FromQuery] int days = 7,
        [FromQuery] DateOnly? endDate = null)
    {
        return Ok(await _trendService.GetRevenueTrendAsync(days, endDate));
    }

    [HttpGet("gdd")]
    public async Task<ActionResult<IEnumerable<TrendPointDto>>> GetGddTrend(
        [FromQuery] int days = 30,
        [FromQuery] DateOnly? endDate = null)
    {
        return Ok(await _trendService.GetGddTrendAsync(days, endDate));
    }
}
