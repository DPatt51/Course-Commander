using CourseCommander.DTOs;

namespace CourseCommander.Services;

public class DailyBriefingService
{
    private readonly DashboardMetricService _dashboardMetricService;
    private readonly GrowingDegreeDayService _gddService;
    private readonly TrendService _trendService;
    private readonly ForecastService _forecastService;
    private readonly PriorityService _priorityService;

    public DailyBriefingService(
        DashboardMetricService dashboardMetricService,
        GrowingDegreeDayService gddService,
        TrendService trendService,
        ForecastService forecastService,
        PriorityService priorityService)
    {
        _dashboardMetricService = dashboardMetricService;
        _gddService = gddService;
        _trendService = trendService;
        _forecastService = forecastService;
        _priorityService = priorityService;
    }

    public async Task<DailyBriefingDto> GenerateDailyBriefingAsync(
        DateOnly date,
        int openMaintenanceTaskCount,
        int criticalEquipmentIssueCount)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        if (date == today)
        {
            return new DailyBriefingDto
            {
                BriefingMode = "TodayOutlook",
                DailyBriefing = CleanLabel(await BuildTodayOutlookAsync(date, openMaintenanceTaskCount, criticalEquipmentIssueCount)),
                YesterdayRecap = CleanLabel(await BuildYesterdayRecapAsync(date.AddDays(-1)))
            };
        }

