using CourseCommander.Data;
using CourseCommander.DTOs;
using CourseCommander.Entities;
using Microsoft.EntityFrameworkCore;

namespace CourseCommander.Services;

public class DemoDataService
{
    private const string DemoSourceName = "Demo Data";
    private const string DemoPrefix = "[Demo]";

    private readonly AppDbContext _context;

    public DemoDataService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<DemoStatusDto> GetStatusAsync()
    {
        var playCount = await _context.DailyPlayMetrics
            .CountAsync(metric => metric.SourceSystemName == DemoSourceName);
        var salesCount = await _context.DailySalesMetrics
            .CountAsync(metric => metric.SourceSystemName == DemoSourceName);
        var weatherCount = await _context.DailyWeatherMetrics
            .CountAsync(metric => metric.SourceSystemName == DemoSourceName);

        return new DemoStatusDto
        {
            DemoDataExists = playCount > 0 || salesCount > 0 || weatherCount > 0,
            DemoPlayRecordCount = playCount,
            DemoSalesRecordCount = salesCount,
            DemoWeatherRecordCount = weatherCount
        };
    }

    public async Task<DemoStatusDto> LoadDemoDataAsync()
    {
        await ClearDemoDataAsync(saveChanges: false);

        var random = new Random(42);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var playMetrics = new List<DailyPlayMetric>();
        var salesMetrics = new List<DailySalesMetric>();
        var weatherMetrics = new List<DailyWeatherMetric>();

        for (var dayIndex = 0; dayIndex < 30; dayIndex++)
        {
            var date = today.AddDays(dayIndex - 29);
            var weather = BuildWeatherMetric(date, dayIndex, random);
            var play = BuildPlayMetric(date, weather.RainfallInches, random);
            var sales = BuildSalesMetric(date, play.RoundsPlayed, weather.HighTemp, weather.RainfallInches, random);

            weatherMetrics.Add(weather);
            playMetrics.Add(play);
            salesMetrics.Add(sales);
        }

        _context.DailyWeatherMetrics.AddRange(weatherMetrics);
        _context.DailyPlayMetrics.AddRange(playMetrics);
        _context.DailySalesMetrics.AddRange(salesMetrics);
        _context.MaintenanceTasks.AddRange(BuildMaintenanceTasks(today));
        _context.EquipmentIssues.AddRange(BuildEquipmentIssues(today));

        await _context.SaveChangesAsync();
        return await GetStatusAsync();
    }

    public Task<DemoStatusDto> ClearDemoDataAsync()
    {
        return ClearDemoDataAsync(saveChanges: true);
    }

    private async Task<DemoStatusDto> ClearDemoDataAsync(bool saveChanges)
    {
        var demoPlayMetrics = await _context.DailyPlayMetrics
            .Where(metric => metric.SourceSystemName == DemoSourceName)
            .ToListAsync();
        var demoSalesMetrics = await _context.DailySalesMetrics
            .Where(metric => metric.SourceSystemName == DemoSourceName)
            .ToListAsync();
        var demoWeatherMetrics = await _context.DailyWeatherMetrics
            .Where(metric => metric.SourceSystemName == DemoSourceName)
            .ToListAsync();
        var demoMaintenanceTasks = await _context.MaintenanceTasks
            .Where(task => task.Title.StartsWith(DemoPrefix))
            .ToListAsync();
        var demoEquipmentIssues = await _context.EquipmentIssues
            .Where(issue => issue.EquipmentName.StartsWith(DemoPrefix))
            .ToListAsync();

        _context.DailyPlayMetrics.RemoveRange(demoPlayMetrics);
        _context.DailySalesMetrics.RemoveRange(demoSalesMetrics);
        _context.DailyWeatherMetrics.RemoveRange(demoWeatherMetrics);
        _context.MaintenanceTasks.RemoveRange(demoMaintenanceTasks);
        _context.EquipmentIssues.RemoveRange(demoEquipmentIssues);

        if (saveChanges)
        {
            await _context.SaveChangesAsync();
        }

        return await GetStatusAsync();
    }

