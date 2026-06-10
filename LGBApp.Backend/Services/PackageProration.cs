using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;
using System.Text.RegularExpressions;

namespace LGBApp.Backend.Services;

public static class PackageProration
{
    public static string GetEffectiveStatus(CustomerPackage package, DateTime? asOfUtc = null)
    {
        if (string.Equals(package.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            return "Cancelled";

        var today = (asOfUtc ?? DateTime.UtcNow).Date;
        if (string.Equals(package.Status, "Expired", StringComparison.OrdinalIgnoreCase)
            || today >= package.ExpiryDate.Date)
        {
            return "Expired";
        }

        return "Active";
    }

    public static void NormalizePackageStatus(CustomerPackage package, DateTime? asOfUtc = null)
    {
        package.Status = GetEffectiveStatus(package, asOfUtc);
    }

    public static bool IsBillableActive(CustomerPackage package, DateTime? asOfUtc = null)
        => GetEffectiveStatus(package, asOfUtc) == "Active";

    /// <summary>
    /// Full contract value: catalog base scaled by validity + flat optional customer add-ons.
    /// </summary>
    public static decimal GetContractValue(CustomerPackage package)
    {
        var parsed = DeserializePricing(package.PricingJson);
        if (parsed.BasePackagePrice > 0)
        {
            var validity = string.IsNullOrWhiteSpace(parsed.Validity)
                ? package.Validity
                : parsed.Validity;
            var scaledBase = parsed.BasePackagePrice * ValidityFactor(validity);
            var addOnTotal = (parsed.AddOnLines ?? [])
                .Where(l => l.Qty > 0)
                .Sum(l => l.Qty * NormalizeAddOnUnitPrice(l.UnitPrice));
            return Math.Round(scaledBase + addOnTotal, 2, MidpointRounding.AwayFromZero);
        }

        return package.PackageValue;
    }

    /// <summary>
    /// Remaining contract value prorated by days left in the term.
    /// </summary>
    public static decimal GetActiveValue(CustomerPackage package, DateTime? asOfUtc = null)
    {
        if (!IsBillableActive(package, asOfUtc))
            return 0;

        var contractValue = GetContractValue(package);
        if (contractValue <= 0)
            return 0;

        var purchased = package.PurchasedDate.Date;
        var expiry = package.ExpiryDate.Date;
        var today = (asOfUtc ?? DateTime.UtcNow).Date;

        var totalDays = (expiry - purchased).TotalDays;
        if (totalDays <= 0)
            return contractValue;

        var remainingDays = Math.Max(0, (expiry - today).TotalDays);
        var fraction = (decimal)(remainingDays / totalDays);
        return Math.Round(contractValue * fraction, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal ValidityFactor(string? validity)
    {
        if (string.IsNullOrWhiteSpace(validity))
            return 1m;

        var match = Regex.Match(validity.Trim(), @"(\d+)");
        var amount = match.Success && int.TryParse(match.Groups[1].Value, out var n) ? n : 1;
        if (amount <= 0)
            amount = 1;

        if (validity.Contains("month", StringComparison.OrdinalIgnoreCase))
            return amount / 12m;

        return amount;
    }

    private static decimal NormalizeAddOnUnitPrice(decimal unitPrice)
        => unitPrice > 0 ? unitPrice : FigmaProductCatalog.CustomerOptionalAddOnUnitPrice;

    private static PackagePricingDto DeserializePricing(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return new PackagePricingDto();

        return JsonHelper.Deserialize<PackagePricingDto>(json);
    }
}
