namespace LGBApp.Backend.Services;

public static class JobItemDocumentStorage
{
    public static string RootPath(IWebHostEnvironment env)
    {
        var configured = Environment.GetEnvironmentVariable("LGB_UPLOAD_ROOT");
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured);

        return Path.Combine(env.ContentRootPath, "uploads", "job-items");
    }

    public static string BuildStorageKey(int jobId, string folder, string fileName)
    {
        var safeFolder = SanitizeSegment(folder);
        var safeName = SanitizeFileName(fileName);
        return Path.Combine(jobId.ToString(), safeFolder, $"{Guid.NewGuid():N}_{safeName}");
    }

    public static string FullPath(IWebHostEnvironment env, string storageKey) =>
        Path.Combine(RootPath(env), storageKey.Replace('/', Path.DirectorySeparatorChar));

    public static async Task SaveAsync(IWebHostEnvironment env, string storageKey, Stream content)
    {
        var path = FullPath(env, storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file);
    }

    public static void DeleteFile(IWebHostEnvironment env, string storageKey)
    {
        var path = FullPath(env, storageKey);
        if (File.Exists(path))
            File.Delete(path);
    }

    private static string SanitizeSegment(string value)
    {
        var trimmed = (value ?? "supporting").Trim().ToLowerInvariant();
        return trimmed is "moi" or "moa" or "supporting" ? trimmed : "supporting";
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name))
            return "file";
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
