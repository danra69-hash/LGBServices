namespace LGBApp.Backend.Models.DTOs;

public class JobRequestUnitDto
{
    public int Id { get; set; }
    public int UnitNumber { get; set; }
    public int? AssignedUserId { get; set; }
    public string AssignedUserName { get; set; } = string.Empty;
    public List<UnitAssigneeDto> Assignees { get; set; } = [];
    public string? ScheduledDate { get; set; }
    public string Status { get; set; } = "Pending";
    public string InternalHandoffStatus { get; set; } = string.Empty;
    public string? LinkedFormKind { get; set; }
    public int? LinkedFormId { get; set; }
    public bool HasMoiForm { get; set; }
    public bool HasMoaForm { get; set; }
    public int? MoiFormId { get; set; }
    public string? MoiWorkflowState { get; set; }
    public string? RequiredExecutionDate { get; set; }
    public string DisplayStatus { get; set; } = string.Empty;
    public string DisplayStatusKey { get; set; } = string.Empty;
    public bool AwaitingIntakeApproval { get; set; }
}

public class WorkTrackerItemDto
{
    public int UnitId { get; set; }
    public int JobId { get; set; }
    public int UnitNumber { get; set; }
    public int? CustomerPackageId { get; set; }
    public string Customer { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public string? ScheduledDate { get; set; }
    public string DateRequested { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? AssignedUserId { get; set; }
    public string AssignedUserName { get; set; } = string.Empty;
    public List<UnitAssigneeDto> Assignees { get; set; } = [];
    public string InternalHandoffStatus { get; set; } = string.Empty;
    public string DisplayStatus { get; set; } = string.Empty;
    public string DisplayStatusKey { get; set; } = string.Empty;
    public string? LinkedFormKind { get; set; }
    public int? LinkedFormId { get; set; }
    public bool HasMoiForm { get; set; }
    public bool HasMoaForm { get; set; }
    public int? MoiFormId { get; set; }
    public string? MoiWorkflowState { get; set; }
    public string? RequiredExecutionDate { get; set; }
}
