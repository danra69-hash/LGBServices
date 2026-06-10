using System.Text.Json;

namespace LGBApp.Backend.Services;

public static class JsonHelper
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return CreateDefault<T>();

        return JsonSerializer.Deserialize<T>(json, Options) ?? CreateDefault<T>();
    }

    private static T CreateDefault<T>()
    {
        if (typeof(T) == typeof(List<string>))
            return (T)(object)new List<string>();
        if (typeof(T) == typeof(Dictionary<string, int>))
            return (T)(object)new Dictionary<string, int>();

        return default!;
    }
}
