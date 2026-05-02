using CourseCommander.Data;
using CourseCommander.DTOs;
using CourseCommander.Entities;
using Microsoft.EntityFrameworkCore;

namespace CourseCommander.Services;

public class PriorityService
{
    private readonly AppDbContext _context;
    private readonly DashboardMetricService _dashboardMetricService;
    private readonly GrowingDegreeDayService _gddService;

    public PriorityService(
        AppDbContext context,
        DashboardMetricService dashboardMetricService,
        GrowingDegreeDayService gddService)
    {
        _context = context;
        _dashboardMetricService = dashboardMetricService;
        _gddService = gddService;
    }

    public async Task<List<PriorityActionDto>> GetPrioritiesAsync(
        DateOnly date,
        List<AlertDto>? alerts = null,
        List<Insight>? insights = null,
        int limit = 5)
    {
        var priorities = new List<PriorityActionDto>();
        var metrics = await _dashboardMetricService.GetDailyMetricSummaryAsync(date);
        var previousSummaries = await _dashboardMetricService.GetPreviousAvailableSummariesAsync(date, 7);
        var dailyGdd = await _gddService.GetDailyGddAsync(date.ToDateTime(TimeOnly.MinValue));

        await AddEquipmentPrioritiesAsync(priorities);
        await AddMaintenancePrioritiesAsync(priorities);
        AddWeatherPriorities(priorities, metrics);
        AddOperationsPriorities(priorities, metrics, previousSummaries);
        AddTurfPriorities(priorities, dailyGdd?.Gdd);
        AddAlertPriorities(priorities, alerts ?? new List<AlertDto>());
        AddInsightPriorities(priorities, insights ?? new List<Insight>());

        return priorities
            .GroupBy(priority => priority.Title)
            .Select(group => group.OrderByDescending(priority => priority.PriorityScore).First())
            .OrderByDescending(priority => priority.PriorityScore)
            .ThenBy(priority => priority.Title)
            .Take(limit)
            .ToList();
    }

    private async Task AddEquipmentPrioritiesAsync(List<PriorityActionDto> priorities)
    {
        var criticalIssues = await _context.EquipmentIssues
            .Where(issue =>
                issue.Severity == "Critical" &&
                issue.Status != EquipmentIssueStatus.Resolved)
            .OrderByDescending(issue => issue.ReportedAt)
            .Take(5)
            .ToListAsync();

        foreach (var issue in criticalIssues)
        {
            priorities.Add(new PriorityActionDto
            {
                Title = $"Repair {CleanLabel(issue.EquipmentName)}",
                Description = string.IsNullOrWhiteSpace(issue.IssueDescription)
                    ? "Critical equipment issue needs immediate ownership."
                    : CleanLabel(issue.IssueDescription),
                PriorityScore = 100,
                Category = "Equipment"
            });
        }
    }

    private async Task AddMaintenancePrioritiesAsync(List<PriorityActionDto> priorities)
    {
        var openTasks = await _context.MaintenanceTasks
            .Where(task => task.Status != MaintenanceTaskStatus.Completed)
            .ToListAsync();

        if (openTasks.Count >= 10)
        {
            priorities.Add(new PriorityActionDto
            {
                Title = "Reduce maintenance backlog",
                Description = $"{openTasks.Count} maintenance tasks are open. Assign crews to the highest-impact work first.",
                PriorityScore = 50,
                Category = "Maintenance"
            });
        }

        foreach (var task in openTasks
            .OrderBy(task => GetPriorityRank(task.Priority))
            .ThenBy(task => task.CreatedAt)
            .Take(5))
        {
            priorities.Add(new PriorityActionDto
            {
                Title = CleanLabel(task.Title),
                Description = $"{CleanLabel(task.Category)}: {CleanLabel(task.Description)}",
                PriorityScore = GetMaintenanceScore(task),
                Category = "Maintenance"
            });
        }
    }

    private static void AddWeatherPriorities(List<PriorityActionDto> priorities, DashboardMetricSummary metrics)
    {
        if (metrics.RainfallInches > 0.25m)
        {
            priorities.Add(new PriorityActionDto
            {
                Title = "Inspect drainage and course conditions",
                Description = $"Rainfall is {metrics.RainfallInches:0.##} inches. Check drainage, cart path restrictions, and wet areas before play.",
                PriorityScore = 70,
                Category = "Weather"
            });
        }
    }

