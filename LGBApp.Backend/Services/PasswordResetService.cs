using System.Security.Cryptography;
using System.Text;
using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Services.Email;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public class PasswordResetService
{
    public const int OtpLength = 6;
    public static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    public const int MaxVerifyAttempts = 8;

    private readonly AppDbContext _context;
    private readonly IEmailSender _email;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(AppDbContext context, IEmailSender email, ILogger<PasswordResetService> logger)
    {
        _context = context;
        _email = email;
        _logger = logger;
    }

    public async Task RequestOtpAsync(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        var user = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalized);
        if (user == null)
        {
            // No enumeration — pretend success.
            _logger.LogInformation("Password reset requested for unknown email {Email}", normalized);
            return;
        }

        var latest = await _context.PasswordResetOtps
            .Where(o => o.Email == normalized && o.ConsumedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();
        if (latest != null && latest.CreatedAt > DateTime.UtcNow - ResendCooldown)
            throw new DomainException("Please wait a minute before requesting another code.", StatusCodes.Status429TooManyRequests);

        // Invalidate prior active codes
        var active = await _context.PasswordResetOtps
            .Where(o => o.Email == normalized && o.ConsumedAt == null)
            .ToListAsync();
        foreach (var row in active)
            row.ConsumedAt = DateTime.UtcNow;

        var code = GenerateOtp();
        _context.PasswordResetOtps.Add(new PasswordResetOtp
        {
            Email = normalized,
            CodeHash = HashCode(code),
            ExpiresAt = DateTime.UtcNow.Add(OtpLifetime),
            CreatedAt = DateTime.UtcNow,
            AttemptCount = 0,
        });
        await _context.SaveChangesAsync();

        var subject = "Your LGB Services password reset code";
        var text =
            $"Hi {user.Name},\n\n" +
            $"Your password reset code is: {code}\n\n" +
            $"It expires in {(int)OtpLifetime.TotalMinutes} minutes.\n" +
            "If you did not request this, you can ignore this email.\n";
        var html =
            $"<p>Hi {System.Net.WebUtility.HtmlEncode(user.Name)},</p>" +
            $"<p>Your password reset code is: <strong style=\"font-size:1.25rem;letter-spacing:0.15em\">{code}</strong></p>" +
            $"<p>It expires in {(int)OtpLifetime.TotalMinutes} minutes.</p>" +
            "<p>If you did not request this, you can ignore this email.</p>";

        await _email.SendAsync(user.Email, subject, text, html);
    }

    public async Task ResetPasswordAsync(string email, string code, string newPassword)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var codeDigits = (code ?? string.Empty).Trim();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalized);
        if (user == null)
            throw new DomainException("Invalid or expired reset code.");

        var otp = await _context.PasswordResetOtps
            .Where(o => o.Email == normalized && o.ConsumedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp == null || otp.ExpiresAt < DateTime.UtcNow)
            throw new DomainException("Invalid or expired reset code.");

        if (otp.AttemptCount >= MaxVerifyAttempts)
            throw new DomainException("Too many attempts. Request a new code.", StatusCodes.Status429TooManyRequests);

        otp.AttemptCount += 1;
        if (!SecureEquals(otp.CodeHash, HashCode(codeDigits)))
        {
            await _context.SaveChangesAsync();
            throw new DomainException("Invalid or expired reset code.");
        }

        if (newPassword.Length < 6)
            throw new DomainException("New password must be at least 6 characters.");

        user.PasswordHash = PasswordHasher.Hash(newPassword);
        user.MustChangePassword = false;
        otp.ConsumedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    private static string GenerateOtp()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString($"D{OtpLength}");
    }

    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim()));
        return Convert.ToHexString(bytes);
    }

    private static bool SecureEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        if (aBytes.Length != bBytes.Length)
            return false;
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
