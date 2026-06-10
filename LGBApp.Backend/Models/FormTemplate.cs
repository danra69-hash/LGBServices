namespace LGBApp.Backend.Models;

/// <summary>Admin-editable MOI/MOA form model (header, fields, issuer entity).</summary>
public class FormTemplate
{
    public int FormTemplateId { get; set; }
    public string FormType { get; set; } = "MOI";
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AddressedTo { get; set; } = "Head of Legal & Secretarial Department";
    public string DivisionLabel { get; set; } = "Secretarial Division";
    public string IssuerEntity { get; set; } = string.Empty;
    /// <summary>When set, this MOI template is used for package work items with this service name.</summary>
    public string PackageServiceName { get; set; } = string.Empty;
    public string FieldsJson { get; set; } = "[]";
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
