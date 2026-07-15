namespace LGBApp.Backend.Models.DTOs;

public class WorkflowChoiceRequest
{
    /// <summary>MoiMoa or AdminBypass</summary>
    public string Mode { get; set; } = string.Empty;
    public int? UnitNumber { get; set; }
    /// <summary>Required when Mode is AdminBypass — what Sharon needs to do.</summary>
    public string? Note { get; set; }
}
