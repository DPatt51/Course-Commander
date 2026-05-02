using CourseCommander.Entities;

namespace CourseCommander.DTOs;

public class PayrollPeriodSummaryDto
{
    public PayrollPeriod Period { get; set; } = new();
    public int DaysUntilDue { get; set; }
    public bool IsDueToday { get; set; }
    public bool IsOverdue { get; set; }
}