    private static DailyWeatherMetric BuildWeatherMetric(DateOnly date, int dayIndex, Random random)
    {
        var rainyDayIndexes = new[] { 2, 6, 11, 17, 23, 27 };
        var isRainy = rainyDayIndexes.Contains(dayIndex);
        var rainfall = isRainy
            ? Math.Round((decimal)(0.15 + random.NextDouble() * 0.65), 2)
            : 0m;
        var highTemp = 67 + (dayIndex % 8) + random.Next(-4, 5);
        var lowTemp = highTemp - random.Next(13, 20);

        return new DailyWeatherMetric
        {
            Date = date,
            HighTemp = highTemp,
            LowTemp = lowTemp,
            RainfallInches = rainfall,
            WeatherSummary = GetWeatherSummary(rainfall, random),
            SourceSystemName = DemoSourceName,
            SyncedAt = DateTime.UtcNow
        };
    }

    private static DailyPlayMetric BuildPlayMetric(DateOnly date, decimal rainfall, Random random)
    {
        var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var baseRounds = isWeekend ? 158 : 96;
        var weatherPenalty = rainfall > 0.25m ? 44 : rainfall > 0 ? 24 : 0;
        var roundsPlayed = Math.Max(34, baseRounds - weatherPenalty + random.Next(-12, 13));
        var cartRentals = (int)Math.Round(roundsPlayed * (0.60 + random.NextDouble() * 0.15));
        var nineHoleRounds = (int)Math.Round(roundsPlayed * (0.18 + random.NextDouble() * 0.08));

        return new DailyPlayMetric
        {
            Date = date,
            SourceSystemName = DemoSourceName,
            RoundsPlayed = roundsPlayed,
            CartRentals = cartRentals,
            NineHoleRounds = nineHoleRounds,
            EighteenHoleRounds = roundsPlayed - nineHoleRounds,
            SyncedAt = DateTime.UtcNow
        };
    }

    private static DailySalesMetric BuildSalesMetric(DateOnly date, int roundsPlayed, decimal highTemp, decimal rainfall, Random random)
    {
        var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var spendPerRound = (decimal)(isWeekend ? 82 + random.NextDouble() * 12 : 68 + random.NextDouble() * 10);
        var totalRevenue = Math.Round(roundsPlayed * spendPerRound, 2);
        var fandBShare = 0.28 + random.NextDouble() * 0.08;
        var alcoholShare = 0.30 + random.NextDouble() * 0.12;

        if (highTemp > 80)
        {
            fandBShare += 0.04;
            alcoholShare += 0.08;
        }

        if (rainfall > 0.10m)
        {
            fandBShare += 0.03;
        }

        var foodAndBeverageRevenue = Math.Round(totalRevenue * (decimal)fandBShare, 2);
        var proShopRevenue = Math.Round(totalRevenue * (decimal)(0.18 + random.NextDouble() * 0.08), 2);
        var alcoholRevenue = Math.Round(foodAndBeverageRevenue * (decimal)alcoholShare, 2);
        var rangeBallRevenue = Math.Round((roundsPlayed * (decimal)(3.5 + random.NextDouble() * 2.5)) + (isWeekend ? 120 : 60), 2);

        return new DailySalesMetric
        {
            Date = date,
            SourceSystemName = DemoSourceName,
            TotalRevenue = totalRevenue,
            FoodAndBeverageRevenue = foodAndBeverageRevenue,
            ProShopRevenue = proShopRevenue,
            AlcoholRevenue = alcoholRevenue,
            RangeBallRevenue = rangeBallRevenue,
            TransactionCount = Math.Max(roundsPlayed, roundsPlayed + random.Next(-10, 25)),
            SyncedAt = DateTime.UtcNow
        };
    }

