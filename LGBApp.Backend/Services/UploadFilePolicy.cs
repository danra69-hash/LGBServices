namespace LGBApp.Backend.Services;

public static class UploadFilePolicy
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".docx", ".xlsx", ".msg", ".eml",
    };

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".msg"] = "application/vnd.ms-outlook",
        [".eml"] = "message/rfc822",
    };

    public static bool TryResolve(string fileName, out string extension, out string contentType, out string? error)
    {
        extension = Path.GetExtension(fileName ?? string.Empty);
        contentType = "application/octet-stream";
        error = null;

        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            error = "File type not allowed. Use: pdf, png, jpg, jpeg, docx, xlsx, msg, eml.";
            return false;
        }

        contentType = ContentTypes[extension];
        return true;
    }
}
