using System.Text.Json;

namespace LGBApp.Backend.Services;

public static class FormDataHelper
{
    public static bool IsTruthy(object? value)
    {
        if (value is bool b)
            return b;

        if (value is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(je.GetString(), out var parsed) && parsed,
                JsonValueKind.Number => je.TryGetInt32(out var n) && n != 0,
                _ => false,
            };
        }

        return bool.TryParse(value?.ToString(), out var fromString) && fromString;
    }
}
