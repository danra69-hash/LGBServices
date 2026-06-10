namespace LGBApp.Backend.Models;

public static class UserRoles
{
    public const string Admin = "Admin";
    public const string User = "User";
    public const string ClientAdmin = "ClientAdmin";
    public const string Client = "Client";

    public static readonly string[] All = [Admin, User, ClientAdmin, Client];
    public static readonly string[] Internal = [Admin, User];
    public static readonly string[] External = [ClientAdmin, Client];

    public static bool IsValid(string? role) =>
        !string.IsNullOrWhiteSpace(role) && All.Contains(role, StringComparer.OrdinalIgnoreCase);

    public static bool IsInternalRole(string? role) =>
        Internal.Contains(role ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    public static bool IsExternalRole(string? role) =>
        External.Contains(role ?? string.Empty, StringComparer.OrdinalIgnoreCase);
}
