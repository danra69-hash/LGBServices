namespace LGBApp.Backend.Models;

public class MOIForm
{
    public int MOIFormId { get; set; }
    public int? JobRequestId { get; set; }
    public JobRequest? JobRequest { get; set; }
    /// <summary>Package session — one MOI per unit for multi-qty services.</summary>
    public int? JobRequestUnitId { get; set; }
    public JobRequestUnit? JobRequestUnit { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    /// <summary>FormDataJson shape version for future migrations.</summary>
    public int SchemaVersion { get; set; } = 1;
    public string Company { get; set; } = string.Empty;
    public string FormDataJson { get; set; } = "{}";
    public string FormTemplateCode { get; set; } = string.Empty;
    public string WorkflowState { get; set; } = "Draft";
    public bool FinanceRelated { get; set; }
    public bool BankSignatoryMatter { get; set; }
    public int? RecommendedByUserId { get; set; }
    public DateTime? RecommendedAt { get; set; }
    public string RecommendationComments { get; set; } = string.Empty;
    public string ClientApprovalsJson { get; set; } = "[]";
    public string RejectionsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>N2: optimistic concurrency token (rotated on every write).</summary>
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
