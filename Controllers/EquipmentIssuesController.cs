using CourseCommander.Data;
using CourseCommander.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseCommander.Controllers;

[ApiController]
[Route("api/equipment-issues")]
public class EquipmentIssuesController : ControllerBase
{
    private readonly AppDbContext _context;

    public EquipmentIssuesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EquipmentIssue>>> GetEquipmentIssues()
    {
        return await _context.EquipmentIssues
            .OrderByDescending(issue => issue.ReportedAt)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<EquipmentIssue>> GetEquipmentIssue(int id)
    {
        var issue = await _context.EquipmentIssues.FindAsync(id);

        if (issue is null)
        {
            return NotFound();
        }

        return issue;
    }

    [HttpPost]
    public async Task<ActionResult<EquipmentIssue>> CreateEquipmentIssue(EquipmentIssue issue)
    {
        var now = DateTime.UtcNow;
        issue.ReportedAt = now;
        issue.UpdatedAt = now;
        issue.Status = EquipmentIssueStatus.Open;
        issue.StartedAt = null;
        issue.CompletedAt = null;
        issue.IsExternal = false;
        issue.ExternalSourceName = null;
        issue.ExternalIssueId = null;
        issue.ExternalStatus = null;
        issue.LastSyncedAt = null;

        _context.EquipmentIssues.Add(issue);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetEquipmentIssue), new { id = issue.Id }, issue);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEquipmentIssue(int id, EquipmentIssue issue)
    {
        if (id != issue.Id)
        {
            return BadRequest();
        }

        var existingIssue = await _context.EquipmentIssues.FindAsync(id);

        if (existingIssue is null)
        {
            return NotFound();
        }

        if (!CanTransition(existingIssue.Status, issue.Status))
        {
            return BadRequest($"Cannot change equipment issue status from {existingIssue.Status} to {issue.Status}.");
        }

        existingIssue.EquipmentName = issue.EquipmentName;
        existingIssue.IssueDescription = issue.IssueDescription;
        existingIssue.Severity = issue.Severity;
        existingIssue.AssignedTo = issue.AssignedTo;
        existingIssue.Notes = issue.Notes;
        existingIssue.PartName = issue.PartName;
        existingIssue.PartOrderedDate = issue.PartOrderedDate;
        existingIssue.ExpectedArrivalDate = issue.ExpectedArrivalDate;

        if (existingIssue.Status != issue.Status)
        {
            ApplyStatusTransition(existingIssue, issue.Status);
        }

        existingIssue.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("{id}/start")]
    public async Task<ActionResult<EquipmentIssue>> StartEquipmentIssue(int id)
    {
        var issue = await _context.EquipmentIssues.FindAsync(id);

        if (issue is null)
        {
            return NotFound();
        }

        if (issue.Status != EquipmentIssueStatus.Open)
        {
            return BadRequest($"Cannot start an equipment issue with status {issue.Status}.");
        }

        issue.Status = EquipmentIssueStatus.InProgress;
        issue.StartedAt ??= DateTime.UtcNow;
        issue.CompletedAt = null;
        issue.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(issue);
    }

    [HttpPut("{id}/waiting-on-parts")]
    public async Task<ActionResult<EquipmentIssue>> MarkWaitingOnParts(int id)
    {
        var issue = await _context.EquipmentIssues.FindAsync(id);

        if (issue is null)
        {
            return NotFound();
        }

        if (issue.Status != EquipmentIssueStatus.InProgress)
        {
            return BadRequest("Only in-progress equipment issues can be marked waiting on parts.");
        }

        issue.Status = EquipmentIssueStatus.WaitingOnParts;
        issue.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(issue);
    }

    [HttpPut("{id}/resolve")]
    public async Task<ActionResult<EquipmentIssue>> ResolveEquipmentIssue(int id)
    {
        var issue = await _context.EquipmentIssues.FindAsync(id);

        if (issue is null)
        {
            return NotFound();
        }

        if (issue.Status is not EquipmentIssueStatus.InProgress and not EquipmentIssueStatus.WaitingOnParts)
        {
            return BadRequest("Only in-progress or waiting-on-parts equipment issues can be resolved.");
        }

        issue.Status = EquipmentIssueStatus.Resolved;
        issue.CompletedAt = DateTime.UtcNow;
        issue.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(issue);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEquipmentIssue(int id)
    {
        var issue = await _context.EquipmentIssues.FindAsync(id);

        if (issue is null)
        {
            return NotFound();
        }

        _context.EquipmentIssues.Remove(issue);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static bool CanTransition(EquipmentIssueStatus currentStatus, EquipmentIssueStatus nextStatus)
    {
        if (currentStatus == nextStatus)
        {
            return true;
        }

        return currentStatus switch
        {
            EquipmentIssueStatus.Open => nextStatus == EquipmentIssueStatus.InProgress,
            EquipmentIssueStatus.InProgress => nextStatus is EquipmentIssueStatus.WaitingOnParts or EquipmentIssueStatus.Resolved,
            EquipmentIssueStatus.WaitingOnParts => nextStatus == EquipmentIssueStatus.Resolved,
            _ => false
        };
    }

    private static void ApplyStatusTransition(EquipmentIssue issue, EquipmentIssueStatus nextStatus)
    {
        var now = DateTime.UtcNow;
        issue.Status = nextStatus;

        if (nextStatus == EquipmentIssueStatus.InProgress)
        {
            issue.StartedAt ??= now;
            issue.CompletedAt = null;
        }
        else if (nextStatus == EquipmentIssueStatus.Resolved)
        {
            issue.CompletedAt = now;
        }
        else if (nextStatus != EquipmentIssueStatus.WaitingOnParts)
        {
            issue.CompletedAt = null;
        }
    }
}
