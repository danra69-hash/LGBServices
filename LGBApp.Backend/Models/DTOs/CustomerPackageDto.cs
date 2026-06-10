namespace LGBApp.Backend.Models.DTOs;

public class CustomerPackageDto
{
    public int Id { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public decimal PackageValue { get; set; }
    public string? PackageDetail { get; set; }
    public string PurchasedDate { get; set; } = string.Empty;
    public string ExpiryDate { get; set; } = string.Empty;
    public string Validity { get; set; } = "1 Year";
    public PackagePricingDto? Pricing { get; set; }
    public string Status { get; set; } = "Active";
    public decimal ActiveValue { get; set; }
}

public class CustomerPackageInput
{
    public int? Id { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public string? PackageValue { get; set; }
    public string? PackageDetail { get; set; }
    public string? Validity { get; set; }
    public string? PurchasedDate { get; set; }
    public string? PricingJson { get; set; }
    public string Status { get; set; } = "Active";
}
