using CourseCommander.DTOs;
using CourseCommander.Services;
using Microsoft.AspNetCore.Mvc;

namespace CourseCommander.Controllers;

[ApiController]
[Route("api/gdd")]
public class GrowingDegreeDaysController : ControllerBase
{
    private readonly GrowingDegreeDayService _gddService;

    public GrowingDegreeDaysController(GrowingDegreeDayService gddService)
    {
        _gddService = gddService;
    }

    [HttpGet("daily/{date}")]
    public async Task<ActionResult<DailyGddDto>> GetDailyGdd(DateTime date)
    {
        var dailyGdd = await _gddService.GetDailyGddAsync(date);

        if (dailyGdd is null)
        {
            return NotFound($"No weather data found for {date:yyyy-MM-dd}.");
        }

        return Ok(dailyGdd);
    }

    [HttpGet("range")]
    public async Task<ActionResult<GddSummaryDto>> GetRangeGdd(DateTime startDate, DateTime endDate)
    {
        try
        {
            return Ok(await _gddService.GetRangeGddAsync(startDate, endDate));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("past-30-days")]
    public async Task<ActionResult<GddSummaryDto>> GetPast30DaysGdd()
    {
        return Ok(await _gddService.GetPast30DaysGddAsync());
    }

    [HttpGet("year-to-date/{year}")]
    public async Task<ActionResult<GddSummaryDto>> GetYearToDateGdd(int year)
    {
        return Ok(await _gddService.GetYearToDateGddAsync(year));
    }
}
