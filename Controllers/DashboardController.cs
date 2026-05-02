using CourseCommander.Data;
using CourseCommander.DTOs;
using CourseCommander.Entities;
using CourseCommander.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseCommander.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly InsightService _insightService;
    private readonly DashboardMetricService _dashboardMetricService;
    private readonly GrowingDegreeDayService _gddService;
    private readonly DailyBriefingService _dailyBriefingService;
    private readonly AlertService _alertService;
    private readonly PriorityService _priorityService;
    private readonly FandBAnalyticsService _fandBAnalyticsService;
    private readonly AgronomyService _agronomyService;

    public DashboardController(
        AppDbContext context,
        InsightService insightService,
        DashboardMetricService dashboardMetricService,
        GrowingDegreeDayService gddService,
        DailyBriefingService dailyBriefingService,
        AlertService alertService,
        PriorityService priorityService,
        FandBAnalyticsService fandBAnalyticsService,
        AgronomyService agronomyService)
    {
        _context = context;
        _insightService = insightService;
        _dashboardMetricService = dashboardMetricService;
        _gddService = gddService;
        _dailyBriefingService = dailyBriefingService;
        _alertService = alertService;
        _priorityService = priorityService;
        _fandBAnalyticsService = fandBAnalyticsService;
        _agronomyService = agronomyService;
    }

    [HttpGet("{date}")]
    public async Task<IActionResult> GetDashboardSummary(string date)
    {
        if (!DateOnly.TryParse(date, out var selectedDate))
        {
            return BadRequest("Please provide a valid date, such as 2026-04-28.");
        }

        var metrics = await _dashboardMetricService.GetDailyMetricSummaryAsync(selectedDate);
        var selectedDateTime = selectedDate.ToDateTime(TimeOnly.MinValue);
        var dailyGdd = await _gddService.GetDailyGddAsync(selectedDateTime);
        var past30DaysGdd = await _gddService.GetRangeGddAsync(selectedDateTime.AddDays(-29), selectedDateTime);
        var yearToDateGdd = await _gddService.GetRangeGddAsync(new DateTime(selectedDate.Year, 1, 1), selectedDateTime);

        var openMaintenanceTaskCount = await _context.MaintenanceTasks
            .CountAsync(task => task.Status != MaintenanceTaskStatus.Completed);

        var criticalMaintenanceTaskCount = await _context.MaintenanceTasks
            .CountAsync(task => task.Priority == "Critical" && task.Status != MaintenanceTaskStatus.Completed);

        var openEquipmentIssueCount = await _context.EquipmentIssues
            .CountAsync(issue => issue.Status != EquipmentIssueStatus.Resolved);

        var criticalEquipmentIssueCount = await _context.EquipmentIssues
            .CountAsync(issue =>
                issue.Severity == "Critical" &&
                issue.Status != EquipmentIssueStatus.Resolved);

        var insights = await _insightService.GenerateDailyInsightsAsync(selectedDate);
        var briefing = await _dailyBriefingService.GenerateDailyBriefingAsync(
            selectedDate,
            openMaintenanceTaskCount,
            criticalEquipmentIssueCount);
        var alerts = await _alertService.GenerateAlertsAsync(
            selectedDate,
            openMaintenanceTaskCount,
            criticalEquipmentIssueCount);
        var priorities = await _priorityService.GetPrioritiesAsync(selectedDate, alerts, insights);
        var fandBAnalytics = await _fandBAnalyticsService.GetAnalyticsAsync(selectedDate);
        var turfConditions = await _agronomyService.GetMoistureSummaryAsync(selectedDate);

        var summary = new DashboardSummaryDto
        {
            Date = selectedDate,
            DailyBriefing = briefing.DailyBriefing,
            YesterdayRecap = briefing.YesterdayRecap,
            BriefingMode = briefing.BriefingMode,
            RoundsPlayed = metrics.RoundsPlayed ?? 0,
            CartRentals = metrics.CartRentals,
            TotalRevenue = metrics.TotalRevenue,
            ProShopRevenue = metrics.ProShopRevenue,
            FoodAndBeverageRevenue = metrics.FoodAndBeverageRevenue,
            AlcoholRevenue = metrics.AlcoholRevenue,
            RangeBallRevenue = metrics.RangeBallRevenue,
            FandBAnalytics = fandBAnalytics,
            WeatherSummary = metrics.WeatherSummary,
            DailyGdd = dailyGdd,
            Past30DaysGdd = past30DaysGdd,
            YearToDateGdd = yearToDateGdd,
            TurfConditions = turfConditions,
            OpenMaintenanceTaskCount = openMaintenanceTaskCount,
            CriticalMaintenanceTaskCount = criticalMaintenanceTaskCount,
            OpenEquipmentIssueCount = openEquipmentIssueCount,
            CriticalEquipmentIssueCount = criticalEquipmentIssueCount,
            SourceSystems = new DashboardSourceSystemsDto
            {
                Play = metrics.PlaySourceSystemName,
                Sales = metrics.SalesSourceSystemName,
                Weather = metrics.WeatherSourceSystemName
            },
            Alerts = alerts,
            Priorities = priorities,
            Insights = insights
        };

        return Ok(summary);
    }
}
