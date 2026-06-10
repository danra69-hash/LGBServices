using System.Text.RegularExpressions;
using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;

namespace LGBApp.Backend.Services;

public static class CustomerMapper
{
    public static CustomerResponse ToResponse(Customer customer)
    {
        var asOf = DateTime.UtcNow;
        var packages = customer.Packages
            .OrderByDescending(p => p.PurchasedDate)
            .Select(p => ToPackageDto(p, asOf))
            .ToList();

        var activePackages = customer.Packages.Where(p => PackageProration.IsBillableActive(p, asOf)).ToList();
        var primaryDto = packages.FirstOrDefault(p => p.Status == "Active" && p.ActiveValue > 0)
            ?? packages.FirstOrDefault(p => p.Status == "Active")
            ?? packages.FirstOrDefault();

        var totalValue = activePackages.Sum(p => PackageProration.GetContractValue(p));

        return new CustomerResponse
        {
            Id = customer.CustomerId,
            Name = customer.Name,
            Email = customer.Email,
            Phone = customer.Phone,
            Company = customer.Company,
            Status = customer.Status,
            Value = totalValue,
            LastContact = customer.LastContact.ToString("yyyy-MM-dd"),
            InvoiceBy = customer.InvoiceBy,
            ChargeTo = customer.ChargeTo,
            Package = primaryDto?.PackageName ?? customer.Package,
            PackageValue = primaryDto?.PackageValue ?? customer.PackageValue,
            Cosec = customer.Cosec,
            DivisionGroupCode = customer.DivisionGroupCode,
            HasLoa = customer.HasLoa,
            LoaHolders = JsonHelper.Deserialize<List<string>>(customer.LoaHoldersJson),
            MoiFormTemplateCode = customer.MoiFormTemplateCode,
            MoaFormTemplateCode = customer.MoaFormTemplateCode,
            Moi = JsonHelper.Deserialize<List<string>>(customer.MoiJson),
            MoiApproval = JsonHelper.Deserialize<List<string>>(customer.MoiApprovalJson),
            Moa = JsonHelper.Deserialize<List<string>>(customer.MoaJson),
            PurchasedDate = primaryDto?.PurchasedDate ?? customer.PurchasedDate.ToString("yyyy-MM-dd"),
            ExpiryDate = primaryDto?.ExpiryDate ?? customer.ExpiryDate.ToString("yyyy-MM-dd"),
            Packages = packages,
            AccountHolders = customer.AccountHolders
                .OrderBy(h => h.AccountHolderId)
                .Select(h => new AccountHolderDto
                {
                    Id = h.AccountHolderId,
                    Name = h.Name,
                    Email = h.Email,
                    Phone = h.Phone
                })
                .ToList()
        };
    }

    public static CustomerPackageDto ToPackageDto(CustomerPackage package, DateTime? asOfUtc = null) => new()
    {
        Id = package.CustomerPackageId,
        PackageName = package.PackageName,
        PackageValue = package.PackageValue,
        PackageDetail = package.PackageDetail,
        PurchasedDate = package.PurchasedDate.ToString("yyyy-MM-dd"),
        ExpiryDate = package.ExpiryDate.ToString("yyyy-MM-dd"),
        Validity = string.IsNullOrWhiteSpace(package.Validity) ? "1 Year" : package.Validity,
        Pricing = DeserializePricing(package.PricingJson),
        Status = PackageProration.GetEffectiveStatus(package, asOfUtc),
        ActiveValue = PackageProration.GetContractValue(package)
    };

