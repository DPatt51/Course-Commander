namespace CourseCommander.Entities;

public class DailyWeatherMetric
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public decimal HighTemp { get; set; }
    public decimal LowTemp { get; set; }
    public decimal RainfallInches { get; set; }
    public string WeatherSummary { get; set; } = string.Empty;
    public string SourceSystemName { get; set; } = string.Empty;
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
}
