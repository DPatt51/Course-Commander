namespace CourseCommander.DTOs;

public class GddSummaryDto
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal BaseTemperature { get; set; }
    public decimal TotalGdd { get; set; }
    public decimal AverageDailyGdd { get; set; }
    public int DaysIncluded { get; set; }
    public List<DailyGddDto> DailyValues { get; set; } = new();
}
