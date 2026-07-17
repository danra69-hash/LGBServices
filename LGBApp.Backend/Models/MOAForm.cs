namespace LGBApp.Backend.Models;

public class MOAForm
{
    public int MOAFormId { get; set; }
    public int? JobRequestId { get; set; }
    public JobRequest? JobRequest { get; set; }
    public int? JobRequestUnitId { get; set; }
    public JobRequestUnit? JobRequestUnit { get; set; }
    public int? MOIFormId { get; set; }
    public MOIForm? MOIForm { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    /// <summary>FormDataJson shape version for future migrations.</summary>
    public int SchemaVersion { get; set; } = 1;
    public string Company { get; set; } = string.Empty;
    public string FormDataJson { get; set; } = "{}";
    public string FormTemplateCode { get; set; } = string.Empty;
    public bool FinanceRelated { get; set; }
    public bool BankSignatoryMatter { get; set; }
    public bool ShareMovement { get; set; }
    public string PackChecklistJson { get; set; } = "{}";
    public string ClientApprovalsJson { get; set; } = "[]";
    public string RejectionsJson { get; set; } = "[]";
    /// <summary>Optional Admin override of MOA approver names for this job (Start MOA).</summary>
    public string MoaApproversOverrideJson { get; set; } = "[]";
    public DateTime? SharonApprovedAt { get; set; }
    public DateTime? SubmittedForAdminReviewAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>N2: optimistic concurrency token (rotated on every write).</summary>
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
