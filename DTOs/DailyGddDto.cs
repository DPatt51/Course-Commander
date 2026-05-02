namespace CourseCommander.DTOs;

public class DailyGddDto
{
    public DateOnly Date { get; set; }
    public decimal HighTemp { get; set; }
    public decimal LowTemp { get; set; }
    public decimal BaseTemperature { get; set; }
    public decimal Gdd { get; set; }
}
