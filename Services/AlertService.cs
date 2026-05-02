using CourseCommander.Data;
using CourseCommander.DTOs;
using CourseCommander.Entities;
using Microsoft.EntityFrameworkCore;

namespace CourseCommander.Services;

public class AlertService
{
    private readonly AppDbContext _context;
    private readonly DashboardMetricService _dashboardMetricService;
    private readonly GrowingDegreeDayService _gddService;
    private readonly AdminService _adminService;

    public AlertService(
        AppDbContext context,
        DashboardMetricService dashboardMetricService,
        GrowingDegreeDayService gddService,
        AdminService adminService)
    {
        _context = context;
        _dashboardMetricService = dashboardMetricService;
        _gddService = gddService;
        _adminService = adminService;
    }

    public async Task<List<AlertDto>> GenerateAlertsAsync(
        DateOnly date,
        int openMaintenanceTaskCount,
        int criticalEquipmentIssueCount)
    {
        var alerts = new List<AlertDto>();
        var metrics = await _dashboardMetricService.GetDailyMetricSummaryAsync(date);
        var previousSummaries = await _dashboardMetricService.GetPreviousAvailableSummariesAsync(date, 7);
        var dailyGdd = await _gddService.GetDailyGddAsync(date.ToDateTime(TimeOnly.MinValue));

        await AddEquipmentAlertsAsync(alerts, criticalEquipmentIssueCount);
        await AddMaintenanceAlertsAsync(alerts, openMaintenanceTaskCount);
        AddOperationsAlerts(alerts, metrics, previousSummaries);
        AddWeatherAndTurfAlerts(alerts, metrics.RainfallInches, dailyGdd?.Gdd);
        await AddPayrollAlertsAsync(alerts, date);

        return alerts;
    }

    private async Task AddEquipmentAlertsAsync(List<AlertDto> alerts, int criticalEquipmentIssueCount)
    {
        if (criticalEquipmentIssueCount <= 0)
        {
            return;
        }

        var criticalIssues = await _context.EquipmentIssues
            .Where(issue =>
                issue.Severity == "Critical" &&
                issue.Status != EquipmentIssueStatus.Resolved)
            .OrderByDescending(issue => issue.ReportedAt)
            .Take(5)
            .ToListAsync();
        var relatedItems = criticalIssues
            .Select(BuildEquipmentIssueItem)
            .ToList();

        alerts.Add(CreateAlert(
            "Critical Equipment Issue",
            $"{criticalEquipmentIssueCount} critical equipment issue(s) are currently open.",
            "Critical",
            "Equipment",
            "Assign repair ownership for the listed critical equipment issues before peak operations.",
            relatedItems));
    }

    private async Task AddMaintenanceAlertsAsync(List<AlertDto> alerts, int openMaintenanceTaskCount)
    {
        if (openMaintenanceTaskCount < 10)
        {
            return;
        }

        var openTasks = await _context.MaintenanceTasks
            .Where(task => task.Status != MaintenanceTaskStatus.Completed)
            .ToListAsync();
        var relatedItems = openTasks
            .OrderBy(task => GetPriorityRank(task.Priority))
            .ThenBy(task => task.CreatedAt)
            .Take(5)
            .Select(task => $"{CleanLabel(task.Title)} — {CleanLabel(task.Category)}")
            .ToList();

        alerts.Add(CreateAlert(
            "Maintenance Backlog",
            $"{openMaintenanceTaskCount} maintenance task(s) are open.",
            "Warning",
            "Maintenance",
            "Prioritize the listed maintenance tasks and assign crews to reduce the backlog.",
            relatedItems));
    }

