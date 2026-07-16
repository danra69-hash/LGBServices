namespace LGBApp.Backend.Services;

/// <summary>
/// Review #4 §6: optional page/pageSize (cap 200).
/// When both are omitted, callers must return the full result set — existing
/// frontends (package workboard, client portal, customer table) do not pass
/// page params and were silently truncated to 100 rows.
/// </summary>
public static class Pagination
{
    public const int DefaultPageSize = 100;
    public const int MaxPageSize = 200;

    /// <summary>True when the client requested pagination.</summary>
    public static bool IsRequested(int? page, int? pageSize) =>
        page.HasValue || pageSize.HasValue;

    public static (int Page, int PageSize) Normalize(int? page, int? pageSize)
    {
        var p = page is null or < 1 ? 1 : page.Value;
        var size = pageSize is null or < 1
            ? DefaultPageSize
            : Math.Min(pageSize.Value, MaxPageSize);
        return (p, size);
    }

    public static IQueryable<T> Apply<T>(IQueryable<T> query, int? page, int? pageSize)
    {
        if (!IsRequested(page, pageSize))
            return query;

        var (p, size) = Normalize(page, pageSize);
        return query.Skip((p - 1) * size).Take(size);
    }

    public static List<T> ApplyInMemory<T>(List<T> items, int? page, int? pageSize)
    {
        if (!IsRequested(page, pageSize))
            return items;

        var (p, size) = Normalize(page, pageSize);
        return items.Skip((p - 1) * size).Take(size).ToList();
    }
}
