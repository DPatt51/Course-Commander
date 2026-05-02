namespace CourseCommander.Entities;

public class DailyPlayMetric
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string SourceSystemName { get; set; } = string.Empty;
    public int RoundsPlayed { get; set; }
    public int CartRentals { get; set; }
    public int NineHoleRounds { get; set; }
    public int EighteenHoleRounds { get; set; }
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
}
