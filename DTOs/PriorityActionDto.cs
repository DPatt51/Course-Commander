namespace CourseCommander.DTOs;

public class PriorityActionDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int PriorityScore { get; set; }
    public string Category { get; set; } = "Operations";
}
