namespace CourseCommander.DTOs;

public class AlertDto
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public string Category { get; set; } = "Operations";
    public string RecommendedAction { get; set; } = string.Empty;
    public List<string> RelatedItems { get; set; } = new();
}
