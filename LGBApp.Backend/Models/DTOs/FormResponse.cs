namespace LGBApp.Backend.Models.DTOs;

public class FormResponse
{
    public int Id { get; set; }
    public int? JobId { get; set; }
    public int? UnitNumber { get; set; }
    public int? JobRequestUnitId { get; set; }
    public string Company { get; set; } = string.Empty;
    public Dictionary<string, object?> Data { get; set; } = new();
    public string FormTemplateCode { get; set; } = string.Empty;
    public string WorkflowState { get; set; } = string.Empty;
    public bool FinanceRelated { get; set; }
    public bool BankSignatoryMatter { get; set; }
    public bool ShareMovement { get; set; }
    public int? MoiFormId { get; set; }
    public MoaPackChecklistDto? PackChecklist { get; set; }
    public List<string>? PackValidationErrors { get; set; }
    public WorkflowInstanceDto? Workflow { get; set; }
    public List<ClientApprovalDto>? ClientApprovals { get; set; }
    public List<FormRejectionDto>? Rejections { get; set; }
    public List<string>? RequiredApprovers { get; set; }
    public List<string>? PendingApprovers { get; set; }
    public string? SharonApprovedAt { get; set; }
    public string? SubmittedForAdminReviewAt { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}
