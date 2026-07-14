using System.Text.RegularExpressions;

namespace LGBApp.Backend.Services;

public static class PasswordPolicy
{
    public const int MinLength = 6;

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string NormalizeEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && EmailRegex.IsMatch(email.Trim());

    public static bool MeetsMinLength(string? password) =>
        !string.IsNullOrEmpty(password) && password.Length >= MinLength;
}
