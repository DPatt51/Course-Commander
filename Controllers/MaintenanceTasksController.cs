using CourseCommander.Data;
using CourseCommander.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseCommander.Controllers;

[ApiController]
[Route("api/maintenance-tasks")]
public class MaintenanceTasksController : ControllerBase
{
    private readonly AppDbContext _context;

    public MaintenanceTasksController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MaintenanceTask>>> GetMaintenanceTasks()
    {
        return await _context.MaintenanceTasks
            .OrderByDescending(task => task.CreatedAt)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MaintenanceTask>> GetMaintenanceTask(int id)
    {
        var task = await _context.MaintenanceTasks.FindAsync(id);

        if (task is null)
        {
            return NotFound();
        }

        return task;
    }

    [HttpPost]
    public async Task<ActionResult<MaintenanceTask>> CreateMaintenanceTask(MaintenanceTask task)
    {
        var now = DateTime.UtcNow;
        task.CreatedAt = now;
        task.UpdatedAt = now;
        task.Status = MaintenanceTaskStatus.Open;
        task.StartedAt = null;
        task.CompletedAt = null;
        task.IsExternal = false;
        task.ExternalSourceName = null;
        task.ExternalTaskId = null;
        task.ExternalStatus = null;
        task.LastSyncedAt = null;

        _context.MaintenanceTasks.Add(task);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMaintenanceTask), new { id = task.Id }, task);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMaintenanceTask(int id, MaintenanceTask task)
    {
        if (id != task.Id)
        {
            return BadRequest();
        }

        var existingTask = await _context.MaintenanceTasks.FindAsync(id);

        if (existingTask is null)
        {
            return NotFound();
        }

        if (!CanTransition(existingTask.Status, task.Status))
        {
            return BadRequest($"Cannot change maintenance task status from {existingTask.Status} to {task.Status}.");
        }

        existingTask.Title = task.Title;
        existingTask.Description = task.Description;
        existingTask.Category = task.Category;
        existingTask.Priority = task.Priority;
        existingTask.AssignedTo = task.AssignedTo;

        if (existingTask.Status != task.Status)
        {
            ApplyStatusTransition(existingTask, task.Status);
        }

        existingTask.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("{id}/start")]
    public async Task<ActionResult<MaintenanceTask>> StartMaintenanceTask(int id)
    {
        var task = await _context.MaintenanceTasks.FindAsync(id);

        if (task is null)
        {
            return NotFound();
        }

        if (task.Status is not MaintenanceTaskStatus.Open and not MaintenanceTaskStatus.Blocked)
        {
            return BadRequest($"Cannot start a maintenance task with status {task.Status}.");
        }

        task.Status = MaintenanceTaskStatus.InProgress;
        task.StartedAt ??= DateTime.UtcNow;
        task.CompletedAt = null;
        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(task);
    }

    [HttpPut("{id}/complete")]
    public async Task<ActionResult<MaintenanceTask>> CompleteMaintenanceTask(int id)
    {
        var task = await _context.MaintenanceTasks.FindAsync(id);

        if (task is null)
        {
            return NotFound();
        }

        if (task.Status != MaintenanceTaskStatus.InProgress)
        {
            return BadRequest("Only in-progress maintenance tasks can be completed.");
        }

        task.Status = MaintenanceTaskStatus.Completed;
        task.CompletedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(task);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMaintenanceTask(int id)
    {
        var task = await _context.MaintenanceTasks.FindAsync(id);

        if (task is null)
        {
            return NotFound();
        }

        _context.MaintenanceTasks.Remove(task);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static bool CanTransition(MaintenanceTaskStatus currentStatus, MaintenanceTaskStatus nextStatus)
    {
        if (currentStatus == nextStatus)
        {
            return true;
        }

        return currentStatus switch
        {
            MaintenanceTaskStatus.Open => nextStatus is MaintenanceTaskStatus.InProgress or MaintenanceTaskStatus.Blocked,
            MaintenanceTaskStatus.Blocked => nextStatus is MaintenanceTaskStatus.Open or MaintenanceTaskStatus.InProgress,
            MaintenanceTaskStatus.InProgress => nextStatus is MaintenanceTaskStatus.Completed or MaintenanceTaskStatus.Blocked,
            _ => false
        };
    }

    private static void ApplyStatusTransition(MaintenanceTask task, MaintenanceTaskStatus nextStatus)
    {
        var now = DateTime.UtcNow;
        task.Status = nextStatus;

        if (nextStatus == MaintenanceTaskStatus.InProgress)
        {
            task.StartedAt ??= now;
            task.CompletedAt = null;
        }
        else if (nextStatus == MaintenanceTaskStatus.Completed)
        {
            task.CompletedAt = now;
        }
        else
        {
            task.CompletedAt = null;
        }
    }
}
