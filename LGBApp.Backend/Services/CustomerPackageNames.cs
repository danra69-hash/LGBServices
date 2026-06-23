namespace LGBApp.Backend.Services;

public static class CustomerPackageNames
{
    public const string AddOnsOnly = "Add-ons only";

    public static bool IsAddOnsOnly(string? packageName) =>
        string.Equals(packageName?.Trim(), AddOnsOnly, StringComparison.OrdinalIgnoreCase);
}
