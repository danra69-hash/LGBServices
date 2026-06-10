namespace LGBApp.Backend.Models.DTOs;

public class WorkflowInstanceDto
{
    public int Id { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string FormType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int CurrentStepOrder { get; set; }
    public WorkflowConditionsDto Conditions { get; set; } = new();
    public List<WorkflowStepInstanceDto> Steps { get; set; } = new();
}

public class WorkflowConditionsDto
{
    public bool FinanceRelated { get; set; }
    public bool BankSignatory { get; set; }
    public bool ShareMovement { get; set; }
}

public class WorkflowStepInstanceDto
{
    public int Id { get; set; }
    public int StepOrder { get; set; }
    public string StepKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AssigneeName { get; set; } = string.Empty;
    public int? AssigneeUserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ApprovedAt { get; set; }
    public string Comments { get; set; } = string.Empty;
    public bool AdminOverridden { get; set; }
    public bool IsCurrent { get; set; }
}

public class WorkflowTemplateDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string WorkflowType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<WorkflowStepTemplateDto> Steps { get; set; } = new();
}

public class WorkflowStepTemplateDto
{
    public int Id { get; set; }
    public int StepOrder { get; set; }
    public string StepKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ConditionType { get; set; } = "Always";
    public string AssigneeType { get; set; } = string.Empty;
    public string? AssigneeRole { get; set; }
    public int? AssigneeUserId { get; set; }
    public string? AssigneeDisplayName { get; set; }
    public bool AllowAdminOverride { get; set; } = true;
}

public class FormTemplateDto
{
    public int Id { get; set; }
    public string FormType { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AddressedTo { get; set; } = string.Empty;
    public string DivisionLabel { get; set; } = string.Empty;
    public string IssuerEntity { get; set; } = string.Empty;
    public string PackageServiceName { get; set; } = string.Empty;
    public List<FormFieldDefinitionDto> Fields { get; set; } = new();
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

public class FormFieldDefinitionDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public bool Required { get; set; }
    public string Section { get; set; } = "main";
}

public class DivisionGroupDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MoaWorkflowTemplateCode { get; set; } = string.Empty;
    public string? DefaultMoiFormTemplateCode { get; set; }
    public string? DefaultMoaFormTemplateCode { get; set; }
    public bool IsActive { get; set; }
    public List<DivisionGroupRecommenderDto> Recommenders { get; set; } = new();
}

public class DivisionGroupRecommenderDto
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

public class ApproveWorkflowStepRequest
{
    public string Comments { get; set; } = string.Empty;
}

public class AdminOverrideStepRequest
{
    public int StepInstanceId { get; set; }
    public string Comments { get; set; } = string.Empty;
}

public class RecommendMoiRequest
{
    public string Comments { get; set; } = string.Empty;
}

public class DivisionGroupImportRow
{
    public string Company { get; set; } = string.Empty;
    public string DivisionGroup { get; set; } = string.Empty;
    public bool HasLoa { get; set; }
}

public class DivisionGroupImportResult
{
    public int Updated { get; set; }
    public List<string> Unmatched { get; set; } = new();
}
