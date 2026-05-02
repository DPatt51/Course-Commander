using CourseCommander.Data;
using Microsoft.EntityFrameworkCore;

namespace CourseCommander.Services;

public class DashboardMetricService
{
    private readonly AppDbContext _context;

    public DashboardMetricService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardMetricSummary> GetDailyMetricSummaryAsync(DateOnly date)
    {
        var manualMetric = await _context.DailyOperationMetrics
            .FirstOrDefaultAsync(metric => metric.Date == date);

        var playMetric = await _context.DailyPlayMetrics
            .Where(metric => metric.Date == date)
            .OrderByDescending(metric => metric.SyncedAt)
            .FirstOrDefaultAsync();

        var salesMetric = await _context.DailySalesMetrics
            .Where(metric => metric.Date == date)
            .OrderByDescending(metric => metric.SyncedAt)
            .FirstOrDefaultAsync();

        var weatherMetric = await _context.DailyWeatherMetrics
            .Where(metric => metric.Date == date)
            .OrderByDescending(metric => metric.SyncedAt)
            .FirstOrDefaultAsync();

        return new DashboardMetricSummary
        {
            Date = date,
            RoundsPlayed = playMetric?.RoundsPlayed ?? manualMetric?.RoundsPlayed,
            CartRentals = playMetric?.CartRentals ?? manualMetric?.CartRentals,
            TotalRevenue = salesMetric?.TotalRevenue ?? manualMetric?.TotalRevenue,
            FoodAndBeverageRevenue = salesMetric?.FoodAndBeverageRevenue,
            ProShopRevenue = salesMetric?.ProShopRevenue,
            AlcoholRevenue = salesMetric?.AlcoholRevenue,
            RangeBallRevenue = salesMetric?.RangeBallRevenue,
            RainfallInches = weatherMetric?.RainfallInches,
            HighTemp = weatherMetric?.HighTemp,
            LowTemp = weatherMetric?.LowTemp,
            WeatherSummary = weatherMetric?.WeatherSummary ?? manualMetric?.WeatherSummary,
            PlaySourceSystemName = playMetric?.SourceSystemName,
            SalesSourceSystemName = salesMetric?.SourceSystemName,
            WeatherSourceSystemName = weatherMetric?.SourceSystemName
        };
    }

    public async Task<List<DashboardMetricSummary>> GetPreviousAvailableSummariesAsync(DateOnly date, int count)
    {
        var dates = new List<DateOnly>();

        dates.AddRange(await _context.DailyOperationMetrics
            .Where(metric => metric.Date < date)
            .Select(metric => metric.Date)
            .ToListAsync());

        dates.AddRange(await _context.DailyPlayMetrics
            .Where(metric => metric.Date < date)
            .Select(metric => metric.Date)
            .ToListAsync());

        dates.AddRange(await _context.DailySalesMetrics
            .Where(metric => metric.Date < date)
            .Select(metric => metric.Date)
            .ToListAsync());

        dates.AddRange(await _context.DailyWeatherMetrics
            .Where(metric => metric.Date < date)
            .Select(metric => metric.Date)
            .ToListAsync());

        var previousDates = dates
            .Distinct()
            .OrderByDescending(metricDate => metricDate)
            .Take(count)
            .ToList();

        var summaries = new List<DashboardMetricSummary>();

        foreach (var previousDate in previousDates)
        {
            summaries.Add(await GetDailyMetricSummaryAsync(previousDate));
        }

        return summaries;
    }
}
