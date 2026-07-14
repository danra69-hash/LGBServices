namespace LGBApp.Backend.Models;

public class PasswordResetOtp
{
    public int PasswordResetOtpId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int AttemptCount { get; set; }
}