    private static List<MaintenanceTask> BuildMaintenanceTasks(DateOnly today)
    {
        var createdAt = today.ToDateTime(new TimeOnly(7, 0));

        return new List<MaintenanceTask>
        {
            BuildMaintenanceTask("Repair irrigation leak on hole 4", "Irrigation", "High", MaintenanceTaskStatus.Open, createdAt),
            BuildMaintenanceTask("Topdress practice green", "Greens", "Medium", MaintenanceTaskStatus.Open, createdAt.AddHours(1)),
            BuildMaintenanceTask("Edge bunkers on front nine", "Fairways", "Medium", MaintenanceTaskStatus.Open, createdAt.AddHours(2)),
            BuildMaintenanceTask("Inspect cart path washout near hole 12", "Fairways", "High", MaintenanceTaskStatus.Open, createdAt.AddHours(3)),
            BuildMaintenanceTask("Replace broken tee marker set", "Clubhouse", "Low", MaintenanceTaskStatus.Open, createdAt.AddHours(4)),
            BuildMaintenanceTask("Adjust sprinkler head on hole 8", "Irrigation", "Medium", MaintenanceTaskStatus.Open, createdAt.AddHours(5)),
            BuildMaintenanceTask("Mow surrounds after rain delay", "Greens", "High", MaintenanceTaskStatus.Open, createdAt.AddHours(6)),
            BuildMaintenanceTask("Clean drainage inlet behind green 15", "Fairways", "Medium", MaintenanceTaskStatus.Open, createdAt.AddHours(7)),
            BuildMaintenanceTask("Service range ball washer", "Equipment", "Low", MaintenanceTaskStatus.Open, createdAt.AddHours(8)),
            BuildMaintenanceTask("Check clubhouse HVAC filter", "Clubhouse", "Low", MaintenanceTaskStatus.Open, createdAt.AddHours(9)),
            BuildMaintenanceTask("Fill divots on par 3 tees", "Fairways", "Medium", MaintenanceTaskStatus.Open, createdAt.AddHours(10)),
            BuildMaintenanceTask("Finish flower bed cleanup", "Clubhouse", "Low", MaintenanceTaskStatus.Completed, createdAt.AddDays(-2), createdAt.AddDays(-1))
        };
    }

    private static MaintenanceTask BuildMaintenanceTask(
        string title,
        string category,
        string priority,
        MaintenanceTaskStatus status,
        DateTime createdAt,
        DateTime? completedAt = null)
    {
        return new MaintenanceTask
        {
            Title = $"{DemoPrefix} {title}",
            Description = "Demo maintenance task for Course Commander sample data.",
            Category = category,
            Priority = priority,
            Status = status,
            CreatedAt = createdAt,
            StartedAt = status == MaintenanceTaskStatus.Completed ? createdAt.AddHours(1) : null,
            CompletedAt = completedAt,
            UpdatedAt = completedAt ?? createdAt
        };
    }

    private static List<EquipmentIssue> BuildEquipmentIssues(DateOnly today)
    {
        var reportedAt = today.ToDateTime(new TimeOnly(6, 30));

        return new List<EquipmentIssue>
        {
            new()
            {
                EquipmentName = $"{DemoPrefix} Greens Mower 2",
                IssueDescription = "Hydraulic warning light active during morning setup.",
                Severity = "Critical",
                Status = EquipmentIssueStatus.Open,
                ReportedAt = reportedAt,
                UpdatedAt = reportedAt
            },
            new()
            {
                EquipmentName = $"{DemoPrefix} Utility Cart 4",
                IssueDescription = "Battery range reduced after overnight charge.",
                Severity = "Medium",
                Status = EquipmentIssueStatus.WaitingOnParts,
                ReportedAt = reportedAt.AddDays(-1),
                StartedAt = reportedAt.AddDays(-1).AddHours(1),
                UpdatedAt = reportedAt.AddHours(-2),
                Notes = "Battery pack diagnosis completed.",
                PartName = "Replacement battery module",
                PartOrderedDate = reportedAt.AddDays(-1).AddHours(2),
                ExpectedArrivalDate = reportedAt.AddDays(2)
            },
            new()
            {
                EquipmentName = $"{DemoPrefix} Bunker Rake",
                IssueDescription = "Tine assembly repaired and returned to service.",
                Severity = "Low",
                Status = EquipmentIssueStatus.Resolved,
                ReportedAt = reportedAt.AddDays(-5),
                StartedAt = reportedAt.AddDays(-5).AddHours(2),
                CompletedAt = reportedAt.AddDays(-3),
                UpdatedAt = reportedAt.AddDays(-3)
            }
        };
    }

    private static string GetWeatherSummary(decimal rainfall, Random random)
    {
        if (rainfall > 0.25m)
        {
            return "Rain";
        }

        if (rainfall > 0)
        {
            return "Light rain";
        }

        var drySummaries = new[] { "Sunny", "Partly cloudy", "Cloudy" };
        return drySummaries[random.Next(drySummaries.Length)];
    }
}
