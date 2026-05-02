using CourseCommander.Data;
using CourseCommander.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CourseCommander.Services;

public class ForecastService
{
    private readonly AppDbContext _context;
    private readonly DashboardMetricService _dashboardMetricService;
    private readonly TrendService _trendService;
    private readonly GrowingDegreeDayService _gddService;

    public ForecastService(
        AppDbContext context,
        DashboardMetricService dashboardMetricService,
        TrendService trendService,
        GrowingDegreeDayService gddService)
    {
        _context = context;
        _dashboardMetricService = dashboardMetricService;
        _trendService = trendService;
        _gddService = gddService;
    }

    public async Task<ForecastDto> GetForecastAsync(DateOnly date)
    {
        var recentDays = await _dashboardMetricService.GetPreviousAvailableSummariesAsync(date, 7);
        var sameWeekdayDays = await GetSameWeekdayHistoryAsync(date);

        if (recentDays.Count == 0)
        {
            return new ForecastDto
            {
                Date = date,
                ConfidenceLevel = "Low",
                Explanation = "Not enough recent play or sales history is available for a forecast."
            };
        }

        var baseline = BuildBaseline(recentDays, sameWeekdayDays);
        var weather = await GetWeatherForForecastAsync(date, recentDays);
        var predictedRounds = baseline.Rounds;
        var predictedTotalRevenue = baseline.TotalRevenue;
        var predictedFoodAndBeverageRevenue = baseline.FoodAndBeverageRevenue;
        var predictedCartRentals = baseline.CartRentals;
        var explanationParts = new List<string> { baseline.Explanation };

        ApplyWeatherAdjustments(
            weather,
            ref predictedRounds,
            ref predictedTotalRevenue,
            ref predictedFoodAndBeverageRevenue,
            explanationParts,
            baseline.HasAlcoholHistory);

        var trendAdjustedValues = await ApplyTrendAdjustmentsAsync(
            date,
            predictedRounds,
            predictedTotalRevenue,
            explanationParts);
        predictedRounds = trendAdjustedValues.Rounds;
        predictedTotalRevenue = trendAdjustedValues.TotalRevenue;

        predictedCartRentals = predictedCartRentals > 0
            ? predictedCartRentals * (predictedRounds / Math.Max(1, baseline.Rounds))
            : predictedRounds * 0.65m;

        var predictedGdd = GetPredictedGdd(weather, recentDays);
        var dayLabel = date == DateOnly.FromDateTime(DateTime.Now).AddDays(1)
            ? "Tomorrow"
            : date.ToString("MMMM d");
        var summary = $"{dayLabel} is expected to see about {Math.Max(0, (int)Math.Round(predictedRounds))} rounds with revenue near {Math.Max(0, predictedTotalRevenue):C0}.";

        explanationParts.Add(summary);

        return new ForecastDto
        {
            Date = date,
            PredictedRounds = Math.Max(0, (int)Math.Round(predictedRounds)),
            PredictedTotalRevenue = Math.Max(0, Math.Round(predictedTotalRevenue, 2)),
            PredictedFoodAndBeverageRevenue = Math.Max(0, Math.Round(predictedFoodAndBeverageRevenue, 2)),
            PredictedCartRentals = Math.Max(0, (int)Math.Round(predictedCartRentals)),
            PredictedGdd = Math.Max(0, Math.Round(predictedGdd, 1)),
            ConfidenceLevel = GetConfidenceLevel(recentDays.Count, sameWeekdayDays.Count, weather.HasWeatherData),
            Explanation = string.Join(" ", explanationParts),
            Summary = summary
        };
    }

    private async Task<List<DashboardMetricSummary>> GetSameWeekdayHistoryAsync(DateOnly date)
    {
        var startDate = date.AddDays(-70);
        var playDates = await _context.DailyPlayMetrics
            .Where(metric => metric.Date < date && metric.Date >= startDate)
            .Select(metric => metric.Date)
            .ToListAsync();
        var salesDates = await _context.DailySalesMetrics
            .Where(metric => metric.Date < date && metric.Date >= startDate)
            .Select(metric => metric.Date)
            .ToListAsync();

        var candidateDates = playDates
            .Concat(salesDates)
            .Distinct()
            .Where(candidateDate => candidateDate.DayOfWeek == date.DayOfWeek)
            .OrderByDescending(candidateDate => candidateDate)
            .Take(5)
            .ToList();

        var summaries = new List<DashboardMetricSummary>();

        foreach (var candidateDate in candidateDates)
        {
            var summary = await _dashboardMetricService.GetDailyMetricSummaryAsync(candidateDate);

            if (summary.RoundsPlayed is not null || summary.TotalRevenue is not null)
            {
                summaries.Add(summary);
            }
        }

        return summaries;
    }

