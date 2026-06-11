namespace LGBApp.Backend.Models.DTOs;

public class FormRequest
{
    public int? JobId { get; set; }
    /// <summary>Session number for multi-qty service lines (Board meeting #2, etc.).</summary>
    public int? UnitNumber { get; set; }
    public int? MoiFormId { get; set; }
    public string Company { get; set; } = string.Empty;
    public string? FormTemplateCode { get; set; }
    public bool FinanceRelated { get; set; }
    public bool BankSignatoryMatter { get; set; }
    public bool ShareMovement { get; set; }
    public Dictionary<string, object?> Data { get; set; } = new();
}
