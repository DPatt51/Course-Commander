using CourseCommander.DTOs;
using CourseCommander.Entities;
using CourseCommander.Services;
using Microsoft.AspNetCore.Mvc;

namespace CourseCommander.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly AdminService _adminService;

    public AdminController(AdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet("reminders")]
    public async Task<ActionResult<IEnumerable<AdminReminder>>> GetReminders()
    {
        return await _adminService.GetRemindersAsync();
    }

    [HttpPost("reminders")]
    public async Task<ActionResult<AdminReminder>> CreateReminder(AdminReminder reminder)
    {
        if (string.IsNullOrWhiteSpace(reminder.Title))
        {
            return BadRequest("Title is required.");
        }

        var createdReminder = await _adminService.CreateReminderAsync(reminder);

        return CreatedAtAction(nameof(GetReminders), new { id = createdReminder.Id }, createdReminder);
    }

    [HttpPut("reminders/{id}/complete")]
    public async Task<ActionResult<AdminReminder>> CompleteReminder(int id)
    {
        var reminder = await _adminService.CompleteReminderAsync(id);

        if (reminder is null)
        {
            return NotFound();
        }

        return Ok(reminder);
    }

    [HttpGet("payroll-periods")]
    public async Task<ActionResult<IEnumerable<PayrollPeriod>>> GetPayrollPeriods()
    {
        return await _adminService.GetPayrollPeriodsAsync();
    }

    [HttpPost("payroll-periods")]
    public async Task<ActionResult<PayrollPeriod>> CreatePayrollPeriod(PayrollPeriod period)
    {
        if (period.PeriodStartDate > period.PeriodEndDate)
        {
            return BadRequest("Period start date must be before or equal to period end date.");
        }

        var createdPeriod = await _adminService.CreatePayrollPeriodAsync(period);

        return CreatedAtAction(nameof(GetPayrollPeriods), new { id = createdPeriod.Id }, createdPeriod);
    }

    [HttpPut("payroll-periods/{id}/submit")]
    public async Task<ActionResult<PayrollPeriod>> SubmitPayrollPeriod(int id)
    {
        var period = await _adminService.SubmitPayrollPeriodAsync(id);

        if (period is null)
        {
            return NotFound();
        }

        return Ok(period);
    }

    [HttpGet("payroll-current")]
    public async Task<ActionResult<PayrollPeriodSummaryDto>> GetCurrentPayrollPeriod([FromQuery] DateOnly? date)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Now);

        return await _adminService.GetCurrentPayrollPeriodAsync(selectedDate);
    }
}
