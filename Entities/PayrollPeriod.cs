namespace CourseCommander.Entities;

public class PayrollPeriod
{
    public int Id { get; set; }
    public DateOnly PeriodStartDate { get; set; }
    public DateOnly PeriodEndDate { get; set; }
    public DateOnly PayrollDueDate { get; set; }
    public string Status { get; set; } = "Open";
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
}
