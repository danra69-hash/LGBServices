namespace LGBApp.Backend.Models;

public class JobRequest
{
    public int JobRequestId { get; set; }
    public int? CustomerId { get; set; }
    public int? CustomerPackageId { get; set; }
    public Customer? CustomerRecord { get; set; }
    public CustomerPackage? CustomerPackage { get; set; }
    public string Customer { get; set; } = string.Empty;
    /// <summary>MOI, MOI Approval, MOA, or Service.</summary>
    public string TaskType { get; set; } = "Service";
    public string Service { get; set; } = string.Empty;
    public int UsedQty { get; set; }
    public int TotalQty { get; set; }
    public DateTime DateRequested { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime? DateCompleted { get; set; }
    /// <summary>External signer at the client company (not a system user).</summary>
    public string AccountHolder { get; set; } = string.Empty;
    public string AccountHolderEmail { get; set; } = string.Empty;
    public string AccountHolderPhone { get; set; } = string.Empty;
    public int? AssignedUserId { get; set; }
    public string JobAssignedTo { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    /// <summary>Internal operational handoff: ClientSubmitted → PendingPrep → ResoInProgress → AdminReview → ReadyForMoa → MoaCirculation → Completed</summary>
    public string InternalHandoffStatus { get; set; } = string.Empty;
    public string? AssignmentComments { get; set; }
    public ICollection<JobRequestUnit> Units { get; set; } = new List<JobRequestUnit>();
}
