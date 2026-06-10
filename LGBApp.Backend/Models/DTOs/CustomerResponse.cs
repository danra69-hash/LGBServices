namespace LGBApp.Backend.Models.DTOs;

public class CustomerResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string LastContact { get; set; } = string.Empty;
    public string InvoiceBy { get; set; } = string.Empty;
    public string ChargeTo { get; set; } = string.Empty;
    public List<int> InvoiceByPartyIds { get; set; } = [];
    public List<int> ChargeToPartyIds { get; set; } = [];
    public string Package { get; set; } = string.Empty;
    public decimal PackageValue { get; set; }
    public bool Cosec { get; set; }
    public string DivisionGroupCode { get; set; } = string.Empty;
    public bool HasLoa { get; set; }
    public List<string> LoaHolders { get; set; } = new();
    public string? MoiFormTemplateCode { get; set; }
    public string? MoaFormTemplateCode { get; set; }
    public List<string> Moi { get; set; } = new();
    public List<string> MoiApproval { get; set; } = new();
    public List<string> Moa { get; set; } = new();
    public string PurchasedDate { get; set; } = string.Empty;
    public string ExpiryDate { get; set; } = string.Empty;
    public List<CustomerPackageDto> Packages { get; set; } = new();
    public List<AccountHolderDto> AccountHolders { get; set; } = new();
}