    private static void AddOperationsAlerts(
        List<AlertDto> alerts,
        DashboardMetricSummary metrics,
        List<DashboardMetricSummary> previousSummaries)
    {
        var roundsAverage = GetAverage(previousSummaries.Select(summary => (decimal?)summary.RoundsPlayed));
        var revenueAverage = GetAverage(previousSummaries.Select(summary => summary.TotalRevenue));
        var currentRevenuePerRound = GetRevenuePerRound(metrics);
        var revenuePerRoundAverage = GetAverage(previousSummaries.Select(GetRevenuePerRound));

        if (metrics.RoundsPlayed is not null && roundsAverage is > 0)
        {
            var roundsDropPercent = GetDropPercent(metrics.RoundsPlayed.Value, roundsAverage.Value);

            if (roundsDropPercent > 25)
            {
                alerts.Add(CreateAlert(
                    "Rounds Below Average",
                    $"Rounds are {roundsDropPercent:0}% below the recent 7-day average.",
                    "Warning",
                    "Operations",
                    "Review tee sheet demand, weather impacts, and staffing needs for the day."));
            }
        }

        if (metrics.TotalRevenue is not null && revenueAverage is > 0)
        {
            var revenueDropPercent = GetDropPercent(metrics.TotalRevenue.Value, revenueAverage.Value);

            if (revenueDropPercent > 25)
            {
                alerts.Add(CreateAlert(
                    "Revenue Below Average",
                    $"Revenue is {revenueDropPercent:0}% below the recent 7-day average.",
                    "Warning",
                    "Operations",
                    "Review play volume, pricing, pro shop activity, and F&B performance."));
            }
        }

        if (currentRevenuePerRound is not null && revenuePerRoundAverage is > 0)
        {
            var revenuePerRoundDropPercent = GetDropPercent(currentRevenuePerRound.Value, revenuePerRoundAverage.Value);

            if (revenuePerRoundDropPercent > 20)
            {
                alerts.Add(CreateAlert(
                    "Spend Per Round Below Average",
                    $"Revenue per round is {revenuePerRoundDropPercent:0}% below the recent 7-day average.",
                    "Warning",
                    "Operations",
                    "Review pro shop and F&B conversion opportunities during check-in and turn traffic."));
            }
        }

        if (metrics.RoundsPlayed > 0 && metrics.CartRentals is not null)
        {
            var cartUsagePercent = (decimal)metrics.CartRentals.Value / metrics.RoundsPlayed.Value * 100;

            if (cartUsagePercent < 50)
            {
                alerts.Add(CreateAlert(
                    "Low Cart Usage",
                    $"Cart usage is {cartUsagePercent:0}% for this date.",
                    "Info",
                    "Operations",
                    "Review walking demand, cart availability, and cart staging for upcoming tee times."));
            }
        }
    }

    private static void AddWeatherAndTurfAlerts(List<AlertDto> alerts, decimal? rainfallInches, decimal? dailyGdd)
    {
        if (rainfallInches > 0.25m)
        {
            alerts.Add(CreateAlert(
                "Heavy Rainfall",
                $"Rainfall is {rainfallInches:0.##} inches for this date.",
                "Warning",
                "Weather",
                "Inspect drainage areas, cart path restrictions, and course conditions before play."));
        }

        if (dailyGdd > 20)
        {
            alerts.Add(CreateAlert(
                "High Turf Growth Pressure",
                "GDD is above 20, indicating increased turf growth pressure.",
                "Warning",
                "Turf",
                "Review mowing frequency and labor allocation."));
        }

        if (rainfallInches > 0.10m && dailyGdd > 15)
        {
            alerts.Add(CreateAlert(
                "Disease Pressure Risk",
                "Rainfall and elevated GDD may increase turf disease pressure.",
                "Warning",
                "Turf",
                "Monitor greens and high-stress turf areas for disease symptoms."));
        }
    }

    private async Task AddPayrollAlertsAsync(List<AlertDto> alerts, DateOnly date)
    {
        var payrollSummary = await _adminService.GetCurrentPayrollPeriodAsync(date);

        if (payrollSummary.Period.Status != "Open")
        {
            return;
        }

        if (payrollSummary.IsOverdue)
        {
            alerts.Add(CreateAlert(
                "Payroll Overdue",
                $"Payroll for {payrollSummary.Period.PeriodStartDate:MMM d} - {payrollSummary.Period.PeriodEndDate:MMM d} is overdue.",
                "Critical",
                "Operations",
                "Submit payroll immediately and confirm staff hours are complete."));
            return;
        }

        if (payrollSummary.IsDueToday)
        {
            alerts.Add(CreateAlert(
                "Payroll Due Today",
                "Payroll is due today.",
                "Critical",
                "Operations",
                "Review timecards and submit payroll before the end of the day."));
            return;
        }

        if (payrollSummary.DaysUntilDue <= 3)
        {
            alerts.Add(CreateAlert(
                "Payroll Deadline Approaching",
                $"Payroll is due in {payrollSummary.DaysUntilDue} day(s).",
                "Warning",
                "Operations",
                "Review timecards and resolve missing punches before payroll is due."));
        }
    }

    private static AlertDto CreateAlert(
        string title,
        string message,
        string severity,
        string category,
        string recommendedAction,
        List<string>? relatedItems = null)
    {
        return new AlertDto
        {
            Title = CleanLabel(title),
            Message = CleanLabel(message),
            Severity = severity,
            Category = category,
            RecommendedAction = CleanLabel(recommendedAction),
            RelatedItems = relatedItems?.Select(CleanLabel).ToList() ?? new List<string>()
        };
    }

    private static string BuildEquipmentIssueItem(EquipmentIssue issue)
    {
        if (string.IsNullOrWhiteSpace(issue.IssueDescription))
        {
            return CleanLabel(issue.EquipmentName);
        }

        return $"{CleanLabel(issue.EquipmentName)} — {CleanLabel(issue.IssueDescription)}";
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

    private static decimal? GetRevenuePerRound(DashboardMetricSummary metrics)
    {
        if (metrics.RoundsPlayed > 0 && metrics.TotalRevenue is not null)
        {
            return metrics.TotalRevenue.Value / metrics.RoundsPlayed.Value;
        }

        return null;
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
