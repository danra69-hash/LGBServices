namespace LGBApp.Backend.Models;

public class WorkflowInstance
{
    public int WorkflowInstanceId { get; set; }
    public int WorkflowTemplateId { get; set; }
    public WorkflowTemplate WorkflowTemplate { get; set; } = null!;
    public string FormType { get; set; } = "MOA";
    public int? MoiFormId { get; set; }
    public MOIForm? MoiForm { get; set; }
    public int? MoaFormId { get; set; }
    public MOAForm? MoaForm { get; set; }
    public string Status { get; set; } = "Active";
    public int CurrentStepOrder { get; set; } = 1;
    public string ConditionsJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<WorkflowStepInstance> Steps { get; set; } = new List<WorkflowStepInstance>();
}
