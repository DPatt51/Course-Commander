using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace CourseCommander.Services;

public class NotificationService
{
    private readonly DailyBriefingNotificationOptions _options;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IOptions<DailyBriefingNotificationOptions> options,
        ILogger<NotificationService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string subject, string message, CancellationToken cancellationToken)
    {
        if (CanSendEmail())
        {
            await SendEmailAsync(subject, message, cancellationToken);
            return;
        }

        _logger.LogInformation(
            "Daily briefing notification fallback:{Message}",
            $"{Environment.NewLine}{message}");
    }

    private async Task SendEmailAsync(string subject, string message, CancellationToken cancellationToken)
    {
        using var mailMessage = new MailMessage
        {
            From = new MailAddress(_options.Smtp.FromEmail!),
            Subject = subject,
            Body = message,
            IsBodyHtml = false
        };

        mailMessage.To.Add(_options.RecipientEmail!);

        using var smtpClient = new SmtpClient(_options.Smtp.Host!, _options.Smtp.Port)
        {
            EnableSsl = _options.Smtp.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.Smtp.Username))
        {
            smtpClient.Credentials = new NetworkCredential(_options.Smtp.Username, _options.Smtp.Password);
        }

        await smtpClient.SendMailAsync(mailMessage, cancellationToken);
        _logger.LogInformation("Daily briefing email sent to {RecipientEmail}.", _options.RecipientEmail);
    }

    private bool CanSendEmail()
    {
        return !string.IsNullOrWhiteSpace(_options.RecipientEmail) &&
            !string.IsNullOrWhiteSpace(_options.Smtp.Host) &&
            !string.IsNullOrWhiteSpace(_options.Smtp.FromEmail);
    }
}
