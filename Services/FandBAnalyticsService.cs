using CourseCommander.DTOs;

namespace CourseCommander.Services;

public class FandBAnalyticsService
{
    private readonly DashboardMetricService _dashboardMetricService;

    public FandBAnalyticsService(DashboardMetricService dashboardMetricService)
    {
        _dashboardMetricService = dashboardMetricService;
    }

    public async Task<FandBAnalyticsDto> GetAnalyticsAsync(DateOnly date)
    {
        var metrics = await _dashboardMetricService.GetDailyMetricSummaryAsync(date);
        var previousSummaries = await _dashboardMetricService.GetPreviousAvailableSummariesAsync(date, 7);

        return new FandBAnalyticsDto
        {
            Date = date,
            FoodAndBeverageRevenue = metrics.FoodAndBeverageRevenue,
            FandBRevenuePerRound = GetRevenuePerRound(metrics.FoodAndBeverageRevenue, metrics.RoundsPlayed),
            AlcoholRevenue = metrics.AlcoholRevenue,
            AlcoholSharePercent = GetPercentShare(metrics.AlcoholRevenue, metrics.FoodAndBeverageRevenue),
            RangeBallRevenue = metrics.RangeBallRevenue,
            RangeBallRevenuePerRound = GetRevenuePerRound(metrics.RangeBallRevenue, metrics.RoundsPlayed),
            AverageFandBRevenuePerRound = GetAverage(previousSummaries.Select(summary =>
                GetRevenuePerRound(summary.FoodAndBeverageRevenue, summary.RoundsPlayed))),
            AverageAlcoholSharePercent = GetAverage(previousSummaries.Select(summary =>
                GetPercentShare(summary.AlcoholRevenue, summary.FoodAndBeverageRevenue))),
            AverageRangeBallRevenuePerRound = GetAverage(previousSummaries.Select(summary =>
                GetRevenuePerRound(summary.RangeBallRevenue, summary.RoundsPlayed)))
        };
    }

    public async Task<List<FandBInsightSuggestion>> GetInsightSuggestionsAsync(DateOnly date)
    {
        var suggestions = new List<FandBInsightSuggestion>();
        var metrics = await _dashboardMetricService.GetDailyMetricSummaryAsync(date);
        var previousSummaries = await _dashboardMetricService.GetPreviousAvailableSummariesAsync(date, 7);
        var analytics = await GetAnalyticsAsync(date);
        var roundsAverage = GetAverage(previousSummaries.Select(summary => (decimal?)summary.RoundsPlayed));
        var roundsBelowAverage = metrics.RoundsPlayed is not null &&
            roundsAverage is > 0 &&
            metrics.RoundsPlayed.Value < roundsAverage.Value * 0.85m;

        if (analytics.FandBRevenuePerRound is not null && analytics.AverageFandBRevenuePerRound is > 0)
        {
            var changePercent = GetPercentChange(
                analytics.FandBRevenuePerRound.Value,
                analytics.AverageFandBRevenuePerRound.Value);

            if (changePercent > 15)
            {
                suggestions.Add(new FandBInsightSuggestion(
                    "F&B Performance",
                    "Info",
                    $"F&B revenue per round was {changePercent:0}% above the recent average, suggesting increased on-site spending."));
            }
            else if (changePercent < -15)
            {
                suggestions.Add(new FandBInsightSuggestion(
                    "F&B Performance",
                    "Warning",
                    $"F&B revenue per round was {Math.Abs(changePercent):0}% below the recent average. Review turn traffic, staffing, and menu conversion."));
            }
        }

        if (metrics.HighTemp > 80 && analytics.AlcoholRevenue > 0)
        {
            var alcoholShareChange = analytics.AlcoholSharePercent is not null && analytics.AverageAlcoholSharePercent is > 0
                ? GetPercentChange(analytics.AlcoholSharePercent.Value, analytics.AverageAlcoholSharePercent.Value)
                : 0;

            if (analytics.AlcoholSharePercent > 35 || alcoholShareChange > 10)
            {
                suggestions.Add(new FandBInsightSuggestion(
                    "Alcohol Sales",
                    "Info",
                    "Alcohol sales were elevated, likely driven by warmer temperatures."));
            }
        }

        if (metrics.HighTemp < 65 && analytics.FandBRevenuePerRound is not null && analytics.AverageFandBRevenuePerRound is > 0)
        {
            var changePercent = GetPercentChange(
                analytics.FandBRevenuePerRound.Value,
                analytics.AverageFandBRevenuePerRound.Value);

            if (changePercent < -10)
            {
                suggestions.Add(new FandBInsightSuggestion(
                    "Beverage Demand",
                    "Info",
                    "Cooler temperatures may have limited beverage sales compared with recent conditions."));
            }
        }

        if (IsRainy(metrics) && roundsBelowAverage && analytics.FandBRevenuePerRound > analytics.AverageFandBRevenuePerRound * 1.10m)
        {
            suggestions.Add(new FandBInsightSuggestion(
                "Rain and F&B",
                "Info",
                "Rain likely reduced rounds, but F&B spend per round was above average, suggesting indoor or bar activity."));
        }

        if (roundsBelowAverage &&
            analytics.RangeBallRevenuePerRound is not null &&
            analytics.AverageRangeBallRevenuePerRound is > 0)
        {
            var rangeChange = GetPercentChange(
                analytics.RangeBallRevenuePerRound.Value,
                analytics.AverageRangeBallRevenuePerRound.Value);

            if (rangeChange > 20)
            {
                suggestions.Add(new FandBInsightSuggestion(
                    "Range Activity",
                    "Info",
                    "Range usage remained strong despite lower rounds, indicating practice demand."));
            }
        }

        return suggestions;
    }

    private static decimal? GetRevenuePerRound(decimal? revenue, int? roundsPlayed)
    {
        if (revenue is not null && roundsPlayed > 0)
        {
            return revenue.Value / roundsPlayed.Value;
        }

        return null;
    }

    private static decimal? GetPercentShare(decimal? part, decimal? total)
    {
        if (part is not null && total > 0)
        {
            return part.Value / total.Value * 100;
        }

        return null;
    }

    private static decimal? GetAverage(IEnumerable<decimal?> values)
    {
        var availableValues = values
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToList();

        return availableValues.Count == 0 ? null : availableValues.Average();
    }

    private static decimal GetPercentChange(decimal current, decimal previous)
    {
        return (current - previous) / previous * 100;
    }

    private static bool IsRainy(DashboardMetricSummary metrics)
    {
        return metrics.RainfallInches > 0.10m ||
            metrics.WeatherSummary?.Contains("rain", StringComparison.OrdinalIgnoreCase) == true ||
            metrics.WeatherSummary?.Contains("precipitation", StringComparison.OrdinalIgnoreCase) == true;
    }
}

public record FandBInsightSuggestion(string Title, string Category, string Message);
