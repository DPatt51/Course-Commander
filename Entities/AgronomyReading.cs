using System.ComponentModel.DataAnnotations;

namespace CourseCommander.Entities;

public class AgronomyReading
{
    public int Id { get; set; }
    public DateTime DateTime { get; set; } = System.DateTime.UtcNow;

    [Required]
    public string MeasurementType { get; set; } = "Moisture";

    [Required]
    public string Location { get; set; } = string.Empty;

    public string Zone { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string SourceSystemName { get; set; } = "Manual Entry";
    public string? ExternalReadingId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = System.DateTime.UtcNow;
    public DateTime? SyncedAt { get; set; }
}
