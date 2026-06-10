namespace LGBApp.Backend.Models;

public class Customer
{
    public int CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public decimal Value { get; set; }
    public DateTime LastContact { get; set; }
    public string InvoiceBy { get; set; } = string.Empty;
    public string ChargeTo { get; set; } = string.Empty;
    public string Package { get; set; } = string.Empty;
    public decimal PackageValue { get; set; }
    public bool Cosec { get; set; }
    public string DivisionGroupCode { get; set; } = string.Empty;
    public bool HasLoa { get; set; }
    public string LoaHoldersJson { get; set; } = "[]";
    public string? MoiFormTemplateCode { get; set; }
    public string? MoaFormTemplateCode { get; set; }
    public string MoiJson { get; set; } = "[]";
    public string MoiApprovalJson { get; set; } = "[]";
    public string MoaJson { get; set; } = "[]";
    public DateTime PurchasedDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public ICollection<AccountHolder> AccountHolders { get; set; } = new List<AccountHolder>();
    public ICollection<CustomerPackage> Packages { get; set; } = new List<CustomerPackage>();
}
