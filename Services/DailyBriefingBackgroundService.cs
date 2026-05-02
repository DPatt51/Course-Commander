using CourseCommander.Data;
using CourseCommander.DTOs;
using CourseCommander.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CourseCommander.Services;

public class DailyBriefingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DailyBriefingNotificationOptions _options;
    private readonly ILogger<DailyBriefingBackgroundService> _logger;

    public DailyBriefingBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<DailyBriefingNotificationOptions> options,
        ILogger<DailyBriefingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRun();
            _logger.LogInformation("Daily briefing background service scheduled in {Delay}.", delay);

            await Task.Delay(delay, stoppingToken);

            if (!_options.Enabled)
            {
                _logger.LogInformation("Daily briefing notifications are disabled.");
                continue;
            }

            try
            {
                await SendDailyBriefingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Daily briefing notification failed.");
            }
        }
    }

    private async Task SendDailyBriefingAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var briefingService = scope.ServiceProvider.GetRequiredService<DailyBriefingService>();
        var alertService = scope.ServiceProvider.GetRequiredService<AlertService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
        var today = DateOnly.FromDateTime(DateTime.Now);

        var openMaintenanceTaskCount = await dbContext.MaintenanceTasks
            .CountAsync(task => task.Status != MaintenanceTaskStatus.Completed, cancellationToken);

        var criticalEquipmentIssueCount = await dbContext.EquipmentIssues
            .CountAsync(issue =>
                issue.Severity == "Critical" &&
                issue.Status != EquipmentIssueStatus.Resolved,
                cancellationToken);

        var briefing = await briefingService.GenerateDailyBriefingAsync(
            today,
            openMaintenanceTaskCount,
            criticalEquipmentIssueCount);

        var alerts = await alertService.GenerateAlertsAsync(
            today,
            openMaintenanceTaskCount,
            criticalEquipmentIssueCount);

        var subject = $"Daily Operations Briefing – {today:MMMM d, yyyy}";
        var message = FormatDailyBriefingMessage(subject, briefing, alerts);

        await notificationService.SendAsync(subject, message, cancellationToken);
    }

    private TimeSpan GetDelayUntilNextRun()
    {
        var now = DateTime.Now;
        var deliveryTime = GetDeliveryTime();
        var nextRun = now.Date.Add(deliveryTime.ToTimeSpan());

        if (nextRun <= now)
        {
            nextRun = nextRun.AddDays(1);
        }

        return nextRun - now;
    }

    private TimeOnly GetDeliveryTime()
    {
        return TimeOnly.TryParse(_options.DeliveryTime, out var deliveryTime)
            ? deliveryTime
            : new TimeOnly(6, 0);
    }

    private static string FormatDailyBriefingMessage(
        string title,
        DailyBriefingDto briefing,
        List<AlertDto> alerts)
    {
        var actionItems = alerts.Count == 0
            ? "No urgent action items for this date."
            : string.Join(
                Environment.NewLine,
                alerts.Select(alert =>
                    $"- [{alert.Severity}] {alert.Category}: {CleanLabel(alert.Title)}. {CleanLabel(alert.Message)} Recommended action: {CleanLabel(alert.RecommendedAction)}"));

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            title,
            $"Today's Outlook{Environment.NewLine}{CleanLabel(briefing.DailyBriefing)}",
            $"Yesterday's Recap{Environment.NewLine}{CleanLabel(briefing.YesterdayRecap ?? "No recap available.")}",
            $"Action Items{Environment.NewLine}{CleanLabel(actionItems)}");
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
