using System.Text.Json;
using LGBApp.Backend.Models;
using LGBApp.Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Data;

/// <summary>
/// Seeds customers / billing / division recommenders from CubeV SOURCE rows 2–167
/// (first 166 real companies in cubev-init.json). Skips addon-menu placeholders.
/// Idempotent upsert by company name.
/// </summary>
public static class CubeVCustomerSeeder
{
    /// <summary>Excel SOURCE rows 2–167 → JSON companies[0..165].</summary>
    public const int SourceCompanyCount = 166;

    private static readonly HashSet<string> AddonMenuPlaceholders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Newly Incorp. Company (please specify)",
        "Other Ad-hoc & Complex Works (please specify)",
        "SSM lodgement fee for others (please specify)",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static void SeedIfNeeded(AppDbContext context) => UpsertSourceCompanies(context);

    /// <summary>Upsert SOURCE companies (rows 2–167). Safe to re-run.</summary>
    public static void UpsertSourceCompanies(AppDbContext context)
    {
        var data = LoadSeedData();
        if (data?.Companies == null || data.Companies.Count == 0)
        {
            Console.WriteLine("[CubeV seed] No seed data found — skipping.");
            return;
        }

        var sourceRows = data.Companies
            .Take(SourceCompanyCount)
            .Where(r => !string.IsNullOrWhiteSpace(r.Company)
                        && !AddonMenuPlaceholders.Contains(r.Company.Trim()))
            .ToList();

        Console.WriteLine($"[CubeV seed] Upserting {sourceRows.Count} SOURCE companies (rows 2–167)…");

        ApplyDivisionRecommenders(context, data);
        var partyIds = EnsureBillingParties(context, data);
        var purchased = DateTime.UtcNow.Date.AddMonths(-1);
        var expiry = purchased.AddYears(1);
        var created = 0;
        var updated = 0;

        var existingByCompany = context.Customers
            .Include(c => c.Packages)
            .Include(c => c.AccountHolders)
            .ToList()
            .GroupBy(c => c.Company.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in sourceRows)
        {
            var companyName = row.Company.Trim();
            var holders = BuildHoldersForCompany(row, data);
            var primary = holders.FirstOrDefault()
                ?? new AccountHolderInput { Name = "Company Contact", Email = "", Moi = true };

            var invoiceName = CleanParty(row.BillTo) ?? CleanParty(row.InvoiceBy) ?? companyName;
            var chargeName = CleanParty(row.ChargeTo) ?? companyName;
            var invoiceId = ResolvePartyId(partyIds, invoiceName);
            var chargeId = ResolvePartyId(partyIds, chargeName);
            var pricing = BuildPricingJson(row);

            if (existingByCompany.TryGetValue(companyName, out var customer))
            {
                customer.Name = primary.Name;
                customer.Email = primary.Email;
                customer.Value = row.PackageValue;
                customer.InvoiceBy = invoiceName;
                customer.ChargeTo = chargeName;
                customer.InvoiceByPartyIdsJson = JsonHelper.Serialize(invoiceId > 0 ? new List<int> { invoiceId } : new List<int>());
                customer.ChargeToPartyIdsJson = JsonHelper.Serialize(chargeId > 0 ? new List<int> { chargeId } : new List<int>());
                customer.Package = row.PackageName;
                customer.PackageValue = row.PackageValue;
                customer.Cosec = row.Cosec;
                customer.DivisionGroupCode = row.DivisionCode ?? "";
                customer.HasLoa = row.HasLoa;
                customer.LoaHoldersJson = JsonHelper.Serialize(
                    row.HasLoa
                        ? holders.Where(h => h.Moa).Select(h => h.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList()
                        : new List<string>());
                customer.MoaWorkflowTemplateCode = string.IsNullOrWhiteSpace(row.MoaWorkflowTemplateCode)
                    ? customer.MoaWorkflowTemplateCode
                    : row.MoaWorkflowTemplateCode;
                customer.MoiJson = JsonHelper.Serialize(holders.Where(h => h.Moi).Select(h => h.Name).ToList());
                customer.MoiApprovalJson = JsonHelper.Serialize(holders.Where(h => h.MoiApproval).Select(h => h.Name).ToList());
                customer.MoaJson = JsonHelper.Serialize(holders.Where(h => h.Moa).Select(h => h.Name).ToList());
                customer.Status = "Active";

                var pkg = customer.Packages.OrderByDescending(p => p.PurchasedDate).FirstOrDefault();
                if (pkg == null)
                {
                    customer.Packages.Add(new CustomerPackage
                    {
                        PackageName = row.PackageName,
                        PackageValue = row.PackageValue,
                        Validity = "1 Year",
                        PurchasedDate = purchased,
                        ExpiryDate = expiry,
                        Status = "Active",
                        PricingJson = pricing,
                    });
                }
                else
                {
                    pkg.PackageName = row.PackageName;
                    pkg.PackageValue = row.PackageValue;
                    pkg.PricingJson = pricing;
                    pkg.Status = "Active";
                }

                updated++;
                continue;
            }

            customer = new Customer
            {
                Name = primary.Name,
                Email = primary.Email,
                Phone = "",
                Company = companyName,
                Status = "Active",
                Value = row.PackageValue,
                LastContact = DateTime.UtcNow,
                InvoiceBy = invoiceName,
                ChargeTo = chargeName,
                InvoiceByPartyIdsJson = JsonHelper.Serialize(invoiceId > 0 ? new List<int> { invoiceId } : new List<int>()),
                ChargeToPartyIdsJson = JsonHelper.Serialize(chargeId > 0 ? new List<int> { chargeId } : new List<int>()),
                Package = row.PackageName,
                PackageValue = row.PackageValue,
                Cosec = row.Cosec,
                DivisionGroupCode = row.DivisionCode ?? "",
                HasLoa = row.HasLoa,
                LoaHoldersJson = JsonHelper.Serialize(
                    row.HasLoa
                        ? holders.Where(h => h.Moa).Select(h => h.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList()
                        : new List<string>()),
                MoaWorkflowTemplateCode = string.IsNullOrWhiteSpace(row.MoaWorkflowTemplateCode)
                    ? null
                    : row.MoaWorkflowTemplateCode,
                MoiJson = JsonHelper.Serialize(holders.Where(h => h.Moi).Select(h => h.Name).ToList()),
                MoiApprovalJson = JsonHelper.Serialize(holders.Where(h => h.MoiApproval).Select(h => h.Name).ToList()),
                MoaJson = JsonHelper.Serialize(holders.Where(h => h.Moa).Select(h => h.Name).ToList()),
                PurchasedDate = purchased,
                ExpiryDate = expiry,
                AccountHolders = holders.Select(h => new AccountHolder
                {
                    Name = h.Name,
                    Email = h.Email,
                    Phone = h.Phone,
                    NeedsMoi = h.Moi,
                    NeedsMoiApproval = h.MoiApproval,
                    NeedsMoa = h.Moa,
                }).ToList(),
                Packages =
                [
                    new CustomerPackage
                    {
                        PackageName = row.PackageName,
                        PackageValue = row.PackageValue,
                        Validity = "1 Year",
                        PurchasedDate = purchased,
                        ExpiryDate = expiry,
                        Status = "Active",
                        PricingJson = pricing,
                    },
                ],
            };

            context.Customers.Add(customer);
            existingByCompany[companyName] = customer;
            created++;
        }

        context.SaveChanges();
        Console.WriteLine($"[CubeV seed] SOURCE upsert done — {created} created, {updated} updated.");
    }

    private static CubeVSeedFile? LoadSeedData()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Data", "Seed", "cubev-init.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Data", "Seed", "cubev-init.json"),
            Path.Combine(AppContext.BaseDirectory, "cubev-init.json"),
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path))
                continue;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<CubeVSeedFile>(json, JsonOptions);
        }

        Console.WriteLine("[CubeV seed] cubev-init.json not found next to the API binary.");
        return null;
    }

    private static void ApplyDivisionRecommenders(AppDbContext context, CubeVSeedFile data)
    {
        if (data.Groups == null)
            return;

        foreach (var (code, group) in data.Groups)
        {
            var division = context.DivisionGroups
                .Include(g => g.Recommenders)
                .FirstOrDefault(g => g.Code == code);
            if (division == null || group.Recommenders == null || group.Recommenders.Count == 0)
                continue;

            context.DivisionGroupRecommenders.RemoveRange(division.Recommenders);
            division.Recommenders = group.Recommenders
                .Take(8)
                .Select(r => new DivisionGroupRecommender
                {
                    DisplayName = r.Name,
                    UserId = null,
                })
                .ToList();

            if (code == "SWM")
                division.MoaWorkflowTemplateCode = "MOA_SWM";
        }

        context.SaveChanges();
    }

    private static Dictionary<string, int> EnsureBillingParties(AppDbContext context, CubeVSeedFile data)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in data.Companies)
        {
            foreach (var raw in new[] { c.InvoiceBy, c.ChargeTo, c.BillTo, c.Company })
            {
                var cleaned = CleanParty(raw);
                if (cleaned != null)
                    names.Add(cleaned);
            }
        }

        var existing = context.BillingParties.ToList();
        var byName = existing.ToDictionary(p => p.Name, p => p.BillingPartyId, StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            if (byName.ContainsKey(name))
                continue;
            var party = new BillingParty
            {
                Name = name,
                Category = "Both",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            context.BillingParties.Add(party);
            context.SaveChanges();
            byName[name] = party.BillingPartyId;
        }

        return byName;
    }

    private static int ResolvePartyId(Dictionary<string, int> byName, string name)
    {
        return byName.TryGetValue(name, out var id) ? id : 0;
    }

    private static string? CleanParty(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var s = raw.Trim();
        if (s is "No" or "No bill" or "None" or "#N/A" or "0")
            return null;
        return s;
    }

    private static List<AccountHolderInput> BuildHoldersForCompany(CubeVCompanyRow row, CubeVSeedFile data)
    {
        var merged = new Dictionary<string, AccountHolderInput>(StringComparer.OrdinalIgnoreCase);
        void Upsert(CubeVPerson? person, bool moi, bool moiApproval, bool moa)
        {
            if (person == null || string.IsNullOrWhiteSpace(person.Email))
                return;
            var key = person.Email.Trim().ToLowerInvariant();
            if (!merged.TryGetValue(key, out var holder))
            {
                holder = new AccountHolderInput
                {
                    Name = person.Name?.Trim() ?? key,
                    Email = key,
                };
                merged[key] = holder;
            }
            holder.Moi |= moi;
            holder.MoiApproval |= moiApproval;
            holder.Moa |= moa;
        }

        if (!string.IsNullOrWhiteSpace(row.DivisionCode)
            && data.Groups != null
            && data.Groups.TryGetValue(row.DivisionCode, out var group))
        {
            foreach (var p in group.MoiIssuers ?? [])
                Upsert(p, moi: true, moiApproval: false, moa: false);
            foreach (var p in group.MoiApprovers ?? [])
                Upsert(p, moi: false, moiApproval: true, moa: false);
            foreach (var p in group.MoaApprovers ?? [])
                Upsert(p, moi: false, moiApproval: false, moa: true);
        }

        // Cap holders per company so signatory provisioning stays manageable.
        var list = merged.Values
            .OrderByDescending(h => (h.Moi ? 4 : 0) + (h.MoiApproval ? 2 : 0) + (h.Moa ? 1 : 0))
            .Take(12)
            .ToList();

        if (list.Count == 0)
        {
            var slug = Slugify(row.Company);
            list.Add(new AccountHolderInput
            {
                Name = $"{row.Company} Contact",
                Email = $"contact+{slug}@lgb.test",
                Moi = true,
                Moa = true,
            });
        }

        return list;
    }

    private static string BuildPricingJson(CubeVCompanyRow row)
    {
        var addOns = (row.AddOns ?? [])
            .Where(a => !string.IsNullOrWhiteSpace(a.Name) && a.Qty > 0)
            .Select(a => new Dictionary<string, object?>
            {
                ["name"] = a.Name,
                ["qty"] = a.Qty,
                ["unitPrice"] = 120,
            })
            .ToList();

        return JsonHelper.Serialize(new Dictionary<string, object?>
        {
            ["validity"] = "1 Year",
            ["basePackagePrice"] = row.PackageValue,
            ["addOnLines"] = addOns,
        });
    }

    private static string Slugify(string value)
    {
        var chars = value.ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .Take(20)
            .ToArray();
        return chars.Length == 0 ? "company" : new string(chars);
    }

    private sealed class CubeVSeedFile
    {
        public List<CubeVCompanyRow> Companies { get; set; } = [];
        public Dictionary<string, CubeVGroup>? Groups { get; set; }
        public List<string>? LoaCompanies { get; set; }
        public List<CubeVPerson>? CosecStaff { get; set; }
    }

    private sealed class CubeVCompanyRow
    {
        public string Company { get; set; } = "";
        public string? DivisionCode { get; set; }
        public bool Cosec { get; set; }
        public string PackageName { get; set; } = "Basic Package";
        public decimal PackageValue { get; set; }
        public int ResoQty { get; set; }
        public string? InvoiceBy { get; set; }
        public string? ChargeTo { get; set; }
        public string? BillTo { get; set; }
        public bool HasLoa { get; set; }
        public string? MoaWorkflowTemplateCode { get; set; }
        public List<CubeVAddOn>? AddOns { get; set; }
    }

    private sealed class CubeVGroup
    {
        public List<CubeVPerson>? MoiIssuers { get; set; }
        public List<CubeVPerson>? MoiApprovers { get; set; }
        public List<CubeVPerson>? MoaApprovers { get; set; }
        public List<CubeVPerson>? Recommenders { get; set; }
    }

    private sealed class CubeVPerson
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
    }

    private sealed class CubeVAddOn
    {
        public string Name { get; set; } = "";
        public int Qty { get; set; }
    }

    private sealed class AccountHolderInput
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public bool Moi { get; set; }
        public bool MoiApproval { get; set; }
        public bool Moa { get; set; }
    }
}
