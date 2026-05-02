namespace CourseCommander.Services;

public class DailyBriefingNotificationOptions
{
    public bool Enabled { get; set; } = false;
    public string DeliveryTime { get; set; } = "06:00";
    public string? RecipientEmail { get; set; }
    public SmtpOptions Smtp { get; set; } = new();
}

public class SmtpOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 25;
    public string? FromEmail { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool EnableSsl { get; set; } = false;
}
