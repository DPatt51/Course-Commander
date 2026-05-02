using CourseCommander.Data;
using CourseCommander.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CourseCommander.Services;

public class TrendService
{
    private readonly AppDbContext _context;
    private readonly GrowingDegreeDayService _gddService;

    public TrendService(AppDbContext context, GrowingDegreeDayService gddService)
    {
        _context = context;
        _gddService = gddService;
    }

    public async Task<List<TrendPointDto>> GetRoundsTrendAsync(int days, DateOnly? endDate = null)
    {
        var startDate = GetStartDate(days, endDate);
        var lastDate = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var playMetrics = await _context.DailyPlayMetrics
            .Where(metric => metric.Date >= startDate && metric.Date <= lastDate)
            .ToListAsync();

        var points = playMetrics
            .GroupBy(metric => metric.Date)
            .Select(group => group.OrderByDescending(metric => metric.SyncedAt).First())
            .OrderBy(metric => metric.Date)
            .Select(metric => new TrendPointDto
            {
                Date = metric.Date,
                Value = metric.RoundsPlayed
            })
            .ToList();

        AddPercentChanges(points);
        return points;
    }

    public async Task<List<TrendPointDto>> GetRevenueTrendAsync(int days, DateOnly? endDate = null)
    {
        var startDate = GetStartDate(days, endDate);
        var lastDate = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var salesMetrics = await _context.DailySalesMetrics
            .Where(metric => metric.Date >= startDate && metric.Date <= lastDate)
            .ToListAsync();

        var points = salesMetrics
            .GroupBy(metric => metric.Date)
            .Select(group => group.OrderByDescending(metric => metric.SyncedAt).First())
            .OrderBy(metric => metric.Date)
            .Select(metric => new TrendPointDto
            {
                Date = metric.Date,
                Value = metric.TotalRevenue
            })
            .ToList();

        AddPercentChanges(points);
        return points;
    }

    public async Task<List<TrendPointDto>> GetGddTrendAsync(int days, DateOnly? endDate = null)
    {
        var lastDate = endDate?.ToDateTime(TimeOnly.MinValue) ?? DateTime.UtcNow.Date;
        var startDate = lastDate.AddDays(-Math.Max(days, 1) + 1);
        var gddSummary = await _gddService.GetRangeGddAsync(startDate, lastDate);

        var points = gddSummary.DailyValues
            .OrderBy(value => value.Date)
            .Select(value => new TrendPointDto
            {
                Date = value.Date,
                Value = value.Gdd
            })
            .ToList();

        AddPercentChanges(points);
        return points;
    }

    public TrendDirection? GetConsecutiveTrend(List<TrendPointDto> points)
    {
        if (points.Count < 4)
        {
            return null;
        }

        var latestDirection = 0;
        var consecutiveChanges = 0;

        for (var index = points.Count - 1; index > 0; index--)
        {
            var difference = points[index].Value - points[index - 1].Value;

            if (difference == 0)
            {
                break;
            }

            var direction = difference > 0 ? 1 : -1;

            if (latestDirection == 0)
            {
                latestDirection = direction;
            }

            if (direction != latestDirection)
            {
                break;
            }

            consecutiveChanges++;
        }

        if (consecutiveChanges < 3)
        {
            return null;
        }

        return new TrendDirection
        {
            Direction = latestDirection > 0 ? "Up" : "Down",
            ConsecutiveChanges = consecutiveChanges
        };
    }

    private static DateOnly GetStartDate(int days, DateOnly? endDate = null)
    {
        var lastDate = endDate?.ToDateTime(TimeOnly.MinValue) ?? DateTime.UtcNow.Date;
        return DateOnly.FromDateTime(lastDate.AddDays(-Math.Max(days, 1) + 1));
    }

    private static void AddPercentChanges(List<TrendPointDto> points)
    {
        for (var index = 1; index < points.Count; index++)
        {
            var previousValue = points[index - 1].Value;

            if (previousValue == 0)
            {
                points[index].PercentChangeFromPreviousDay = null;
                continue;
            }

            points[index].PercentChangeFromPreviousDay = (points[index].Value - previousValue) / previousValue * 100;
        }
    }
}

public class TrendDirection
{
    public string Direction { get; set; } = string.Empty;
    public int ConsecutiveChanges { get; set; }
}