    public static Customer FromCreateRequest(CreateCustomerRequest request)
    {
        var holders = request.AccountHolders ?? new List<AccountHolderInput>();
        var moi = holders.Where(h => h.Moi).Select(h => h.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        var moiApproval = holders.Where(h => h.MoiApproval).Select(h => h.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        var moa = holders.Where(h => h.Moa).Select(h => h.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        var purchased = DateTime.TryParse(request.DateCreated, out var pd) ? pd : DateTime.UtcNow;

        var packageInputs = ResolvePackageInputs(request);
        var packages = packageInputs.Select(input => ToPackageEntity(input, purchased)).ToList();

        var primary = packages
            .Where(p => PackageProration.IsBillableActive(p))
            .OrderByDescending(p => p.PurchasedDate)
            .FirstOrDefault()
            ?? packages.OrderByDescending(p => p.PurchasedDate).FirstOrDefault();

        var totalValue = packages
            .Where(p => PackageProration.IsBillableActive(p))
            .Sum(p => PackageProration.GetActiveValue(p));

        return new Customer
        {
            Name = request.ContactName ?? string.Empty,
            Email = request.Email ?? string.Empty,
            Phone = request.Mobile ?? string.Empty,
            Company = request.CompanyName ?? string.Empty,
            Status = "Active",
            Value = totalValue,
            LastContact = purchased,
            InvoiceBy = request.InvoiceBy ?? request.CompanyName ?? string.Empty,
            ChargeTo = request.ChargeTo ?? request.CompanyName ?? string.Empty,
            Package = primary?.PackageName ?? string.Empty,
            PackageValue = primary?.PackageValue ?? 0,
            Cosec = request.Cosec,
            DivisionGroupCode = request.DivisionGroupCode ?? string.Empty,
            HasLoa = request.HasLoa,
            LoaHoldersJson = JsonHelper.Serialize(request.LoaHolders ?? new List<string>()),
            MoiFormTemplateCode = request.MoiFormTemplateCode,
            MoaFormTemplateCode = request.MoaFormTemplateCode,
            MoiJson = JsonHelper.Serialize(moi),
            MoiApprovalJson = JsonHelper.Serialize(moiApproval),
            MoaJson = JsonHelper.Serialize(moa),
            PurchasedDate = primary?.PurchasedDate ?? purchased,
            ExpiryDate = primary?.ExpiryDate ?? ComputeExpiryDate(purchased, request.Validity),
            Packages = packages,
            AccountHolders = holders.Select(h => new AccountHolder
            {
                Name = h.Name,
                Email = h.Email,
                Phone = h.Phone
            }).ToList()
        };
    }

    public static void ApplyUpdate(Customer customer, CustomerResponse request)
    {
        customer.Name = request.Name;
        customer.Email = request.Email;
        customer.Phone = request.Phone;
        customer.Company = request.Company;
        customer.Status = request.Status;
        customer.InvoiceBy = request.InvoiceBy;
        customer.ChargeTo = request.ChargeTo;
        customer.Cosec = request.Cosec;
        customer.DivisionGroupCode = request.DivisionGroupCode ?? string.Empty;
        customer.HasLoa = request.HasLoa;
        customer.LoaHoldersJson = JsonHelper.Serialize(request.LoaHolders);
        customer.MoiFormTemplateCode = request.MoiFormTemplateCode;
        customer.MoaFormTemplateCode = request.MoaFormTemplateCode;
        customer.MoiJson = JsonHelper.Serialize(request.Moi);
        customer.MoiApprovalJson = JsonHelper.Serialize(request.MoiApproval);
        customer.MoaJson = JsonHelper.Serialize(request.Moa);
        customer.LastContact = DateTime.TryParse(request.LastContact, out var lc) ? lc : customer.LastContact;
        customer.PurchasedDate = DateTime.TryParse(request.PurchasedDate, out var pd) ? pd : customer.PurchasedDate;
        customer.ExpiryDate = DateTime.TryParse(request.ExpiryDate, out var ed) ? ed : customer.ExpiryDate;

        if (request.Packages.Count > 0)
        {
            customer.Packages.Clear();
            foreach (var pkg in request.Packages)
            {
                var purchased = DateTime.TryParse(pkg.PurchasedDate, out var pDate) ? pDate : customer.PurchasedDate;
                var validity = string.IsNullOrWhiteSpace(pkg.Validity) ? "1 Year" : pkg.Validity;
                var expiry = DateTime.TryParse(pkg.ExpiryDate, out var eDate)
                    ? eDate
                    : ComputeExpiryDate(purchased, validity);

                customer.Packages.Add(new CustomerPackage
                {
                    PackageName = pkg.PackageName,
                    PackageValue = pkg.PackageValue,
                    PackageDetail = pkg.PackageDetail,
                    Validity = validity,
                    PricingJson = SerializePricing(pkg.Pricing),
                    PurchasedDate = purchased,
                    ExpiryDate = expiry,
                    Status = string.IsNullOrWhiteSpace(pkg.Status) ? "Active" : pkg.Status
                });
            }

            SyncPrimaryPackageFields(customer);
        }
        else
        {
            customer.Package = request.Package;
            customer.PackageValue = request.PackageValue;
            customer.Value = request.Value;
        }
    }

    public static void SyncPrimaryPackageFields(Customer customer)
    {
        foreach (var package in customer.Packages)
            PackageProration.NormalizePackageStatus(package);

        var asOf = DateTime.UtcNow;
        var active = customer.Packages
            .Where(p => PackageProration.IsBillableActive(p, asOf))
            .OrderByDescending(p => p.PurchasedDate)
            .ToList();

        var primary = active.FirstOrDefault()
            ?? customer.Packages.OrderByDescending(p => p.PurchasedDate).FirstOrDefault();

        if (primary == null)
        {
            customer.Package = string.Empty;
            customer.PackageValue = 0;
            customer.Value = 0;
            return;
        }

        customer.Package = primary.PackageName;
        customer.PackageValue = primary.PackageValue;
        customer.PurchasedDate = primary.PurchasedDate;
        customer.ExpiryDate = primary.ExpiryDate;
        customer.Value = active.Sum(p => PackageProration.GetContractValue(p));
    }

    private static List<CustomerPackageInput> ResolvePackageInputs(CreateCustomerRequest request)
    {
        if (request.Packages is { Count: > 0 })
            return request.Packages;

        if (string.IsNullOrWhiteSpace(request.PackageName))
            return new List<CustomerPackageInput>();

        return
        [
            new CustomerPackageInput
            {
                PackageName = request.PackageName,
                PackageValue = request.PackageValue,
                PackageDetail = null,
                Validity = request.Validity,
                PurchasedDate = request.DateCreated,
                Status = "Active"
            }
        ];
    }

    private static CustomerPackage ToPackageEntity(CustomerPackageInput input, DateTime defaultPurchased)
    {
        var purchased = DateTime.TryParse(input.PurchasedDate, out var pd) ? pd : defaultPurchased;
        decimal.TryParse(input.PackageValue, out var packageValue);

        var validity = string.IsNullOrWhiteSpace(input.Validity) ? "1 Year" : input.Validity;

        return new CustomerPackage
        {
            PackageName = input.PackageName ?? string.Empty,
            PackageValue = packageValue,
            PackageDetail = input.PackageDetail,
            Validity = validity,
            PricingJson = !string.IsNullOrWhiteSpace(input.PricingJson) ? input.PricingJson : "{}",
            PurchasedDate = purchased,
            ExpiryDate = ComputeExpiryDate(purchased, validity),
            Status = string.IsNullOrWhiteSpace(input.Status) ? "Active" : input.Status
        };
    }

    private static PackagePricingDto DeserializePricing(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return new PackagePricingDto();

        var pricing = JsonHelper.Deserialize<PackagePricingDto>(json);
        pricing.AddOnLines = NormalizeAddOnLines(pricing.AddOnLines);
        return pricing;
    }

    private static string SerializePricing(PackagePricingDto? pricing)
    {
        if (pricing == null)
            return "{}";

        pricing.AddOnLines = NormalizeAddOnLines(pricing.AddOnLines);
        return JsonHelper.Serialize(pricing);
    }

    private static List<AddOnLineDto> NormalizeAddOnLines(List<AddOnLineDto>? lines)
    {
        if (lines == null || lines.Count == 0)
            return [];

        return lines
            .Select(l => new AddOnLineDto
            {
                Name = l.Name,
                Qty = l.Qty,
                UnitPrice = l.Qty > 0
                    ? FigmaProductCatalog.CustomerOptionalAddOnUnitPrice
                    : 0,
            })
            .ToList();
    }

    private static DateTime ComputeExpiryDate(DateTime purchased, string? validity)
    {
        if (string.IsNullOrWhiteSpace(validity))
            return purchased.AddYears(1);

        var match = Regex.Match(validity.Trim(), @"(\d+)");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var amount) || amount <= 0)
            return purchased.AddYears(1);

        if (amount > 50)
            return purchased.AddYears(1);

        var lower = validity.ToLowerInvariant();
        if (lower.Contains("month"))
        {
            amount = Math.Min(amount, 120);
            return purchased.AddMonths(amount);
        }

        amount = Math.Min(amount, 10);
        return purchased.AddYears(amount);
    }
}