    private ForecastBaseline BuildBaseline(
        List<DashboardMetricSummary> recentDays,
        List<DashboardMetricSummary> sameWeekdayDays)
    {
        var recent = BuildAverage(recentDays);
        var sameWeekday = sameWeekdayDays.Count > 0 ? BuildAverage(sameWeekdayDays) : null;
        var useSameWeekday = sameWeekday is not null && sameWeekdayDays.Count >= 2;
        var rounds = Blend(recent.Rounds, sameWeekday?.Rounds, useSameWeekday);
        var totalRevenue = Blend(recent.TotalRevenue, sameWeekday?.TotalRevenue, useSameWeekday);
        var foodAndBeverageRevenue = Blend(
            recent.FoodAndBeverageRevenue,
            sameWeekday?.FoodAndBeverageRevenue,
            useSameWeekday);
        var cartRentals = Blend(recent.CartRentals, sameWeekday?.CartRentals, useSameWeekday);

        if (foodAndBeverageRevenue == 0 && totalRevenue > 0)
        {
            foodAndBeverageRevenue = totalRevenue * 0.25m;
        }

        if (cartRentals == 0 && rounds > 0)
        {
            cartRentals = rounds * 0.65m;
        }

        return new ForecastBaseline
        {
            Rounds = rounds,
            TotalRevenue = totalRevenue,
            FoodAndBeverageRevenue = foodAndBeverageRevenue,
            CartRentals = cartRentals,
            HasAlcoholHistory = recent.HasAlcoholHistory || sameWeekday?.HasAlcoholHistory == true,
            Explanation = useSameWeekday
                ? "Forecast uses the recent 7-day average, weighted toward same-weekday history."
                : "Forecast uses the recent 7-day average because limited same-weekday history is available."
        };
    }

    private static ForecastAverage BuildAverage(List<DashboardMetricSummary> summaries)
    {
        return new ForecastAverage
        {
            Rounds = Average(summaries.Select(summary => ToDecimal(summary.RoundsPlayed))),
            CartRentals = Average(summaries.Select(summary => ToDecimal(summary.CartRentals))),
            TotalRevenue = Average(summaries.Select(summary => summary.TotalRevenue)),
            FoodAndBeverageRevenue = Average(summaries.Select(summary => summary.FoodAndBeverageRevenue)),
            HasAlcoholHistory = summaries.Any(summary => summary.AlcoholRevenue > 0)
        };
    }

    private async Task<ForecastWeather> GetWeatherForForecastAsync(
        DateOnly date,
        List<DashboardMetricSummary> recentDays)
    {
        var summary = await _dashboardMetricService.GetDailyMetricSummaryAsync(date);
        var hasWeatherData = summary.HighTemp is not null ||
            summary.LowTemp is not null ||
            summary.RainfallInches is not null ||
            !string.IsNullOrWhiteSpace(summary.WeatherSummary);

        if (hasWeatherData)
        {
            return new ForecastWeather
            {
                HighTemp = summary.HighTemp,
                LowTemp = summary.LowTemp,
                RainfallInches = summary.RainfallInches,
                WeatherSummary = summary.WeatherSummary,
                HasWeatherData = true
            };
        }

        return new ForecastWeather
        {
            HighTemp = Average(recentDays.Select(day => day.HighTemp)),
            LowTemp = Average(recentDays.Select(day => day.LowTemp)),
            RainfallInches = Average(recentDays.Select(day => day.RainfallInches)),
            HasWeatherData = false
        };
    }

