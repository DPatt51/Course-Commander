using CourseCommander.Data;
using CourseCommander.DTOs;
using CourseCommander.Entities;
using Microsoft.EntityFrameworkCore;

namespace CourseCommander.Services;

public class AdminService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AdminService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<List<AdminReminder>> GetRemindersAsync()
    {
        return await _context.AdminReminders
            .OrderBy(reminder => reminder.IsCompleted)
            .ThenBy(reminder => reminder.DueDate)
            .ThenBy(reminder => reminder.Title)
            .ToListAsync();
    }

    public async Task<List<AdminReminder>> GetUpcomingRemindersAsync(DateOnly date, int limit = 5)
    {
        return await _context.AdminReminders
            .Where(reminder => !reminder.IsCompleted && reminder.DueDate >= date)
            .OrderBy(reminder => reminder.DueDate)
            .ThenBy(reminder => reminder.Title)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<AdminReminder> CreateReminderAsync(AdminReminder reminder)
    {
        reminder.CreatedAt = DateTime.UtcNow;
        reminder.IsCompleted = false;
        reminder.CompletedAt = null;

        _context.AdminReminders.Add(reminder);
        await _context.SaveChangesAsync();

        return reminder;
    }

    public async Task<AdminReminder?> CompleteReminderAsync(int id)
    {
        var reminder = await _context.AdminReminders.FindAsync(id);

        if (reminder is null)
        {
            return null;
        }

        reminder.IsCompleted = true;
        reminder.CompletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return reminder;
    }

    public async Task<List<PayrollPeriod>> GetPayrollPeriodsAsync()
    {
        return await _context.PayrollPeriods
            .OrderByDescending(period => period.PeriodStartDate)
            .ToListAsync();
    }

    public async Task<PayrollPeriod> CreatePayrollPeriodAsync(PayrollPeriod period)
    {
        period.CreatedAt = DateTime.UtcNow;
        period.Status = string.IsNullOrWhiteSpace(period.Status) ? "Open" : period.Status;
        period.SubmittedAt = null;

        _context.PayrollPeriods.Add(period);
        await _context.SaveChangesAsync();

        return period;
    }

    public async Task<PayrollPeriod?> SubmitPayrollPeriodAsync(int id)
    {
        var period = await _context.PayrollPeriods.FindAsync(id);

        if (period is null)
        {
            return null;
        }

        period.Status = "Submitted";
        period.SubmittedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return period;
    }

    public async Task<PayrollPeriodSummaryDto> GetCurrentPayrollPeriodAsync(DateOnly date)
    {
        var periodStart = GetCurrentPeriodStartDate(date);
        var periodEnd = periodStart.AddDays(13);
        var payrollDueDate = periodEnd.AddDays(GetPayrollDueDaysAfterPeriodEnd());

        var period = await _context.PayrollPeriods
            .FirstOrDefaultAsync(payrollPeriod =>
                payrollPeriod.PeriodStartDate == periodStart &&
                payrollPeriod.PeriodEndDate == periodEnd);

        if (period is null)
        {
            period = new PayrollPeriod
            {
                PeriodStartDate = periodStart,
                PeriodEndDate = periodEnd,
                PayrollDueDate = payrollDueDate,
                Status = "Open",
                CreatedAt = DateTime.UtcNow
            };

            _context.PayrollPeriods.Add(period);
            await _context.SaveChangesAsync();
        }

        var daysUntilDue = period.PayrollDueDate.DayNumber - date.DayNumber;

        return new PayrollPeriodSummaryDto
        {
            Period = period,
            DaysUntilDue = daysUntilDue,
            IsDueToday = daysUntilDue == 0,
            IsOverdue = daysUntilDue < 0 && period.Status == "Open"
        };
    }

    private DateOnly GetCurrentPeriodStartDate(DateOnly date)
    {
        var scheduleStart = GetPayrollScheduleStartDate(date);
        var daysSinceStart = date.DayNumber - scheduleStart.DayNumber;
        var periodIndex = (int)Math.Floor(daysSinceStart / 14.0);

        return scheduleStart.AddDays(periodIndex * 14);
    }

    private DateOnly GetPayrollScheduleStartDate(DateOnly date)
    {
        var configuredStart = _configuration["Admin:PayrollStartDate"];

        if (DateOnly.TryParse(configuredStart, out var startDate))
        {
            return startDate;
        }

        var firstDayOfYear = new DateOnly(date.Year, 1, 1);
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)firstDayOfYear.DayOfWeek + 7) % 7;

        return firstDayOfYear.AddDays(daysUntilMonday);
    }

    private int GetPayrollDueDaysAfterPeriodEnd()
    {
        return int.TryParse(_configuration["Admin:PayrollDueDaysAfterPeriodEnd"], out var days)
            ? days
            : 2;
    }
}
