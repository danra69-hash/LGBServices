namespace LGBApp.Backend.Services.Email;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string textBody, string? htmlBody = null, CancellationToken cancellationToken = default);
}
