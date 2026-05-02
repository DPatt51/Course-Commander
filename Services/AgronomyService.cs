using CourseCommander.Data;
using CourseCommander.DTOs;
using CourseCommander.Entities;
using Microsoft.EntityFrameworkCore;

namespace CourseCommander.Services;

public class AgronomyService
{
    private readonly AppDbContext _context;

    public AgronomyService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AgronomySummaryDto> GetMoistureSummaryAsync(DateOnly date)
    {
        var start = date.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        var moistureReadings = await _context.AgronomyReadings
            .Where(reading =>
                reading.DateTime >= start &&
                reading.DateTime < end &&
                reading.MeasurementType.ToLower() == "moisture")
            .ToListAsync();

        var summary = new AgronomySummaryDto
        {
            Date = date
        };

        if (moistureReadings.Count == 0)
        {
            return summary;
        }

        var orderedMoistureReadings = moistureReadings
            .OrderBy(reading => reading.Value)
            .ToList();

        summary.AverageMoistureToday = Math.Round(moistureReadings.Average(reading => reading.Value), 1);
        summary.LowestMoistureReading = orderedMoistureReadings.First().Value;
        summary.HighestMoistureReading = orderedMoistureReadings.Last().Value;
        summary.TopDriestLocations = orderedMoistureReadings
            .GroupBy(reading => new { reading.Location, reading.Zone })
            .Select(group => group.OrderBy(reading => reading.Value).First())
            .OrderBy(reading => reading.Value)
            .Take(3)
            .Select(reading => new DriestLocationDto
            {
                Location = reading.Location,
                Zone = reading.Zone,
                Value = reading.Value,
                Unit = reading.Unit
            })
            .ToList();

        return summary;
    }

    public async Task<List<AgronomyReading>> GetReadingsForDateAsync(DateOnly date)
    {
        var start = date.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);

        return await _context.AgronomyReadings
            .Where(reading => reading.DateTime >= start && reading.DateTime < end)
            .OrderBy(reading => reading.Location)
            .ThenBy(reading => reading.Zone)
            .ThenBy(reading => reading.DateTime)
            .ToListAsync();
    }
}
