namespace LGBApp.Backend.Services.Email;

/// <summary>Dev / no-key fallback: writes outbound mail to the application log.</summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(
        string toEmail,
        string subject,
        string textBody,
        string? htmlBody = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "EMAIL (logging sink) To={To} Subject={Subject}\n{Body}",
            toEmail,
            subject,
            textBody);
        return Task.CompletedTask;
    }
}