        return new DailyBriefingDto
        {
            BriefingMode = "HistoricalRecap",
            DailyBriefing = CleanLabel(await BuildHistoricalBriefingAsync(date))
        };
    }

    private async Task<string> BuildTodayOutlookAsync(
        DateOnly date,
        int openMaintenanceTaskCount,
        int criticalEquipmentIssueCount)
    {
        var metrics = await _dashboardMetricService.GetDailyMetricSummaryAsync(date);
        var dailyGdd = await _gddService.GetDailyGddAsync(date.ToDateTime(TimeOnly.MinValue));
        var forecast = await _forecastService.GetForecastAsync(date.AddDays(1));
        var priorities = await _priorityService.GetPrioritiesAsync(date, limit: 3);
        var sentences = new List<string>
        {
            BuildTodayConditionsSentence(metrics, dailyGdd?.Gdd),
            BuildTodayPrioritySentence(metrics, dailyGdd?.Gdd, openMaintenanceTaskCount, criticalEquipmentIssueCount),
            BuildTodayStatusSentence(openMaintenanceTaskCount, criticalEquipmentIssueCount),
            forecast.Summary
        };

        var priorityText = BuildTopPrioritiesText(priorities);

        if (!string.IsNullOrWhiteSpace(priorityText))
        {
            sentences.Add(priorityText);
        }

        return string.Join(" ", sentences);
    }

    private static string? BuildTopPrioritiesText(List<PriorityActionDto> priorities)
    {
        if (priorities.Count == 0)
        {
            return null;
        }

        var items = priorities
            .Take(3)
            .Select(priority => $"- {CleanLabel(priority.Title)}")
            .ToList();

        return $"Top priorities today:{Environment.NewLine}{string.Join(Environment.NewLine, items)}";
    }

    private async Task<string> BuildYesterdayRecapAsync(DateOnly yesterday)
    {
        var metrics = await _dashboardMetricService.GetDailyMetricSummaryAsync(yesterday);

        if (!metrics.HasAnyData)
        {
            return "Yesterday recap: No completed play, sales, or weather data has been logged yet.";
        }

        var previousSummaries = await _dashboardMetricService.GetPreviousAvailableSummariesAsync(yesterday, 7);
        var dailyGdd = await _gddService.GetDailyGddAsync(yesterday.ToDateTime(TimeOnly.MinValue));
        var roundsAverage = GetAverage(previousSummaries.Select(summary => (decimal?)summary.RoundsPlayed));
        var revenueAverage = GetAverage(previousSummaries.Select(summary => summary.TotalRevenue));
        var currentRevenuePerRound = GetRevenuePerRound(metrics);
        var averageRevenuePerRound = GetAverage(previousSummaries.Select(GetRevenuePerRound));
        var roundsChange = metrics.RoundsPlayed is not null && roundsAverage is > 0
            ? GetPercentChange(metrics.RoundsPlayed.Value, roundsAverage.Value)
            : null;
        var revenueChange = metrics.TotalRevenue is not null && revenueAverage is > 0
            ? GetPercentChange(metrics.TotalRevenue.Value, revenueAverage.Value)
            : null;
        var revenuePerRoundChange = currentRevenuePerRound is not null && averageRevenuePerRound is > 0
            ? GetPercentChange(currentRevenuePerRound.Value, averageRevenuePerRound.Value)
            : null;
        var sentences = new List<string>
        {
            BuildYesterdayMetricsSentence(metrics)
        };
        var conditionsSentence = BuildWeatherAndGddSentence(metrics, dailyGdd?.Gdd);

        if (!string.IsNullOrWhiteSpace(conditionsSentence))
        {
            sentences.Add(conditionsSentence);
        }

        sentences.Add(BuildYesterdayTakeaway(metrics, dailyGdd?.Gdd, roundsChange, revenueChange, revenuePerRoundChange));

        return string.Join(" ", sentences);
    }

    private async Task<string> BuildHistoricalBriefingAsync(DateOnly date)
    {
        var metrics = await _dashboardMetricService.GetDailyMetricSummaryAsync(date);
        var previousSummaries = await _dashboardMetricService.GetPreviousAvailableSummariesAsync(date, 7);
        var dailyGdd = await _gddService.GetDailyGddAsync(date.ToDateTime(TimeOnly.MinValue));
        var roundsTrend = _trendService.GetConsecutiveTrend(await _trendService.GetRoundsTrendAsync(7, date));
        var revenueTrend = _trendService.GetConsecutiveTrend(await _trendService.GetRevenueTrendAsync(7, date));

        var roundsAverage = GetAverage(previousSummaries.Select(summary => (decimal?)summary.RoundsPlayed));
        var revenueAverage = GetAverage(previousSummaries.Select(summary => summary.TotalRevenue));
        var currentRevenuePerRound = GetRevenuePerRound(metrics);
        var averageRevenuePerRound = GetAverage(previousSummaries.Select(GetRevenuePerRound));
        var roundsChange = metrics.RoundsPlayed is not null && roundsAverage is > 0
            ? GetPercentChange(metrics.RoundsPlayed.Value, roundsAverage.Value)
            : null;
        var revenueChange = metrics.TotalRevenue is not null && revenueAverage is > 0
            ? GetPercentChange(metrics.TotalRevenue.Value, revenueAverage.Value)
            : null;
        var revenuePerRoundChange = currentRevenuePerRound is not null && averageRevenuePerRound is > 0
            ? GetPercentChange(currentRevenuePerRound.Value, averageRevenuePerRound.Value)
            : null;

        var sentences = new List<string>
        {
            BuildPerformanceSentence(metrics, roundsChange, revenueChange),
            BuildCauseSentence(metrics, roundsChange, roundsTrend, revenueTrend),
            BuildTakeawaySentence(roundsChange, revenueChange, revenuePerRoundChange)
        };
        var turfSentence = BuildTurfSentence(dailyGdd?.Gdd);

        if (!string.IsNullOrWhiteSpace(turfSentence))
        {
            sentences.Add(turfSentence);
        }

        return string.Join(" ", sentences);
    }

    private static string BuildTodayConditionsSentence(DashboardMetricSummary metrics, decimal? dailyGdd)
    {
        var weatherPhrase = GetTodayWeatherPhrase(metrics);
        var gddPhrase = GetTodayGddPhrase(dailyGdd);

        if (!string.IsNullOrWhiteSpace(weatherPhrase) && !string.IsNullOrWhiteSpace(gddPhrase))
        {
            return $"{weatherPhrase} is expected today with {gddPhrase}.";
        }

        if (!string.IsNullOrWhiteSpace(weatherPhrase))
        {
            return $"{weatherPhrase} is expected today.";
        }

        if (!string.IsNullOrWhiteSpace(gddPhrase))
        {
            return $"Today starts with {gddPhrase}.";
        }

        return "Today's weather and GDD data have not been synced yet.";
    }

    private static string BuildTodayPrioritySentence(
        DashboardMetricSummary metrics,
        decimal? dailyGdd,
        int openMaintenanceTaskCount,
        int criticalEquipmentIssueCount)
    {
        if (criticalEquipmentIssueCount > 0)
        {
            return "Prioritize critical equipment repairs, morning course setup, and safety checks before peak play.";
        }

        if (dailyGdd > 15 && metrics.RainfallInches > 0.10m)
        {
            return "Prioritize mowing, turf monitoring, and disease prevention.";
        }

        if (dailyGdd > 15)
        {
            return "Prioritize mowing and maintenance labor allocation.";
        }

        if (openMaintenanceTaskCount >= 10)
        {
            return "Prioritize the maintenance backlog, course setup, and time-sensitive turf needs.";
        }

        return "Prioritize course setup, safety checks, and any time-sensitive maintenance.";
    }

    private static string BuildTodayStatusSentence(int openMaintenanceTaskCount, int criticalEquipmentIssueCount)
    {
        if (openMaintenanceTaskCount == 0 && criticalEquipmentIssueCount == 0)
        {
            return "No maintenance or critical equipment issues are currently open.";
        }

        if (openMaintenanceTaskCount == 0)
        {
            return $"{criticalEquipmentIssueCount} critical equipment issue(s) are currently open.";
        }

        if (criticalEquipmentIssueCount == 0)
        {
            return $"{openMaintenanceTaskCount} maintenance task(s) are open, with no critical equipment issues currently open.";
        }

        return $"{openMaintenanceTaskCount} maintenance task(s) and {criticalEquipmentIssueCount} critical equipment issue(s) are currently open.";
    }

    private static string? GetTodayWeatherPhrase(DashboardMetricSummary metrics)
    {
        if (!string.IsNullOrWhiteSpace(metrics.WeatherSummary))
        {
            return CapitalizeFirst(metrics.WeatherSummary.ToLowerInvariant());
        }

        if (metrics.RainfallInches > 0.10m)
        {
            return $"Rainfall of {metrics.RainfallInches:0.##} inches";
        }

        return null;
    }

    private static string? GetTodayGddPhrase(decimal? dailyGdd)
    {
        if (dailyGdd is null)
        {
            return null;
        }

        if (dailyGdd > 25)
        {
            return $"extreme turf pressure (GDD {dailyGdd:0.#})";
        }

        if (dailyGdd > 15)
        {
            return $"elevated turf pressure (GDD {dailyGdd:0.#})";
        }

        if (dailyGdd >= 5)
        {
            return $"moderate turf pressure (GDD {dailyGdd:0.#})";
        }

        return $"low turf pressure (GDD {dailyGdd:0.#})";
    }

    private static string BuildYesterdayMetricsSentence(DashboardMetricSummary metrics)
    {
        var roundsText = metrics.RoundsPlayed is null ? "rounds not logged" : $"{metrics.RoundsPlayed} rounds";
        var cartText = metrics.CartRentals is null ? "cart rentals not logged" : $"{metrics.CartRentals} cart rentals";
        var revenueText = metrics.TotalRevenue is null ? "revenue not logged" : $"{metrics.TotalRevenue:C} revenue";

        return $"Yesterday closed with {roundsText}, {cartText}, and {revenueText}.";
    }

    private static string? BuildWeatherAndGddSentence(DashboardMetricSummary metrics, decimal? dailyGdd)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(metrics.WeatherSummary))
        {
            parts.Add($"weather was {metrics.WeatherSummary.ToLowerInvariant()}");
        }

        if (dailyGdd is not null)
        {
            parts.Add($"GDD was {dailyGdd:0.#} ({GetGddClassification(dailyGdd.Value)})");
        }

        return parts.Count == 0 ? null : $"{CapitalizeFirst(string.Join(", and ", parts))}.";
    }

    private static string BuildYesterdayTakeaway(
        DashboardMetricSummary metrics,
        decimal? dailyGdd,
        decimal? roundsChange,
        decimal? revenueChange,
        decimal? revenuePerRoundChange)
    {
        if (roundsChange is < -20 && revenueChange is < -20 && HasDifficultWeather(metrics))
        {
            return "Key takeaway: weather likely pressured play volume and revenue.";
        }

        if (roundsChange is > 20 && revenueChange is > 20)
        {
            return "Key takeaway: demand and revenue outperformed recent patterns.";
        }

        if (revenuePerRoundChange is > 15)
        {
            return "Key takeaway: spending per round was stronger than recent patterns.";
        }

        if (dailyGdd > 15)
        {
            return "Key takeaway: turf growth pressure remained elevated.";
        }

        if (HasDifficultWeather(metrics))
        {
            return "Key takeaway: weather likely impacted play volume and should be factored into performance evaluation.";
        }

        return "Key takeaway: operations were generally steady against recent trends.";
    }

    private static string BuildPerformanceSentence(
        DashboardMetricSummary metrics,
        decimal? roundsChange,
        decimal? revenueChange)
    {
        var roundsPhrase = BuildRoundsPhrase(metrics.RoundsPlayed, roundsChange);
        var revenuePhrase = BuildRevenuePhrase(metrics.TotalRevenue, revenueChange);

        return $"{CapitalizeFirst(roundsPhrase)}, and {revenuePhrase}.";
    }

    private static string BuildRoundsPhrase(int? roundsPlayed, decimal? changePercent)
    {
        if (roundsPlayed is null)
        {
            return "rounds were not logged";
        }

        if (changePercent is < -20)
        {
            return $"rounds were {Math.Abs(changePercent.Value):0}% below recent trends at {roundsPlayed} played";
        }

        if (changePercent is > 20)
        {
            return $"rounds were {changePercent.Value:0}% above recent trends at {roundsPlayed} played";
        }

        return $"rounds were steady at {roundsPlayed} played";
    }

    private static string BuildRevenuePhrase(decimal? totalRevenue, decimal? changePercent)
    {
        if (totalRevenue is null)
        {
            return "revenue was not logged";
        }

        if (changePercent is < -20)
        {
            return $"total revenue was {Math.Abs(changePercent.Value):0}% below recent trends at {totalRevenue:C}";
        }

        if (changePercent is > 20)
        {
            return $"total revenue was {changePercent.Value:0}% above recent trends at {totalRevenue:C}";
        }

        return $"total revenue was steady at {totalRevenue:C}";
    }

    private static string BuildCauseSentence(
        DashboardMetricSummary metrics,
        decimal? roundsChange,
        TrendDirection? roundsTrend,
        TrendDirection? revenueTrend)
    {
        var difficultWeather = HasDifficultWeather(metrics);

        if (roundsChange is < -20 && difficultWeather)
        {
            return $"Weather conditions ({GetDifficultWeatherLabel(metrics)}) likely reduced play volume.";
        }

        if (roundsChange is > 20 && difficultWeather)
        {
            return "Rounds increased despite unfavorable weather, suggesting strong demand or pre-booked tee sheet volume.";
        }

        if (roundsTrend?.Direction == "Down" && revenueTrend?.Direction == "Down")
        {
            return "Recent trends show both play volume and revenue softening into this date.";
        }

        if (roundsTrend?.Direction == "Up" && revenueTrend?.Direction == "Up")
        {
            return "Recent trends show play volume and revenue building into this date.";
        }

        if (roundsTrend?.Direction == "Down")
        {
            return "Recent rounds trends were softening into this date.";
        }

        if (roundsTrend?.Direction == "Up")
        {
            return "Recent rounds trends were improving into this date.";
        }

        if (!string.IsNullOrWhiteSpace(metrics.WeatherSummary))
        {
            return $"Weather was {metrics.WeatherSummary.ToLowerInvariant()}, which should be considered when reviewing performance.";
        }

        return "Weather and recent trends did not point to a clear performance driver.";
    }

    private static string BuildTakeawaySentence(
        decimal? roundsChange,
        decimal? revenueChange,
        decimal? revenuePerRoundChange)
    {
        if (roundsChange is < -20 && revenueChange is < -20)
        {
            return "Monitor tee sheet activity and review pricing, promotions, and F&B performance.";
        }

        if (revenueChange is < -20)
        {
            return "Review pro shop and F&B spending because revenue softened without a matching rounds decline.";
        }

        if (roundsChange is > 20 || revenueChange is > 20)
        {
            return "Keep staffing, cart availability, and service coverage aligned with stronger demand.";
        }

        if (revenuePerRoundChange is > 15)
        {
            return "Spending per round is strong, so protect service quality while watching capacity.";
        }

        return "Watch tee sheet, staffing, and spending patterns for the next operating day.";
    }

    private static string? BuildTurfSentence(decimal? dailyGdd)
    {
        if (dailyGdd is null)
        {
            return null;
        }

        if (dailyGdd > 25)
        {
            return $"Extreme GDD ({dailyGdd:0.#}) indicates heavy turf growth pressure, so monitor stress, disease risk, and mowing needs.";
        }

        if (dailyGdd > 15)
        {
            return $"High GDD ({dailyGdd:0.#}) indicates increased turf growth pressure, so prioritize mowing frequency and maintenance planning.";
        }

        if (dailyGdd >= 5)
        {
            return $"Moderate GDD ({dailyGdd:0.#}) suggests steady turf growth, so maintenance demand should remain stable.";
        }

        return $"Low GDD ({dailyGdd:0.#}) suggests minimal turf growth, so maintenance demand should be lighter.";
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

    private static decimal? GetPercentChange(decimal current, decimal previous)
    {
        if (previous == 0)
        {
            return null;
        }

        return (current - previous) / previous * 100;
    }

    private static bool HasDifficultWeather(DashboardMetricSummary metrics)
    {
        var summary = metrics.WeatherSummary ?? string.Empty;

        return summary.Contains("fog", StringComparison.OrdinalIgnoreCase) ||
            summary.Contains("rain", StringComparison.OrdinalIgnoreCase) ||
            summary.Contains("precipitation", StringComparison.OrdinalIgnoreCase) ||
            metrics.RainfallInches > 0.10m;
    }

    private static string GetDifficultWeatherLabel(DashboardMetricSummary metrics)
    {
        var summary = metrics.WeatherSummary ?? string.Empty;
        var conditions = new List<string>();

        if (summary.Contains("rain", StringComparison.OrdinalIgnoreCase) ||
            summary.Contains("precipitation", StringComparison.OrdinalIgnoreCase) ||
            metrics.RainfallInches > 0.10m)
        {
            conditions.Add("rain");
        }

        if (summary.Contains("fog", StringComparison.OrdinalIgnoreCase))
        {
            conditions.Add("fog");
        }

        return conditions.Count == 0 ? "weather" : string.Join("/", conditions);
    }

    private static string CapitalizeFirst(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return char.ToUpperInvariant(value[0]) + value[1..];
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
