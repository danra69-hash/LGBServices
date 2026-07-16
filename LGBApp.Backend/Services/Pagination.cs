namespace LGBApp.Backend.Services;

/// <summary>Review #4 §6: shared page/pageSize normalization (cap 200).</summary>
public static class Pagination
{
    public const int DefaultPageSize = 100;
    public const int MaxPageSize = 200;

    public static (int Page, int PageSize) Normalize(int? page, int? pageSize)
    {
        var p = page is null or < 1 ? 1 : page.Value;
        var size = pageSize is null or < 1
            ? DefaultPageSize
            : Math.Min(pageSize.Value, MaxPageSize);
        return (p, size);
    }
}
