using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;

namespace LGBApp.Backend.Services;

public static class TemplateMapper
{
    public static FormTemplateDto ToDto(FormTemplate t) => new()
    {
        Id = t.FormTemplateId,
        FormType = t.FormType,
        Code = t.Code,
        Name = t.Name,
        Description = t.Description,
        AddressedTo = t.AddressedTo,
        DivisionLabel = t.DivisionLabel,
        IssuerEntity = t.IssuerEntity,
        PackageServiceName = t.PackageServiceName,
        Fields = JsonHelper.Deserialize<List<FormFieldDefinitionDto>>(t.FieldsJson),
        IsDefault = t.IsDefault,
        IsActive = t.IsActive,
    };

    public static WorkflowTemplateDto ToDto(WorkflowTemplate t) => new()
    {
        Id = t.WorkflowTemplateId,
        Code = t.Code,
        Name = t.Name,
        WorkflowType = t.WorkflowType,
        Description = t.Description,
        IsActive = t.IsActive,
        Steps = t.Steps.OrderBy(s => s.StepOrder).Select(ToStepDto).ToList(),
    };

    public static WorkflowStepTemplateDto ToStepDto(WorkflowStepTemplate s) => new()
    {
        Id = s.WorkflowStepTemplateId,
        StepOrder = s.StepOrder,
        StepKey = s.StepKey,
        DisplayName = s.DisplayName,
        ConditionType = s.ConditionType,
        AssigneeType = s.AssigneeType,
        AssigneeRole = s.AssigneeRole,
        AssigneeUserId = s.AssigneeUserId,
        AssigneeDisplayName = s.AssigneeDisplayName,
        AllowAdminOverride = s.AllowAdminOverride,
    };

    public static DivisionGroupDto ToDto(DivisionGroup g) => new()
    {
        Id = g.DivisionGroupId,
        Code = g.Code,
        Name = g.Name,
        MoaWorkflowTemplateCode = g.MoaWorkflowTemplateCode,
        DefaultMoiFormTemplateCode = g.DefaultMoiFormTemplateCode,
        DefaultMoaFormTemplateCode = g.DefaultMoaFormTemplateCode,
        IsActive = g.IsActive,
        Recommenders = g.Recommenders.Select(r => new DivisionGroupRecommenderDto
        {
            Id = r.DivisionGroupRecommenderId,
            UserId = r.UserId,
            DisplayName = r.DisplayName,
        }).ToList(),
    };

    public static void ApplyFormTemplate(FormTemplate entity, FormTemplateDto dto)
    {
        entity.FormType = dto.FormType;
        entity.Code = dto.Code;
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.AddressedTo = dto.AddressedTo;
        entity.DivisionLabel = dto.DivisionLabel;
        entity.IssuerEntity = dto.IssuerEntity;
        entity.PackageServiceName = dto.PackageServiceName?.Trim() ?? string.Empty;
        entity.FieldsJson = JsonHelper.Serialize(dto.Fields);
        entity.IsDefault = dto.IsDefault;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
    }
}
