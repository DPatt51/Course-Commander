using CourseCommander.Data;
using CourseCommander.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CourseCommander.Services;

public class GrowingDegreeDayService
{
    private const decimal DefaultBaseTemperature = 50m;

    private readonly AppDbContext _context;

    public GrowingDegreeDayService(AppDbContext context)
    {
        _context = context;
    }

    public decimal CalculateDailyGdd(decimal highTemp, decimal lowTemp, decimal baseTemp = DefaultBaseTemperature)
    {
        var gdd = ((highTemp + lowTemp) / 2) - baseTemp;
        return gdd < 0 ? 0 : gdd;
    }

    public async Task<DailyGddDto?> GetDailyGddAsync(DateTime date)
    {
        var dateOnly = DateOnly.FromDateTime(date);
        var weatherMetric = await _context.DailyWeatherMetrics
            .Where(metric => metric.Date == dateOnly)
            .OrderByDescending(metric => metric.SyncedAt)
            .FirstOrDefaultAsync();

        if (weatherMetric is null)
        {
            return null;
        }

        return new DailyGddDto
        {
            Date = weatherMetric.Date,
            HighTemp = weatherMetric.HighTemp,
            LowTemp = weatherMetric.LowTemp,
            BaseTemperature = DefaultBaseTemperature,
            Gdd = CalculateDailyGdd(weatherMetric.HighTemp, weatherMetric.LowTemp)
        };
    }

    public async Task<GddSummaryDto> GetRangeGddAsync(DateTime startDate, DateTime endDate)
    {
        var startDateOnly = DateOnly.FromDateTime(startDate);
        var endDateOnly = DateOnly.FromDateTime(endDate);

        if (startDateOnly > endDateOnly)
        {
            throw new InvalidOperationException("startDate must be before or equal to endDate.");
        }

        var weatherMetrics = await _context.DailyWeatherMetrics
            .Where(metric => metric.Date >= startDateOnly && metric.Date <= endDateOnly)
            .OrderBy(metric => metric.Date)
            .ToListAsync();

        var dailyValues = weatherMetrics
            .GroupBy(metric => metric.Date)
            .Select(group => group.OrderByDescending(metric => metric.SyncedAt).First())
            .Select(metric => new DailyGddDto
            {
                Date = metric.Date,
                HighTemp = metric.HighTemp,
                LowTemp = metric.LowTemp,
                BaseTemperature = DefaultBaseTemperature,
                Gdd = CalculateDailyGdd(metric.HighTemp, metric.LowTemp)
            })
            .ToList();

        var totalGdd = dailyValues.Sum(value => value.Gdd);

        return new GddSummaryDto
        {
            StartDate = startDateOnly,
            EndDate = endDateOnly,
            BaseTemperature = DefaultBaseTemperature,
            TotalGdd = totalGdd,
            AverageDailyGdd = dailyValues.Count == 0 ? 0 : totalGdd / dailyValues.Count,
            DaysIncluded = dailyValues.Count,
            DailyValues = dailyValues
        };
    }

    public Task<GddSummaryDto> GetPast30DaysGddAsync()
    {
        var endDate = DateTime.UtcNow.Date;
        var startDate = endDate.AddDays(-29);

        return GetRangeGddAsync(startDate, endDate);
    }

    public Task<GddSummaryDto> GetYearToDateGddAsync(int year)
    {
        var startDate = new DateTime(year, 1, 1);
        var today = DateTime.UtcNow.Date;
        var endDate = year == today.Year ? today : new DateTime(year, 12, 31);

        return GetRangeGddAsync(startDate, endDate);
    }
}
