using CourseCommander.Entities;

namespace CourseCommander.DTOs;

public class DashboardSummaryDto
{
    public DateOnly Date { get; set; }
    public string DailyBriefing { get; set; } = string.Empty;
    public string? YesterdayRecap { get; set; }
    public string BriefingMode { get; set; } = "HistoricalRecap";
    public int RoundsPlayed { get; set; }
    public int? CartRentals { get; set; }
    public decimal? TotalRevenue { get; set; }
    public decimal? ProShopRevenue { get; set; }
    public decimal? FoodAndBeverageRevenue { get; set; }
    public decimal? AlcoholRevenue { get; set; }
    public decimal? RangeBallRevenue { get; set; }
    public FandBAnalyticsDto? FandBAnalytics { get; set; }
    public string? WeatherSummary { get; set; }
    public DailyGddDto? DailyGdd { get; set; }
    public GddSummaryDto Past30DaysGdd { get; set; } = new();
    public GddSummaryDto YearToDateGdd { get; set; } = new();
    public AgronomySummaryDto TurfConditions { get; set; } = new();
    public int OpenMaintenanceTaskCount { get; set; }
    public int CriticalMaintenanceTaskCount { get; set; }
    public int OpenEquipmentIssueCount { get; set; }
    public int CriticalEquipmentIssueCount { get; set; }
    public DashboardSourceSystemsDto SourceSystems { get; set; } = new();
    public List<AlertDto> Alerts { get; set; } = new();
    public List<PriorityActionDto> Priorities { get; set; } = new();
    public List<Insight> Insights { get; set; } = new();
}

public class DashboardSourceSystemsDto
{
    public string? Play { get; set; }
    public string? Sales { get; set; }
    public string? Weather { get; set; }
}

public class AgronomySummaryDto
{
    public DateOnly Date { get; set; }
    public decimal? AverageMoistureToday { get; set; }
    public decimal? LowestMoistureReading { get; set; }
    public decimal? HighestMoistureReading { get; set; }
    public List<DriestLocationDto> TopDriestLocations { get; set; } = new();
}

public class DriestLocationDto
{
    public string Location { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Unit { get; set; } = string.Empty;
}
