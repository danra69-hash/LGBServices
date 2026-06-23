using LGBApp.Backend.Models;

namespace LGBApp.Backend.Services;

public static class MoiFormMetadataHelper
{
    public static string? ReadRequiredExecutionDate(MOIForm? form)
    {
        if (form == null || string.IsNullOrWhiteSpace(form.FormDataJson))
            return null;

        var data = JsonHelper.Deserialize<Dictionary<string, object?>>(form.FormDataJson);
        var raw = data.GetValueOrDefault("requiredExecutionDate")
            ?? data.GetValueOrDefault("requiredDateOfExecution");
        var text = raw?.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
