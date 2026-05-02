using CourseCommander.Data;
using CourseCommander.DTOs;
using CourseCommander.Entities;
using Microsoft.EntityFrameworkCore;

namespace CourseCommander.Services;

public class InsightService
{
    private readonly AppDbContext _context;
    private readonly DashboardMetricService _dashboardMetricService;
    private readonly GrowingDegreeDayService _gddService;
    private readonly TrendService _trendService;
    private readonly FandBAnalyticsService _fandBAnalyticsService;
    private readonly AgronomyService _agronomyService;

    public InsightService(
        AppDbContext context,
        DashboardMetricService dashboardMetricService,
        GrowingDegreeDayService gddService,
        TrendService trendService,
        FandBAnalyticsService fandBAnalyticsService,
        AgronomyService agronomyService)
    {
        _context = context;
        _dashboardMetricService = dashboardMetricService;
        _gddService = gddService;
        _trendService = trendService;
        _fandBAnalyticsService = fandBAnalyticsService;
        _agronomyService = agronomyService;
    }

    public async Task<List<Insight>> GenerateDailyInsightsAsync(DateOnly date)
    {
        var metrics = await _dashboardMetricService.GetDailyMetricSummaryAsync(date);
        var previousDay = await _dashboardMetricService.GetDailyMetricSummaryAsync(date.AddDays(-1));
        var previousSummaries = await _dashboardMetricService.GetPreviousAvailableSummariesAsync(date, 7);
        var dailyGdd = await _gddService.GetDailyGddAsync(date.ToDateTime(TimeOnly.MinValue));
        var pastSevenDaysGdd = await _gddService.GetRangeGddAsync(
            date.AddDays(-7).ToDateTime(TimeOnly.MinValue),
            date.AddDays(-1).ToDateTime(TimeOnly.MinValue));
        var roundsTrend = await _trendService.GetRoundsTrendAsync(7, date);
        var revenueTrend = await _trendService.GetRevenueTrendAsync(7, date);
        var moistureSummary = await _agronomyService.GetMoistureSummaryAsync(date);

        var openMaintenanceTasks = await _context.MaintenanceTasks
            .CountAsync(task => task.Status != MaintenanceTaskStatus.Completed);

        var criticalEquipmentIssues = await _context.EquipmentIssues
            .CountAsync(issue =>
                issue.Severity == "Critical" &&
                issue.Status != EquipmentIssueStatus.Resolved);

        var insights = new List<Insight>
        {
            CreateInsight(date, "Daily Summary", "Info", BuildDailySummary(metrics))
        };

        var roundsChanged = false;
        var roundsDropped = false;
        var roundsIncreased = false;
        var difficultWeather = HasDifficultWeather(metrics);
        decimal? roundsChangePercent = null;
        decimal? revenueChangePercent = null;
        decimal? revenuePerRoundChange = null;
        var currentRevenuePerRound = GetRevenuePerRound(metrics);
        var previousRevenuePerRoundAverage = GetAverageRevenuePerRound(previousSummaries);

        if (metrics.RoundsPlayed is not null && previousDay.RoundsPlayed > 0)
        {
            roundsChangePercent = GetPercentChange(metrics.RoundsPlayed.Value, previousDay.RoundsPlayed.Value);
            roundsChanged = Math.Abs(roundsChangePercent.Value) > 20;
            roundsDropped = roundsChangePercent < -20;
            roundsIncreased = roundsChangePercent > 20;
        }

        if (metrics.TotalRevenue is not null && previousDay.TotalRevenue > 0)
        {
            revenueChangePercent = GetPercentChange(metrics.TotalRevenue.Value, previousDay.TotalRevenue.Value);
        }

        if (currentRevenuePerRound is not null && previousRevenuePerRoundAverage > 0)
        {
            revenuePerRoundChange = GetPercentChange(currentRevenuePerRound.Value, previousRevenuePerRoundAverage.Value);
        }

        AddBusinessPerformanceInsights(
            insights,
            date,
            roundsChangePercent,
            revenueChangePercent,
            revenuePerRoundChange,
            currentRevenuePerRound,
            roundsDropped,
            roundsIncreased,
            roundsChanged,
            difficultWeather);

        AddTrendInsights(insights, date, roundsTrend, revenueTrend);
        await AddFandBInsightsAsync(insights, date);

        if (metrics.RoundsPlayed > 0 && metrics.CartRentals is not null)
        {
            var cartUsagePercentage = (decimal)metrics.CartRentals.Value / metrics.RoundsPlayed.Value * 100;

            if (cartUsagePercentage < 50)
            {
                insights.Add(CreateInsight(
                    date,
                    "Cart Usage",
                    "Warning",
                    $"Warning: Cart usage was {cartUsagePercentage:0}%, below the expected 50% threshold."));
            }
        }

        AddGddInsights(insights, date, dailyGdd?.Gdd, pastSevenDaysGdd.AverageDailyGdd, metrics.RainfallInches);
        AddMoistureInsights(insights, date, moistureSummary, dailyGdd?.Gdd, metrics.RainfallInches);

        if (criticalEquipmentIssues > 0)
        {
            insights.Add(CreateInsight(
                date,
                "Critical Equipment",
                "Critical",
                $"Critical: {criticalEquipmentIssues} critical equipment issue(s) need immediate attention."));
        }

        if (openMaintenanceTasks >= 10)
        {
            insights.Add(CreateInsight(
                date,
                "Maintenance Backlog",
                "Warning",
                $"Warning: {openMaintenanceTasks} maintenance task(s) are open. Prioritize backlog review before the next busy tee sheet."));
        }

        if (!roundsChanged && insights.Count == 1 && metrics.HasAnyData)
        {
            insights.Add(CreateInsight(
                date,
                "Operations Stable",
                "Info",
                "Operations look steady compared with the available recent data."));
        }

        _context.Insights.AddRange(insights);
        await _context.SaveChangesAsync();

        return insights;
    }

