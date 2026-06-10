using System.Globalization;

namespace LGBApp.Backend.Services;

public static class DateOnlyHelper
{
    private static readonly string[] InputFormats =
    [
        "yyyy-MM-dd",
        "dd/MM/yyyy",
        "d/M/yyyy",
        "dd-MM-yyyy",
        "d-M-yyyy",
    ];

    public const string DisplayFormat = "dd/MM/yyyy";

    public static DateTime? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTime.TryParseExact(value.Trim(), InputFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
            return DateTime.SpecifyKind(exact.Date, DateTimeKind.Utc);

        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        return null;
    }

    public static string? Format(DateTime? value)
    {
        if (!value.HasValue)
            return null;

        return DateOnly.FromDateTime(value.Value).ToString(DisplayFormat, CultureInfo.InvariantCulture);
    }
}
