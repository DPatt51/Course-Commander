namespace CourseCommander.Entities;

public class SyncRun
{
    public int Id { get; set; }
    public int SourceSystemId { get; set; }
    public SourceSystem SourceSystem { get; set; } = null!;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "InProgress";
    public string Message { get; set; } = string.Empty;
    public int RecordsProcessed { get; set; }
}
