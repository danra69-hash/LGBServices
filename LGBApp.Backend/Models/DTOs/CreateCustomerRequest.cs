using System.ComponentModel.DataAnnotations;

namespace LGBApp.Backend.Models.DTOs;

public class CreateCustomerRequest
{
    public string? CompanyName { get; set; }
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Mobile { get; set; }
    public string? PackageName { get; set; }
    public string? PackageValue { get; set; }
    public string? Validity { get; set; }
    public string? InvoiceBy { get; set; }
    public string? ChargeTo { get; set; }
    public List<int>? InvoiceByPartyIds { get; set; }
    public List<int>? ChargeToPartyIds { get; set; }
    public bool Cosec { get; set; }
    public string? DivisionGroupCode { get; set; }
    public bool HasLoa { get; set; }
    public List<string>? LoaHolders { get; set; }
    public string? MoiFormTemplateCode { get; set; }
    public string? MoaFormTemplateCode { get; set; }
    public string? DateCreated { get; set; }
    public List<CustomerPackageInput> Packages { get; set; } = new();
    public List<AccountHolderInput> AccountHolders { get; set; } = new();
}

public class AccountHolderInput
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool Moi { get; set; }
    public bool MoiApproval { get; set; }
    public bool Moa { get; set; }
}
