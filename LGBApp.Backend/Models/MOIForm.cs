namespace LGBApp.Backend.Models;

public class MOIForm
{
    public int MOIFormId { get; set; }
    public int? JobRequestId { get; set; }
    public JobRequest? JobRequest { get; set; }
    public string Company { get; set; } = string.Empty;
    public string FormDataJson { get; set; } = "{}";
    public string FormTemplateCode { get; set; } = string.Empty;
    public string WorkflowState { get; set; } = "Draft";
    public bool FinanceRelated { get; set; }
    public bool BankSignatoryMatter { get; set; }
    public int? RecommendedByUserId { get; set; }
    public DateTime? RecommendedAt { get; set; }
    public string RecommendationComments { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
