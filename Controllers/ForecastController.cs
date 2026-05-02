using CourseCommander.DTOs;
using CourseCommander.Services;
using Microsoft.AspNetCore.Mvc;

namespace CourseCommander.Controllers;

[ApiController]
[Route("api/forecast")]
public class ForecastController : ControllerBase
{
    private readonly ForecastService _forecastService;

    public ForecastController(ForecastService forecastService)
    {
        _forecastService = forecastService;
    }

    [HttpGet]
    public async Task<ActionResult<ForecastDto>> GetForecast([FromQuery] DateOnly? date)
    {
        if (date is null)
        {
            return BadRequest("Date is required. Example: /api/forecast?date=2026-04-28");
        }

        return Ok(await _forecastService.GetForecastAsync(date.Value));
    }
}
