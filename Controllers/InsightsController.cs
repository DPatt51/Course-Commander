using CourseCommander.Data;
using CourseCommander.Entities;
using CourseCommander.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseCommander.Controllers;

[ApiController]
[Route("api/insights")]
public class InsightsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly InsightService _insightService;

    public InsightsController(AppDbContext context, InsightService insightService)
    {
        _context = context;
        _insightService = insightService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Insight>>> GetInsights()
    {
        return await _context.Insights
            .OrderByDescending(insight => insight.CreatedAt)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Insight>> GetInsight(int id)
    {
        var insight = await _context.Insights.FindAsync(id);

        if (insight is null)
        {
            return NotFound();
        }

        return insight;
    }

    [HttpPost]
    public async Task<ActionResult<Insight>> CreateInsight(Insight insight)
    {
        insight.CreatedAt = DateTime.UtcNow;

        _context.Insights.Add(insight);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetInsight), new { id = insight.Id }, insight);
    }

    [HttpPost("generate")]
    public async Task<ActionResult<IEnumerable<Insight>>> GenerateInsight(DateOnly? date)
    {
        var insightDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var insights = await _insightService.GenerateDailyInsightsAsync(insightDate);

        return Ok(insights);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteInsight(int id)
    {
        var insight = await _context.Insights.FindAsync(id);

        if (insight is null)
        {
            return NotFound();
        }

        _context.Insights.Remove(insight);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
