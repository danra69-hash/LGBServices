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

    public static List<string> GetRequiredMoiApproverNames(Customer customer) =>
        customer.AccountHolders
            .Where(h => h.NeedsMoiApproval && !string.IsNullOrWhiteSpace(h.Name))
            .Select(h => h.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static List<string> GetRequiredMoaApproverNames(Customer customer) =>
        customer.AccountHolders
            .Where(h => h.NeedsMoa && !string.IsNullOrWhiteSpace(h.Name))
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

    public static bool HasSigned(List<ClientApprovalRecord> records, string holderName) =>
        records.Any(r => r.AccountHolderName.Equals(holderName, StringComparison.OrdinalIgnoreCase));

    public static bool AllRequiredSigned(List<string> required, List<ClientApprovalRecord> records) =>
        required.Count == 0
        || required.All(name => HasSigned(records, name));

    public static List<string> PendingApprovers(List<string> required, List<ClientApprovalRecord> records) =>
        required.Where(name => !HasSigned(records, name)).ToList();

    /// <summary>MOI client phase complete — mode from customer; MOA always requires all signers.</summary>
    public static bool MoiClientPhaseComplete(Customer customer, List<ClientApprovalRecord> records)
    {
        var required = GetRequiredMoiApproverNames(customer);
        if (required.Count == 0)
            return true;

        if (string.Equals(customer.MoiApprovalMode, MoiApprovalModes.AnyOne, StringComparison.OrdinalIgnoreCase))
        {
            return records.Any(r => required.Any(n =>
                n.Equals(r.AccountHolderName, StringComparison.OrdinalIgnoreCase)));
        }

        return AllRequiredSigned(required, records);
    }

    /// <summary>MOA sign-off always requires every listed approver.</summary>
    public static bool MoaClientPhaseComplete(Customer customer, List<ClientApprovalRecord> records)
    {
        var required = GetRequiredMoaApproverNames(customer);
        return AllRequiredSigned(required, records);
    }
}
