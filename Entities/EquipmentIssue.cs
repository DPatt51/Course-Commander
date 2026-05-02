using System.ComponentModel.DataAnnotations;

namespace CourseCommander.Entities;

public enum EquipmentIssueStatus
{
    Open,
    InProgress,
    WaitingOnParts,
    Resolved
}

public class EquipmentIssue
{
    public int Id { get; set; }

    [Required]
    public string EquipmentName { get; set; } = string.Empty;

    public string IssueDescription { get; set; } = string.Empty;

    [Required]
    public string Severity { get; set; } = "Medium";

    public EquipmentIssueStatus Status { get; set; } = EquipmentIssueStatus.Open;
    public string? AssignedTo { get; set; }
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public string? PartName { get; set; }
    public DateTime? PartOrderedDate { get; set; }
    public DateTime? ExpectedArrivalDate { get; set; }
    public string? ExternalSourceName { get; set; }
    public string? ExternalIssueId { get; set; }
    public string? ExternalStatus { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public bool IsExternal { get; set; }
}
