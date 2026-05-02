namespace CourseCommander.DTOs;

public class DailyBriefingDto
{
    public string DailyBriefing { get; set; } = string.Empty;
    public string? YesterdayRecap { get; set; }
    public string BriefingMode { get; set; } = "HistoricalRecap";
}
