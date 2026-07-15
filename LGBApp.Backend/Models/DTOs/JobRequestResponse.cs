namespace LGBApp.Backend.Models.DTOs;

public class JobRequestResponse
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public int? CustomerPackageId { get; set; }
    public string Customer { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public int UsedQty { get; set; }
    public int TotalQty { get; set; }
    public string DateRequested { get; set; } = string.Empty;
    public string? ScheduledDate { get; set; }
    public string? DateCompleted { get; set; }
    public string AccountHolder { get; set; } = string.Empty;
    public string AccountHolderEmail { get; set; } = string.Empty;
    public string AccountHolderPhone { get; set; } = string.Empty;
    public int? AssignedUserId { get; set; }
    public string JobAssignedTo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string InternalHandoffStatus { get; set; } = string.Empty;
    public string? AssignmentComments { get; set; }
    public string WorkflowMode { get; set; } = string.Empty;
    public string AdminBypassNote { get; set; } = string.Empty;
    public string? AdminBypassAt { get; set; }
    public List<JobRequestUnitDto> Units { get; set; } = [];
    public string? LinkedFormKind { get; set; }
    public int? LinkedFormId { get; set; }
    public bool HasMoiForm { get; set; }
    public bool HasMoaForm { get; set; }
    public string? MoiWorkflowState { get; set; }
    public bool AwaitingIntakeApproval { get; set; }
    public string? TaskPhase { get; set; }
    public string DisplayStatus { get; set; } = string.Empty;
    public string DisplayStatusKey { get; set; } = string.Empty;
    /// <summary>MOI document title when set — preferred display name for the work item.</summary>
    public string? DocumentTitle { get; set; }
}
