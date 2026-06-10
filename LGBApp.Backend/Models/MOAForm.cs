namespace LGBApp.Backend.Models;

public class MOAForm
{
    public int MOAFormId { get; set; }
    public int? JobRequestId { get; set; }
    public JobRequest? JobRequest { get; set; }
    public int? MOIFormId { get; set; }
    public MOIForm? MOIForm { get; set; }
    public string Company { get; set; } = string.Empty;
    public string FormDataJson { get; set; } = "{}";
    public string FormTemplateCode { get; set; } = string.Empty;
    public bool FinanceRelated { get; set; }
    public bool BankSignatoryMatter { get; set; }
    public bool ShareMovement { get; set; }
    public string PackChecklistJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
