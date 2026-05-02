using CourseCommander.DTOs;
using CourseCommander.Services;
using Microsoft.AspNetCore.Mvc;

namespace CourseCommander.Controllers;

[ApiController]
[Route("api/demo")]
public class DemoController : ControllerBase
{
    private readonly DemoDataService _demoDataService;

    public DemoController(DemoDataService demoDataService)
    {
        _demoDataService = demoDataService;
    }

    [HttpGet("status")]
    public async Task<ActionResult<DemoStatusDto>> GetDemoStatus()
    {
        return Ok(await _demoDataService.GetStatusAsync());
    }

    [HttpPost("load")]
    public async Task<ActionResult<DemoStatusDto>> LoadDemoData()
    {
        return Ok(await _demoDataService.LoadDemoDataAsync());
    }

    [HttpDelete("clear")]
    public async Task<ActionResult<DemoStatusDto>> ClearDemoData()
    {
        return Ok(await _demoDataService.ClearDemoDataAsync());
    }
}
