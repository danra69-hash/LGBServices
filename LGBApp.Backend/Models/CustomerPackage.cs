namespace LGBApp.Backend.Models;

public class CustomerPackage
{
    public int CustomerPackageId { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string PackageName { get; set; } = string.Empty;
    public decimal PackageValue { get; set; }
    public string? PackageDetail { get; set; }
    public string Validity { get; set; } = "1 Year";
    public string PricingJson { get; set; } = "{}";
    public DateTime PurchasedDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string Status { get; set; } = "Active";
}
