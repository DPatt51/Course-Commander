namespace CourseCommander.DTOs;

public class TrendPointDto
{
    public DateOnly Date { get; set; }
    public decimal Value { get; set; }
    public decimal? PercentChangeFromPreviousDay { get; set; }
}
