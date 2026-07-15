namespace LGBApp.Backend.Services;

/// <summary>D1: per-job (or per-unit) choice — run MOI/MOA or send a note to Sharon.</summary>
public static class JobWorkflowModes
{
    public const string Unset = "";
    public const string MoiMoa = "MoiMoa";
    public const string AdminBypass = "AdminBypass";

    public static bool IsMoiMoa(string? mode) =>
        string.Equals(mode, MoiMoa, StringComparison.OrdinalIgnoreCase);

    public static bool IsAdminBypass(string? mode) =>
        string.Equals(mode, AdminBypass, StringComparison.OrdinalIgnoreCase);
}
