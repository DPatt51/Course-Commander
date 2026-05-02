using System.ComponentModel.DataAnnotations;

namespace CourseCommander.Entities;

public enum MaintenanceTaskStatus
{
    Open,
    InProgress,
    Completed,
    Blocked
}

public class MaintenanceTask
{
    public int Id { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Greens";

    [Required]
    public string Priority { get; set; } = "Medium";

    public MaintenanceTaskStatus Status { get; set; } = MaintenanceTaskStatus.Open;
    public string? AssignedTo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? ExternalSourceName { get; set; }
    public string? ExternalTaskId { get; set; }
    public string? ExternalStatus { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public bool IsExternal { get; set; }
}
