namespace CourseCommander.Services;

public class DashboardMetricSummary
{
    public DateOnly Date { get; set; }
    public int? RoundsPlayed { get; set; }
    public int? CartRentals { get; set; }
    public decimal? TotalRevenue { get; set; }
    public decimal? FoodAndBeverageRevenue { get; set; }
    public decimal? ProShopRevenue { get; set; }
    public decimal? AlcoholRevenue { get; set; }
    public decimal? RangeBallRevenue { get; set; }
    public decimal? RainfallInches { get; set; }
    public decimal? HighTemp { get; set; }
    public decimal? LowTemp { get; set; }
    public string? WeatherSummary { get; set; }
    public string? PlaySourceSystemName { get; set; }
    public string? SalesSourceSystemName { get; set; }
    public string? WeatherSourceSystemName { get; set; }

    public bool HasAnyData =>
        RoundsPlayed is not null ||
        CartRentals is not null ||
        TotalRevenue is not null ||
        RainfallInches is not null ||
        HighTemp is not null ||
        !string.IsNullOrWhiteSpace(WeatherSummary);
}
