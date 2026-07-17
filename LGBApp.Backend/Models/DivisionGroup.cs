namespace LGBApp.Backend.Models;

public class DivisionGroup
{
    public int DivisionGroupId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>MOA_NO_LOA, MOA_WITH_LOA, or MOA_SWM — links to WorkflowTemplate.Code</summary>
    public string MoaWorkflowTemplateCode { get; set; } = "MOA_NO_LOA";
    public string? DefaultMoiFormTemplateCode { get; set; }
    public string? DefaultMoaFormTemplateCode { get; set; }
    /// <summary>Display names for flowchart MS5 (group mandatory MOA approvers). JSON string[].</summary>
    public string MandatoryMoaApproversJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public ICollection<DivisionGroupRecommender> Recommenders { get; set; } = new List<DivisionGroupRecommender>();
}
