using CourseCommander.Data;
using CourseCommander.DTOs;
using CourseCommander.Entities;
using CourseCommander.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseCommander.Controllers;

[ApiController]
[Route("api/analytics")]
public class AnalyticsController : ControllerBase
{
    private static readonly Dictionary<string, string> SupportedMetrics = new(StringComparer.OrdinalIgnoreCase)
    {
        ["rounds"] = "rounds",
        ["cartRentals"] = "cartRentals",
        ["totalRevenue"] = "totalRevenue",
        ["proShopRevenue"] = "proShopRevenue",
        ["foodAndBeverageRevenue"] = "foodAndBeverageRevenue",
        ["alcoholRevenue"] = "alcoholRevenue",
        ["rangeBallRevenue"] = "rangeBallRevenue",
        ["highTemp"] = "highTemp",
        ["lowTemp"] = "lowTemp",
        ["rainfall"] = "rainfall",
        ["gdd"] = "gdd",
        ["averageMoisture"] = "averageMoisture",
        ["openMaintenanceTasks"] = "openMaintenanceTasks",
        ["completedMaintenanceTasks"] = "completedMaintenanceTasks",
        ["equipmentIssues"] = "equipmentIssues"
    };

    private readonly AppDbContext _context;
    private readonly DashboardMetricService _dashboardMetricService;
    private readonly GrowingDegreeDayService _gddService;
    private readonly AgronomyService _agronomyService;

    public AnalyticsController(
        AppDbContext context,
        DashboardMetricService dashboardMetricService,
        GrowingDegreeDayService gddService,
        AgronomyService agronomyService)
    {
        _context = context;
        _dashboardMetricService = dashboardMetricService;
        _gddService = gddService;
        _agronomyService = agronomyService;
    }

    [HttpGet("compare")]
    public async Task<ActionResult<List<AnalyticsComparePointDto>>> CompareMetrics(
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] string? metrics)
    {
        if (startDate is null || endDate is null)
        {
            return BadRequest("startDate and endDate are required.");
        }

        if (startDate > endDate)
        {
            return BadRequest("startDate must be before or equal to endDate.");
        }

        var selectedMetrics = ParseMetrics(metrics);

        if (selectedMetrics.Count == 0)
        {
            return BadRequest("Please request at least one supported metric.");
        }

        var results = new List<AnalyticsComparePointDto>();

        for (var date = startDate.Value; date <= endDate.Value; date = date.AddDays(1))
        {
            var metricSummary = await _dashboardMetricService.GetDailyMetricSummaryAsync(date);
            var gdd = await _gddService.GetDailyGddAsync(date.ToDateTime(TimeOnly.MinValue));
            var moistureSummary = await _agronomyService.GetMoistureSummaryAsync(date);
            var values = new Dictionary<string, decimal?>();

            foreach (var metric in selectedMetrics)
            {
                values[metric] = metric switch
                {
                    "rounds" => ToDecimal(metricSummary.RoundsPlayed),
                    "cartRentals" => ToDecimal(metricSummary.CartRentals),
                    "totalRevenue" => metricSummary.TotalRevenue,
                    "proShopRevenue" => metricSummary.ProShopRevenue,
                    "foodAndBeverageRevenue" => metricSummary.FoodAndBeverageRevenue,
                    "alcoholRevenue" => metricSummary.AlcoholRevenue,
                    "rangeBallRevenue" => metricSummary.RangeBallRevenue,
                    "highTemp" => metricSummary.HighTemp,
                    "lowTemp" => metricSummary.LowTemp,
                    "rainfall" => metricSummary.RainfallInches,
                    "gdd" => gdd?.Gdd,
                    "averageMoisture" => moistureSummary.AverageMoistureToday,
                    "openMaintenanceTasks" => await GetOpenMaintenanceTaskCountAsync(date),
                    "completedMaintenanceTasks" => await GetCompletedMaintenanceTaskCountAsync(date),
                    "equipmentIssues" => await GetActiveEquipmentIssueCountAsync(date),
                    _ => null
                };
            }

            results.Add(new AnalyticsComparePointDto
            {
                Date = date,
                Values = values
            });
        }

        return Ok(results);
    }

    private static List<string> ParseMetrics(string? metrics)
    {
        if (string.IsNullOrWhiteSpace(metrics))
        {
            return new List<string>();
        }

        return metrics
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(metric => SupportedMetrics.ContainsKey(metric))
            .Select(metric => SupportedMetrics[metric])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static decimal? ToDecimal(int? value)
    {
        return value is null ? null : value.Value;
    }

    private async Task<int> GetOpenMaintenanceTaskCountAsync(DateOnly date)
    {
        var dayEnd = date.ToDateTime(TimeOnly.MaxValue);

        return await _context.MaintenanceTasks
            .CountAsync(task =>
                task.CreatedAt <= dayEnd &&
                task.Status != MaintenanceTaskStatus.Completed &&
                (task.CompletedAt == null || task.CompletedAt > dayEnd));
    }

    private async Task<int> GetCompletedMaintenanceTaskCountAsync(DateOnly date)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = date.ToDateTime(TimeOnly.MaxValue);

        return await _context.MaintenanceTasks
            .CountAsync(task =>
                task.CompletedAt >= dayStart &&
                task.CompletedAt <= dayEnd);
    }

    private async Task<int> GetActiveEquipmentIssueCountAsync(DateOnly date)
    {
        var dayEnd = date.ToDateTime(TimeOnly.MaxValue);

        return await _context.EquipmentIssues
            .CountAsync(issue =>
                issue.ReportedAt <= dayEnd &&
                issue.Status != EquipmentIssueStatus.Resolved &&
                (issue.CompletedAt == null || issue.CompletedAt > dayEnd));
    }
}
