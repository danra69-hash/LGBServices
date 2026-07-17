using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

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
        GetRequiredMoaHolders(customer, form: null);

    public static List<AccountHolder> GetRequiredMoaHolders(Customer customer, MOAForm? form)
    {
        // Per-job Admin override at Start MOA wins.
        if (form != null)
        {
            var overrideNames = JsonHelper.Deserialize<List<string>>(form.MoaApproversOverrideJson ?? "[]")
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (overrideNames.Count > 0)
            {
                return overrideNames.Select(name =>
                    customer.AccountHolders.FirstOrDefault(h =>
                        h.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    ?? new AccountHolder { Name = name, NeedsMoa = true }).ToList();
            }
        }

        // Prefer Admin-set MoaApproversJson when present.
        var fromJson = JsonHelper.Deserialize<List<string>>(customer.MoaApproversJson ?? "[]")
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (fromJson.Count > 0)
        {
            return fromJson.Select(name =>
                customer.AccountHolders.FirstOrDefault(h =>
                    h.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? new AccountHolder { Name = name, NeedsMoa = true }).ToList();
        }

        return customer.AccountHolders
            .Where(h => h.NeedsMoa && !string.IsNullOrWhiteSpace(h.Name))
            .GroupBy(h => h.AccountHolderId)
            .Select(g => g.First())
            .ToList();
    }

    public static List<string> GetRequiredMoiApproverNames(Customer customer) =>
        GetRequiredMoiApprovalHolders(customer)
            .Select(h => h.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static List<string> GetRequiredMoiApproverNames(MOIForm form, Customer customer)
    {
        if (!string.IsNullOrWhiteSpace(form.RequiredApproverName))
            return [form.RequiredApproverName.Trim()];
        return GetRequiredMoiApproverNames(customer);
    }

    public static List<string> GetRequiredMoaApproverNames(Customer customer) =>
        GetRequiredMoaHolders(customer)
            .Select(h => h.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static List<string> GetRequiredMoaApproverNames(Customer customer, MOAForm? form) =>
        GetRequiredMoaHolders(customer, form)
            .Select(h => h.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Bind CubeV 1:1 matrix approver onto the MOI at submit time.</summary>
    public static async Task<bool> TryBindMatrixApproverAsync(
        AppDbContext context,
        MOIForm form,
        User? submitter,
        string? requestedByName)
    {
        MoiApprovalMatrixEntry? entry = null;
        if (submitter != null && !string.IsNullOrWhiteSpace(submitter.Email))
        {
            entry = await context.MoiApprovalMatrixEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.RequesterEmail == submitter.Email.Trim().ToLowerInvariant());
        }

        if (entry == null && !string.IsNullOrWhiteSpace(requestedByName))
        {
            var name = requestedByName.Trim();
            entry = await context.MoiApprovalMatrixEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.RequesterName == name);
        }

        // Fall back: parse requestedBy from form JSON
        if (entry == null)
        {
            var fromForm = ReadFormString(form.FormDataJson, "requestedBy")
                ?? ReadFormString(form.FormDataJson, "projectInitiator");
            if (!string.IsNullOrWhiteSpace(fromForm))
            {
                entry = await context.MoiApprovalMatrixEntries
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.RequesterName == fromForm.Trim());
            }
        }

        if (entry == null)
            return false;

        form.RequiredApproverName = entry.ApproverName;
        form.RequiredApproverEmail = entry.ApproverEmail.Trim().ToLowerInvariant();
        return true;
    }

    public static AccountHolder? FindMoiApprovalHolderForUser(Customer customer, User user, MOIForm? form = null)
    {
        if (form != null && !string.IsNullOrWhiteSpace(form.RequiredApproverEmail))
        {
            if (user.Email.Equals(form.RequiredApproverEmail, StringComparison.OrdinalIgnoreCase))
            {
                return customer.AccountHolders.FirstOrDefault(h =>
                           !string.IsNullOrWhiteSpace(h.Email)
                           && h.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
                       ?? new AccountHolder
                       {
                           Name = string.IsNullOrWhiteSpace(form.RequiredApproverName)
                               ? user.Name
                               : form.RequiredApproverName,
                           Email = form.RequiredApproverEmail,
                           UserId = user.UserId,
                           NeedsMoiApproval = true,
                       };
            }

            // Not the matrix approver
            return null;
        }

        return customer.AccountHolders.FirstOrDefault(h => h.UserId == user.UserId && h.NeedsMoiApproval)
            ?? customer.AccountHolders.FirstOrDefault(h =>
                h.NeedsMoiApproval
                && !string.IsNullOrWhiteSpace(h.Email)
                && h.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
            ?? customer.AccountHolders.FirstOrDefault(h =>
                h.NeedsMoiApproval
                && h.Name.Equals(user.Name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static AccountHolder? FindMoaHolderForUser(Customer customer, User user) =>
        customer.AccountHolders.FirstOrDefault(h => h.UserId == user.UserId && h.NeedsMoa)
        ?? customer.AccountHolders.FirstOrDefault(h =>
            h.NeedsMoa
            && !string.IsNullOrWhiteSpace(h.Email)
            && h.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
        ?? customer.AccountHolders.FirstOrDefault(h =>
            h.NeedsMoa
            && h.Name.Equals(user.Name.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? (JsonHelper.Deserialize<List<string>>(customer.MoaApproversJson ?? "[]")
                .Any(n => n.Trim().Equals(user.Name.Trim(), StringComparison.OrdinalIgnoreCase))
            ? new AccountHolder { Name = user.Name.Trim(), Email = user.Email, UserId = user.UserId, NeedsMoa = true }
            : null);

    public static string? ResolveMoaSignerName(Customer customer, User user, bool allowInternalSigner)
    {
        var holder = FindMoaHolderForUser(customer, user);
        if (holder != null)
            return holder.Name.Trim();

        if (allowInternalSigner && (user.IsInternalSignatory || user.CanApproveMoa))
            return user.Name.Trim();

        return null;
    }

    public static bool HasSigned(List<ClientApprovalRecord> records, AccountHolder holder)
    {
        if (holder.UserId is int uid && uid > 0)
            return records.Any(r => r.UserId == uid);

        var name = holder.Name.Trim();
        return records.Any(r =>
            r.UserId == 0
            && r.AccountHolderName.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Matrix 1:1 — complete when the required approver has signed (by email or name).</summary>
    public static bool HasMatrixApproverSigned(MOIForm form, List<ClientApprovalRecord> records)
    {
        if (string.IsNullOrWhiteSpace(form.RequiredApproverName)
            && string.IsNullOrWhiteSpace(form.RequiredApproverEmail))
            return false;

        return records.Any(r =>
            (!string.IsNullOrWhiteSpace(form.RequiredApproverName)
             && r.AccountHolderName.Equals(form.RequiredApproverName.Trim(), StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(form.RequiredApproverEmail)
                && !string.IsNullOrWhiteSpace(r.AccountHolderName)
                && r.AccountHolderName.Contains('@')
                && r.AccountHolderName.Equals(form.RequiredApproverEmail, StringComparison.OrdinalIgnoreCase)));
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

    public static List<string> PendingApprovers(List<string> requiredNames, List<ClientApprovalRecord> records) =>
        requiredNames.Where(name => !records.Any(r =>
            r.AccountHolderName.Equals(name, StringComparison.OrdinalIgnoreCase))).ToList();

    /// <summary>MOI client phase — CubeV 1:1 matrix when bound; else legacy holder list.</summary>
    public static bool MoiClientPhaseComplete(Customer customer, List<ClientApprovalRecord> records) =>
        MoiClientPhaseComplete(customer, form: null, records);

    public static bool MoiClientPhaseComplete(Customer customer, MOIForm? form, List<ClientApprovalRecord> records)
    {
        if (form != null && !string.IsNullOrWhiteSpace(form.RequiredApproverName))
        {
            return records.Any(r =>
                r.AccountHolderName.Equals(form.RequiredApproverName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var required = GetRequiredMoiApprovalHolders(customer);
        if (required.Count == 0)
            return true;

        if (string.Equals(customer.MoiApprovalMode, MoiApprovalModes.AnyOne, StringComparison.OrdinalIgnoreCase))
            return required.Any(h => HasSigned(records, h));

        return AllRequiredSigned(required, records);
    }

    public static bool MoaClientPhaseComplete(Customer customer, List<ClientApprovalRecord> records) =>
        MoaClientPhaseComplete(customer, form: null, records);

    public static bool MoaClientPhaseComplete(Customer customer, MOAForm? form, List<ClientApprovalRecord> records)
    {
        var required = GetRequiredMoaHolders(customer, form);
        return AllRequiredSigned(required, records);
    }

    public static bool IsValidSignature(string? signatureDataUrl, string? signatureFileName)
    {
        if (string.IsNullOrWhiteSpace(signatureDataUrl))
            return false;

        var url = signatureDataUrl.Trim();
        if (!url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return false;

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

    public static string? ReadRequestedByName(MOIForm form) =>
        ReadFormString(form.FormDataJson, "requestedBy")
        ?? ReadFormString(form.FormDataJson, "projectInitiator");

    private static string? ReadFormString(string? json, string key)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var map = JsonHelper.Deserialize<Dictionary<string, object?>>(json);
            if (map != null && map.TryGetValue(key, out var v) && v != null)
            {
                var s = v.ToString();
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }
        }
        catch { /* ignore */ }
        return null;
    }
}
