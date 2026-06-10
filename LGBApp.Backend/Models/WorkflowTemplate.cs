namespace LGBApp.Backend.Models;

public class WorkflowTemplate
{
    public int WorkflowTemplateId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string WorkflowType { get; set; } = "MOA";
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<WorkflowStepTemplate> Steps { get; set; } = new List<WorkflowStepTemplate>();
}
