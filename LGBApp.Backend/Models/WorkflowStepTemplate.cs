namespace LGBApp.Backend.Models;

public class WorkflowStepTemplate
{
    public int WorkflowStepTemplateId { get; set; }
    public int WorkflowTemplateId { get; set; }
    public WorkflowTemplate WorkflowTemplate { get; set; } = null!;
    public int StepOrder { get; set; }
    public string StepKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ConditionType { get; set; } = "Always";
    public string AssigneeType { get; set; } = "JobTitle";
    public string? AssigneeRole { get; set; }
    public int? AssigneeUserId { get; set; }
    public string? AssigneeDisplayName { get; set; }
    public bool AllowAdminOverride { get; set; } = true;
}
