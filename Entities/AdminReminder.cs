using System.ComponentModel.DataAnnotations;

namespace CourseCommander.Entities;

public class AdminReminder
{
    public int Id { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Other";
    public DateOnly DueDate { get; set; }
    public bool IsRecurring { get; set; }
    public string RecurrenceType { get; set; } = "Custom";
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
