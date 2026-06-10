namespace LGBApp.Backend.Models;

public class WorkflowStepInstance
{
    public int WorkflowStepInstanceId { get; set; }
    public int WorkflowInstanceId { get; set; }
    public WorkflowInstance WorkflowInstance { get; set; } = null!;
    public int StepOrder { get; set; }
    public string StepKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ConditionType { get; set; } = "Always";
    public string AssigneeType { get; set; } = string.Empty;
    public int? AssigneeUserId { get; set; }
    public string AssigneeName { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string Comments { get; set; } = string.Empty;
    public bool AdminOverridden { get; set; }
    public int? OverriddenByUserId { get; set; }
}