    private static void AddOperationsPriorities(
        List<PriorityActionDto> priorities,
        DashboardMetricSummary metrics,
        List<DashboardMetricSummary> previousSummaries)
    {
        var roundsAverage = GetAverage(previousSummaries.Select(summary => (decimal?)summary.RoundsPlayed));
        var revenueAverage = GetAverage(previousSummaries.Select(summary => summary.TotalRevenue));

        if (metrics.RoundsPlayed is not null && roundsAverage is > 0)
        {
            var roundsDrop = GetDropPercent(metrics.RoundsPlayed.Value, roundsAverage.Value);

            if (roundsDrop > 25)
            {
                priorities.Add(new PriorityActionDto
                {
                    Title = "Review tee sheet demand",
                    Description = $"Rounds are {roundsDrop:0}% below the recent 7-day average.",
                    PriorityScore = 65,
                    Category = "Operations"
                });
            }
        }

        if (metrics.TotalRevenue is not null && revenueAverage is > 0)
        {
            var revenueDrop = GetDropPercent(metrics.TotalRevenue.Value, revenueAverage.Value);

            if (revenueDrop > 25)
            {
                priorities.Add(new PriorityActionDto
                {
                    Title = "Review revenue performance",
                    Description = $"Revenue is {revenueDrop:0}% below the recent 7-day average. Check play volume, pricing, pro shop, and F&B activity.",
                    PriorityScore = 65,
                    Category = "Operations"
                });
            }
        }
    }

    private static void AddTurfPriorities(List<PriorityActionDto> priorities, decimal? dailyGdd)
    {
        if (dailyGdd > 20)
        {
            priorities.Add(new PriorityActionDto
            {
                Title = "Adjust mowing and turf labor plan",
                Description = $"GDD is {dailyGdd:0.#}, indicating high turf growth pressure.",
                PriorityScore = 45,
                Category = "Turf"
            });
        }
    }

    private static void AddAlertPriorities(List<PriorityActionDto> priorities, List<AlertDto> alerts)
    {
        foreach (var alert in alerts)
        {
            var score = GetAlertScore(alert);

            if (score <= 0)
            {
                continue;
            }

            priorities.Add(new PriorityActionDto
            {
                Title = CleanLabel(alert.Title),
                Description = CleanLabel(alert.RecommendedAction),
                PriorityScore = score,
                Category = alert.Category
            });
        }
    }

    private static void AddInsightPriorities(List<PriorityActionDto> priorities, List<Insight> insights)
    {
        foreach (var insight in insights.Where(insight => insight.Category is "Warning" or "Critical").Take(3))
        {
            priorities.Add(new PriorityActionDto
            {
                Title = CleanLabel(insight.Title),
                Description = CleanLabel(insight.Message),
                PriorityScore = insight.Category == "Critical" ? 90 : 20,
                Category = GetPriorityCategoryFromInsight(insight.Title)
            });
        }
    }

    private static int GetMaintenanceScore(MaintenanceTask task)
    {
        if (IsSafetyOrPlayBlocking(task))
        {
            return 90;
        }

        return task.Priority switch
        {
            "Critical" => 90,
            "High" => 65,
            "Medium" => 50,
            "Low" => 20,
            _ => 20
        };
    }

    private static bool IsSafetyOrPlayBlocking(MaintenanceTask task)
    {
        var text = $"{task.Title} {task.Description}".ToLowerInvariant();

        return text.Contains("safety") ||
            text.Contains("hazard") ||
            text.Contains("closed") ||
            text.Contains("washout") ||
            text.Contains("blocked") ||
            text.Contains("leak");
    }

    private static int GetAlertScore(AlertDto alert)
    {
        if (alert.Severity == "Critical")
        {
            return 100;
        }

        if (alert.Category == "Weather" && alert.Title.Contains("Rainfall", StringComparison.OrdinalIgnoreCase))
        {
            return 70;
        }

        if (alert.Category == "Operations")
        {
            return 65;
        }

        if (alert.Category == "Maintenance")
        {
            return 50;
        }

        if (alert.Category == "Turf")
        {
            return 45;
        }

        return alert.Severity == "Warning" ? 20 : 0;
    }

    private static string GetPriorityCategoryFromInsight(string title)
    {
        if (title.Contains("Equipment", StringComparison.OrdinalIgnoreCase))
        {
            return "Equipment";
        }

        if (title.Contains("Maintenance", StringComparison.OrdinalIgnoreCase))
        {
            return "Maintenance";
        }

        if (title.Contains("Turf", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("GDD", StringComparison.OrdinalIgnoreCase))
        {
            return "Turf";
        }

        if (title.Contains("Weather", StringComparison.OrdinalIgnoreCase))
        {
            return "Weather";
        }

        return "Operations";
    }

    private static int GetPriorityRank(string priority)
    {
        return priority switch
        {
            "Critical" => 0,
            "High" => 1,
            "Medium" => 2,
            "Low" => 3,
            _ => 4
        };
    }

    private static decimal? GetAverage(IEnumerable<decimal?> values)
    {
        var availableValues = values
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToList();

        return availableValues.Count == 0 ? null : availableValues.Average();
    }

    private static decimal GetDropPercent(decimal currentValue, decimal averageValue)
    {
        return (averageValue - currentValue) / averageValue * 100;
    }

    private static string CleanLabel(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        return input
            .Replace("[Demo]", "")
            .Replace("Mock ", "")
            .Replace("mock ", "")
            .Replace("Placeholder ", "")
            .Replace("placeholder ", "")
            .Trim();
    }
}
