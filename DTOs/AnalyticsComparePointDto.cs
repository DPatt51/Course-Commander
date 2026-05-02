namespace CourseCommander.DTOs;

public class AnalyticsComparePointDto
{
    public DateOnly Date { get; set; }
    public Dictionary<string, decimal?> Values { get; set; } = new();
}
