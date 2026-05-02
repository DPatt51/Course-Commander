namespace CourseCommander.Entities;

public class DailyOperationMetric
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int RoundsPlayed { get; set; }
    public int CartRentals { get; set; }
    public decimal TotalRevenue { get; set; }
    public string? WeatherSummary { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
