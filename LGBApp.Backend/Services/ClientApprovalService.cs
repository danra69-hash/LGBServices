using LGBApp.Backend.Models;

namespace LGBApp.Backend.Services;

public static class ClientApprovalService
{
    public static List<ClientApprovalRecord> ParseMoi(MOIForm form) =>
        JsonHelper.Deserialize<List<ClientApprovalRecord>>(form.ClientApprovalsJson);

    public static List<ClientApprovalRecord> ParseMoa(MOAForm form) =>
        JsonHelper.Deserialize<List<ClientApprovalRecord>>(form.ClientApprovalsJson);

    public static void SaveMoi(MOIForm form, List<ClientApprovalRecord> records) =>
        form.ClientApprovalsJson = JsonHelper.Serialize(records);

    public static void SaveMoa(MOAForm form, List<ClientApprovalRecord> records) =>
        form.ClientApprovalsJson = JsonHelper.Serialize(records);

    public static List<AccountHolder> GetRequiredMoiApprovalHolders(Customer customer) =>
        customer.AccountHolders
            .Where(h => h.NeedsMoiApproval && !string.IsNullOrWhiteSpace(h.Name))
            .GroupBy(h => h.AccountHolderId)
            .Select(g => g.First())
            .ToList();

    public static List<AccountHolder> GetRequiredMoaHolders(Customer customer) =>
        customer.AccountHolders
            .Where(h => h.NeedsMoa && !string.IsNullOrWhiteSpace(h.Name))
            .GroupBy(h => h.AccountHolderId)
            .Select(g => g.First())
            .ToList();

    public static List<string> GetRequiredMoiApproverNames(Customer customer) =>
        GetRequiredMoiApprovalHolders(customer)
            .Select(h => h.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static List<string> GetRequiredMoaApproverNames(Customer customer) =>
        GetRequiredMoaHolders(customer)
            .Select(h => h.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static AccountHolder? FindMoiApprovalHolderForUser(Customer customer, User user) =>
        customer.AccountHolders.FirstOrDefault(h => h.UserId == user.UserId && h.NeedsMoiApproval)
        ?? customer.AccountHolders.FirstOrDefault(h =>
            h.NeedsMoiApproval
            && !string.IsNullOrWhiteSpace(h.Email)
            && h.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
        ?? customer.AccountHolders.FirstOrDefault(h =>
            h.NeedsMoiApproval
            && h.Name.Equals(user.Name.Trim(), StringComparison.OrdinalIgnoreCase));

    public static AccountHolder? FindMoaHolderForUser(Customer customer, User user) =>
        customer.AccountHolders.FirstOrDefault(h => h.UserId == user.UserId && h.NeedsMoa)
        ?? customer.AccountHolders.FirstOrDefault(h =>
            h.NeedsMoa
            && !string.IsNullOrWhiteSpace(h.Email)
            && h.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
        ?? customer.AccountHolders.FirstOrDefault(h =>
            h.NeedsMoa
            && h.Name.Equals(user.Name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>External account holder or internal LGB signatory (template/CFO/DLCM).</summary>
    public static string? ResolveMoaSignerName(Customer customer, User user, bool allowInternalSigner)
    {
        var holder = FindMoaHolderForUser(customer, user);
        if (holder != null)
            return holder.Name.Trim();

        if (allowInternalSigner && (user.IsInternalSignatory || user.CanApproveMoa))
            return user.Name.Trim();

        return null;
    }

    /// <summary>
    /// D4: prefer UserId match scoped to this customer’s holder; name fallback only when
    /// the holder has no UserId (unprovisioned). Internal-only countersign records must
    /// not satisfy a required client holder via display-name collision.
    /// </summary>
    public static bool HasSigned(List<ClientApprovalRecord> records, AccountHolder holder)
    {
        if (holder.UserId is int uid && uid > 0)
            return records.Any(r => r.UserId == uid);

        var name = holder.Name.Trim();
        return records.Any(r =>
            r.UserId == 0
            && r.AccountHolderName.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    [Obsolete("Use HasSigned(records, AccountHolder) — name-only matching is unsafe.")]
    public static bool HasSigned(List<ClientApprovalRecord> records, string holderName) =>
        records.Any(r => r.AccountHolderName.Equals(holderName, StringComparison.OrdinalIgnoreCase));

    public static bool AllRequiredSigned(IEnumerable<AccountHolder> required, List<ClientApprovalRecord> records)
    {
        var list = required.ToList();
        return list.Count == 0 || list.All(h => HasSigned(records, h));
    }

    public static List<string> PendingApprovers(IEnumerable<AccountHolder> required, List<ClientApprovalRecord> records) =>
        required.Where(h => !HasSigned(records, h)).Select(h => h.Name.Trim()).ToList();

    // Keep name-list overloads for call sites that still pass names (delegate to holders by name on customer when possible)
    public static List<string> PendingApprovers(List<string> requiredNames, List<ClientApprovalRecord> records) =>
        requiredNames.Where(name => !records.Any(r =>
            r.AccountHolderName.Equals(name, StringComparison.OrdinalIgnoreCase))).ToList();

    /// <summary>MOI client phase complete — mode from customer; MOA always requires all signers.</summary>
    public static bool MoiClientPhaseComplete(Customer customer, List<ClientApprovalRecord> records)
    {
        var required = GetRequiredMoiApprovalHolders(customer);
        if (required.Count == 0)
            return true;

        if (string.Equals(customer.MoiApprovalMode, MoiApprovalModes.AnyOne, StringComparison.OrdinalIgnoreCase))
            return required.Any(h => HasSigned(records, h));

        return AllRequiredSigned(required, records);
    }

    /// <summary>
    /// MOA sign-off requires every listed client holder. Internal countersignatures
    /// (UserId not in required client holder set) do not count toward client completion.
    /// </summary>
    public static bool MoaClientPhaseComplete(Customer customer, List<ClientApprovalRecord> records)
    {
        var required = GetRequiredMoaHolders(customer);
        return AllRequiredSigned(required, records);
    }

    public static bool IsValidSignature(string? signatureDataUrl, string? signatureFileName)
    {
        if (string.IsNullOrWhiteSpace(signatureDataUrl))
            return false;

        var url = signatureDataUrl.Trim();
        if (!url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return false;

        // Drawn PNG/JPEG or attached image / PDF
        var comma = url.IndexOf(',');
        if (comma <= 5)
            return false;

        var meta = url[..comma].ToLowerInvariant();
        var okMime = meta.Contains("image/png")
            || meta.Contains("image/jpeg")
            || meta.Contains("image/jpg")
            || meta.Contains("image/webp")
            || meta.Contains("application/pdf");

        return okMime && url.Length > comma + 4;
    }
}
