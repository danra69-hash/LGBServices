using Microsoft.AspNetCore.Mvc;

namespace LGBApp.Backend.Services;

public static class FormConcurrencyHelper
{
    public static ActionResult? CheckExpectedUpdatedAt(DateTime storedUtc, string? expectedUpdatedAt)
    {
        if (string.IsNullOrWhiteSpace(expectedUpdatedAt))
            return null;

        if (!DateTime.TryParse(
                expectedUpdatedAt,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var expected))
        {
            return null;
        }

        var expectedUtc = expected.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(expected, DateTimeKind.Utc)
            : expected.ToUniversalTime();
        var deltaSeconds = Math.Abs((storedUtc - expectedUtc).TotalSeconds);
        if (deltaSeconds <= 1)
            return null;

        return new ConflictObjectResult(new
        {
            message = "This form was updated by someone else. Refresh and try again.",
            updatedAt = storedUtc.ToString("O"),
        });
    }
}