    private static Insight CreateInsight(DateOnly date, string title, string category, string message)
    {
        return new Insight
        {
            Title = CleanLabel(title),
            Category = category,
            Message = CleanLabel(message),
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string BuildDailySummary(DashboardMetricSummary metrics)
    {
        if (!metrics.HasAnyData)
        {
            return "Daily summary: No play, sales, or weather data has been logged for this date.";
        }

        var parts = new List<string>();

        if (metrics.RoundsPlayed is not null)
        {
            parts.Add($"{metrics.RoundsPlayed} rounds");
        }

        if (metrics.CartRentals is not null)
        {
            parts.Add($"{metrics.CartRentals} cart rentals");
        }

        if (metrics.TotalRevenue is not null)
        {
            parts.Add($"{metrics.TotalRevenue:C} revenue");
        }

        if (!string.IsNullOrWhiteSpace(metrics.WeatherSummary))
        {
            parts.Add(metrics.WeatherSummary);
        }

        var message = $"Daily summary: {string.Join(", ", parts)}.";
        var sources = GetSourceSystemText(metrics);

        if (!string.IsNullOrWhiteSpace(sources))
        {
            message += $" Integrated data used from {sources}.";
        }

        return message;
    }

    private static string GetSourceSystemText(DashboardMetricSummary metrics)
    {
        var sources = new[]
        {
            metrics.PlaySourceSystemName,
            metrics.SalesSourceSystemName,
            metrics.WeatherSourceSystemName
        }
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .Distinct()
            .ToList();

        return string.Join(", ", sources);
    }

    private static void AddBusinessPerformanceInsights(
        List<Insight> insights,
        DateOnly date,
        decimal? roundsChangePercent,
        decimal? revenueChangePercent,
        decimal? revenuePerRoundChange,
        decimal? currentRevenuePerRound,
        bool roundsDropped,
        bool roundsIncreased,
        bool roundsChanged,
        bool difficultWeather)
    {
        var revenueDropped = revenueChangePercent < -20;

        if (roundsDropped && revenueDropped)
        {
            var message = $"Rounds dropped {Math.Abs(roundsChangePercent!.Value):0}% compared to yesterday";

            if (difficultWeather)
            {
                message += ", likely due to weather conditions (fog/rain)";
            }

            message += $". This reduction in play volume drove a {Math.Abs(revenueChangePercent!.Value):0}% decline in total revenue. Monitor tee sheet activity and consider adjusting pricing or promotions.";

            insights.Add(CreateInsight(date, "Revenue and Rounds Down", "Warning", message));
            return;
        }

        if (roundsDropped)
        {
            var message = $"Rounds dropped {Math.Abs(roundsChangePercent!.Value):0}% compared to yesterday.";

            if (difficultWeather)
            {
                message += " Weather conditions (fog/rain) likely reduced play volume.";
            }

            message += " Monitor tee sheet trends under similar weather conditions.";

            insights.Add(CreateInsight(date, "Rounds Down", "Warning", message));
        }
        else if (roundsIncreased)
        {
            var message = difficultWeather
                ? $"Rounds increased {roundsChangePercent!.Value:0}% despite unfavorable weather, suggesting strong demand or pre-booked tee sheet volume."
                : $"Rounds increased {roundsChangePercent!.Value:0}% compared to yesterday.";

            insights.Add(CreateInsight(date, "Rounds Up", "Info", message));
        }

        if (revenueDropped)
        {
            var message = $"Warning: Total revenue dropped {Math.Abs(revenueChangePercent!.Value):0}% compared to yesterday.";

            if (!roundsChanged)
            {
                message += " Lower revenue per round may indicate reduced spending in pro shop or F&B.";
            }

            message += " Review pricing, promotions, and F&B performance.";

            insights.Add(CreateInsight(date, "Revenue Down", "Warning", message));
        }

        AddRevenuePerRoundInsight(insights, date, roundsDropped, revenuePerRoundChange, currentRevenuePerRound);
    }

    private static void AddRevenuePerRoundInsight(
        List<Insight> insights,
        DateOnly date,
        bool roundsDropped,
        decimal? revenuePerRoundChange,
        decimal? currentRevenuePerRound)
    {
        if (revenuePerRoundChange <= 15 || currentRevenuePerRound is null)
        {
            return;
        }

        var message = roundsDropped
            ? $"Higher revenue per round suggests stronger spending despite lower play volume. Revenue per round was {currentRevenuePerRound:C}."
            : $"Revenue per round was {currentRevenuePerRound:C}, which is {revenuePerRoundChange:0}% above the recent 7-day average.";

        insights.Add(CreateInsight(date, "Spend Per Round", "Info", message));
    }

    private async Task AddFandBInsightsAsync(List<Insight> insights, DateOnly date)
    {
        var suggestions = await _fandBAnalyticsService.GetInsightSuggestionsAsync(date);

        foreach (var suggestion in suggestions)
        {
            insights.Add(CreateInsight(date, suggestion.Title, suggestion.Category, suggestion.Message));
        }
    }

    private void AddTrendInsights(
        List<Insight> insights,
        DateOnly date,
        List<TrendPointDto> roundsTrend,
        List<TrendPointDto> revenueTrend)
    {
        var roundsDirection = _trendService.GetConsecutiveTrend(roundsTrend);
        var revenueDirection = _trendService.GetConsecutiveTrend(revenueTrend);

        if (roundsDirection is not null)
        {
            var periodText = GetTrendPeriodText(roundsDirection.ConsecutiveChanges);
            var message = roundsDirection.Direction == "Up"
                ? $"Rounds have increased consistently over {periodText}."
                : $"Rounds have declined consistently over {periodText}.";
            var category = roundsDirection.Direction == "Up" ? "Info" : "Warning";

            insights.Add(CreateInsight(date, "Rounds Trend", category, message));
        }

        if (revenueDirection is not null)
        {
            var periodText = GetTrendPeriodText(revenueDirection.ConsecutiveChanges);
            var message = revenueDirection.Direction == "Up"
                ? $"Revenue has been trending upward over {periodText}."
                : $"Revenue has declined consistently over {periodText}.";
            var category = revenueDirection.Direction == "Up" ? "Info" : "Warning";

            insights.Add(CreateInsight(date, "Revenue Trend", category, message));
        }
    }

    private static string GetTrendPeriodText(int consecutiveChanges)
    {
        var days = consecutiveChanges + 1;
        return days >= 7 ? "the past week" : $"the past {days} days";
    }

    private static void AddGddInsights(
        List<Insight> insights,
        DateOnly date,
        decimal? dailyGdd,
        decimal pastSevenDayAverage,
        decimal? rainfallInches)
    {
        if (dailyGdd is null)
        {
            return;
        }

        var classification = GetGddClassification(dailyGdd.Value);
        var messageParts = new List<string>
        {
            $"Today's GDD was {dailyGdd:0.#} ({classification})."
        };
        var category = "Info";
        var trendDescription = GetGddTrendDescription(dailyGdd.Value, pastSevenDayAverage);
        var isIncreasingTrend = trendDescription?.Contains("increasing", StringComparison.OrdinalIgnoreCase) == true;

        if (rainfallInches > 0 && dailyGdd > 5)
        {
            messageParts.Add($"Rainfall of {rainfallInches:0.##} inches and {GetGddLevelLabel(dailyGdd.Value)} GDD support turf growth.");
        }

        if (dailyGdd > 25)
        {
            category = "Warning";
            messageParts.Add("Monitor turf stress and disease risk.");
        }
        else if (dailyGdd < 5)
        {
            messageParts.Add("Minimal turf growth expected. Maintenance demand is reduced.");
        }

        if (!string.IsNullOrWhiteSpace(trendDescription) && !(dailyGdd > 15 && isIncreasingTrend))
        {
            messageParts.Add(trendDescription);
        }

        if (dailyGdd > 15)
        {
            var gddLabel = dailyGdd > 25 ? "Extreme GDD" : "High GDD";
            var growthMessage = isIncreasingTrend
                ? $"{gddLabel} ({dailyGdd:0.#}) indicates increasing turf growth pressure."
                : $"{gddLabel} ({dailyGdd:0.#}) indicates elevated turf growth pressure.";

            messageParts.Add(growthMessage);
            messageParts.Add("Increased mowing frequency and maintenance planning may be required.");
        }

        if (rainfallInches > 0.10m && dailyGdd > 15)
        {
            category = "Warning";
            messageParts.Add("Warm and wet conditions may increase disease pressure. Monitor turf closely.");
        }

        insights.Add(CreateInsight(
            date,
            "Turf Conditions",
            category,
            string.Join(" ", messageParts)));
    }

    private static void AddMoistureInsights(
        List<Insight> insights,
        DateOnly date,
        AgronomySummaryDto moistureSummary,
        decimal? dailyGdd,
        decimal? rainfallInches)
    {
        if (moistureSummary.LowestMoistureReading is null && moistureSummary.HighestMoistureReading is null)
        {
            return;
        }

        if (moistureSummary.LowestMoistureReading is < 15)
        {
            var driestLocation = moistureSummary.TopDriestLocations.FirstOrDefault();
            var locationText = driestLocation is null
                ? "one monitored area"
                : $"{driestLocation.Location} {driestLocation.Zone}".Trim();

            insights.Add(CreateInsight(
                date,
                "Low Moisture",
                "Warning",
                $"Moisture dropped to {moistureSummary.LowestMoistureReading:0.#}% at {locationText}. Review hand-watering needs and turf stress risk."));
        }

        if (moistureSummary.HighestMoistureReading is > 30)
        {
            insights.Add(CreateInsight(
                date,
                "High Moisture",
                "Warning",
                $"Moisture reached {moistureSummary.HighestMoistureReading:0.#}%, above the 30% threshold. Check drainage and firmness conditions."));
        }

        if (dailyGdd > 15 && moistureSummary.LowestMoistureReading is < 15)
        {
            insights.Add(CreateInsight(
                date,
                "Turf Stress Risk",
                "Warning",
                $"High GDD and low moisture indicate turf stress risk. Prioritize moisture checks on the driest greens."));
        }

        if (rainfallInches > 0.10m && moistureSummary.HighestMoistureReading is > 30)
        {
            insights.Add(CreateInsight(
                date,
                "Drainage Risk",
                "Warning",
                $"Rainfall and high moisture readings may increase disease or drainage risk. Monitor wet areas and airflow."));
        }
    }

    private static string GetGddClassification(decimal gdd)
    {
        return gdd switch
        {
            < 5 => "Low Growth",
            <= 15 => "Moderate Growth",
            <= 25 => "High Growth",
            _ => "Extreme Growth"
        };
    }

    private static string GetGddLevelLabel(decimal gdd)
    {
        return gdd switch
        {
            < 5 => "low",
            <= 15 => "moderate",
            <= 25 => "high",
            _ => "extreme"
        };
    }

    private static string? GetGddTrendDescription(decimal dailyGdd, decimal pastSevenDayAverage)
    {
        if (pastSevenDayAverage <= 0)
        {
            return null;
        }

        var gddChangePercent = GetPercentChange(dailyGdd, pastSevenDayAverage);

        if (dailyGdd > 5 && gddChangePercent > 20)
        {
            return "Growth pressure is increasing compared to recent days.";
        }

        if (gddChangePercent < -20)
        {
            return "Growth pressure remains present but is lower than recent days.";
        }

        return "Growth pressure is consistent with recent conditions.";
    }

    private static decimal? GetRevenuePerRound(DashboardMetricSummary metrics)
    {
        if (metrics.RoundsPlayed > 0 && metrics.TotalRevenue is not null)
        {
            return metrics.TotalRevenue.Value / metrics.RoundsPlayed.Value;
        }

        return null;
    }

    private static decimal? GetAverageRevenuePerRound(List<DashboardMetricSummary> summaries)
    {
        var revenuePerRoundValues = summaries
            .Select(GetRevenuePerRound)
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToList();

        if (revenuePerRoundValues.Count == 0)
        {
            return null;
        }

        return revenuePerRoundValues.Average();
    }

    private static decimal GetPercentChange(decimal current, decimal previous)
    {
        return (current - previous) / previous * 100;
    }

    private static decimal GetPercentChange(int current, int previous)
    {
        return (decimal)(current - previous) / previous * 100;
    }

    private static bool IsRainy(DashboardMetricSummary metrics)
    {
        var summaryIncludesRain = metrics.WeatherSummary?.Contains("rain", StringComparison.OrdinalIgnoreCase) == true;
        var rainfallRecorded = metrics.RainfallInches > 0.10m;

        return summaryIncludesRain || rainfallRecorded;
    }

    private static bool HasDifficultWeather(DashboardMetricSummary metrics)
    {
        var summary = metrics.WeatherSummary ?? string.Empty;

        return summary.Contains("fog", StringComparison.OrdinalIgnoreCase) ||
            summary.Contains("rain", StringComparison.OrdinalIgnoreCase) ||
            summary.Contains("precipitation", StringComparison.OrdinalIgnoreCase) ||
            metrics.RainfallInches > 0.10m;
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
