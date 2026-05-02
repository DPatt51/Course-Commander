using CourseCommander.Data;
using CourseCommander.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseCommander.Controllers;

[ApiController]
[Route("api/daily-operation-metrics")]
public class DailyOperationMetricsController : ControllerBase
{
    private readonly AppDbContext _context;

    public DailyOperationMetricsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DailyOperationMetric>>> GetDailyOperationMetrics()
    {
        return await _context.DailyOperationMetrics
            .OrderByDescending(metric => metric.Date)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DailyOperationMetric>> GetDailyOperationMetric(int id)
    {
        var metric = await _context.DailyOperationMetrics.FindAsync(id);

        if (metric is null)
        {
            return NotFound();
        }

        return metric;
    }

    [HttpPost]
    public async Task<ActionResult<DailyOperationMetric>> CreateDailyOperationMetric(DailyOperationMetric metric)
    {
        var validationError = GetValidationError(metric);

        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        metric.CreatedAt = DateTime.UtcNow;

        _context.DailyOperationMetrics.Add(metric);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetDailyOperationMetric), new { id = metric.Id }, metric);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDailyOperationMetric(int id, DailyOperationMetric metric)
    {
        if (id != metric.Id)
        {
            return BadRequest();
        }

        var validationError = GetValidationError(metric);

        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var exists = await _context.DailyOperationMetrics.AnyAsync(existingMetric => existingMetric.Id == id);

        if (!exists)
        {
            return NotFound();
        }

        _context.Entry(metric).State = EntityState.Modified;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static string? GetValidationError(DailyOperationMetric metric)
    {
        if (metric.Date == default)
        {
            return "Date is required.";
        }

        if (metric.RoundsPlayed < 0)
        {
            return "RoundsPlayed cannot be negative.";
        }

        if (metric.CartRentals < 0)
        {
            return "CartRentals cannot be negative.";
        }

        if (metric.TotalRevenue < 0)
        {
            return "TotalRevenue cannot be negative.";
        }

        return null;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDailyOperationMetric(int id)
    {
        var metric = await _context.DailyOperationMetrics.FindAsync(id);

        if (metric is null)
        {
            return NotFound();
        }

        _context.DailyOperationMetrics.Remove(metric);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