    private static void ApplyWeatherAdjustments(
        ForecastWeather weather,
        ref decimal predictedRounds,
        ref decimal predictedTotalRevenue,
        ref decimal predictedFoodAndBeverageRevenue,
        List<string> explanationParts,
        bool hasAlcoholHistory)
    {
        if (IsRainy(weather))
        {
            var reduction = weather.RainfallInches > 0.25m ? 0.30m : 0.15m;
            predictedRounds *= 1 - reduction;
            predictedTotalRevenue *= 1 - reduction;
            predictedFoodAndBeverageRevenue *= 0.95m;
            explanationParts.Add("Rain is expected to reduce play volume.");
        }

        if (weather.HighTemp > 85)
        {
            predictedRounds *= 1.05m;
            predictedTotalRevenue *= 1.06m;
            predictedFoodAndBeverageRevenue *= hasAlcoholHistory ? 1.14m : 1.10m;
            explanationParts.Add("Warm conditions should support stronger F&B demand.");
        }
        else if (weather.HighTemp > 80)
        {
            predictedFoodAndBeverageRevenue *= hasAlcoholHistory ? 1.10m : 1.07m;
            explanationParts.Add("Warm weather may lift beverage and patio spending.");
        }

        if (weather.HighTemp < 60 || weather.LowTemp < 45)
        {
            predictedRounds *= 0.88m;
            predictedTotalRevenue *= 0.90m;
            predictedFoodAndBeverageRevenue *= 0.92m;
            explanationParts.Add("Cool temperatures may soften play and spending.");
        }
    }

    private async Task<(decimal Rounds, decimal TotalRevenue)> ApplyTrendAdjustmentsAsync(
        DateOnly date,
        decimal predictedRounds,
        decimal predictedTotalRevenue,
        List<string> explanationParts)
    {
        var roundsTrend = _trendService.GetConsecutiveTrend(await _trendService.GetRoundsTrendAsync(7, date.AddDays(-1)));
        var revenueTrend = _trendService.GetConsecutiveTrend(await _trendService.GetRevenueTrendAsync(7, date.AddDays(-1)));

        if (roundsTrend?.Direction == "Up")
        {
            predictedRounds *= 1.05m;
            explanationParts.Add("Recent rounds trend is moving upward.");
        }
        else if (roundsTrend?.Direction == "Down")
        {
            predictedRounds *= 0.95m;
            explanationParts.Add("Recent rounds trend is moving downward.");
        }

        if (revenueTrend?.Direction == "Up")
        {
            predictedTotalRevenue *= 1.05m;
        }
        else if (revenueTrend?.Direction == "Down")
        {
            predictedTotalRevenue *= 0.95m;
        }

        return (predictedRounds, predictedTotalRevenue);
    }

    private decimal GetPredictedGdd(ForecastWeather weather, List<DashboardMetricSummary> recentDays)
    {
        if (weather.HighTemp is not null && weather.LowTemp is not null)
        {
            return _gddService.CalculateDailyGdd(weather.HighTemp.Value, weather.LowTemp.Value);
        }

        var recentGddValues = recentDays
            .Where(day => day.HighTemp is not null && day.LowTemp is not null)
            .Select(day => _gddService.CalculateDailyGdd(day.HighTemp!.Value, day.LowTemp!.Value))
            .ToList();

        return recentGddValues.Count == 0 ? 0 : recentGddValues.Average();
    }

    private static string GetConfidenceLevel(int recentDayCount, int sameWeekdayCount, bool hasWeather)
    {
        if (recentDayCount >= 7 && sameWeekdayCount >= 3 && hasWeather)
        {
            return "High";
        }

        if (recentDayCount >= 5)
        {
            return "Medium";
        }

        return "Low";
    }

    private static bool IsRainy(ForecastWeather weather)
    {
        return weather.RainfallInches > 0.10m ||
            weather.WeatherSummary?.Contains("rain", StringComparison.OrdinalIgnoreCase) == true ||
            weather.WeatherSummary?.Contains("precipitation", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static decimal Blend(decimal recentValue, decimal? sameWeekdayValue, bool useSameWeekday)
    {
        return useSameWeekday && sameWeekdayValue is not null
            ? (sameWeekdayValue.Value * 0.60m) + (recentValue * 0.40m)
            : recentValue;
    }

    private static decimal Average(IEnumerable<decimal?> values)
    {
        var validValues = values
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToList();

        return validValues.Count == 0 ? 0 : validValues.Average();
    }

    private static decimal? ToDecimal(int? value)
    {
        return value is null ? null : value.Value;
    }

    private class ForecastAverage
    {
        public decimal Rounds { get; set; }
        public decimal CartRentals { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal FoodAndBeverageRevenue { get; set; }
        public bool HasAlcoholHistory { get; set; }
    }

    private class ForecastBaseline : ForecastAverage
    {
        public string Explanation { get; set; } = string.Empty;
    }

    private class ForecastWeather
    {
        public decimal? HighTemp { get; set; }
        public decimal? LowTemp { get; set; }
        public decimal? RainfallInches { get; set; }
        public string? WeatherSummary { get; set; }
        public bool HasWeatherData { get; set; }
    }
}
