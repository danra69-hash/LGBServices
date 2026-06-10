using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;

namespace LGBApp.Backend.Services;

public static class FormMapper
{
    public static FormResponse ToMoiResponse(MOIForm form, WorkflowInstanceDto? workflow = null) => new()
    {
        Id = form.MOIFormId,
        JobId = form.JobRequestId,
        Company = form.Company,
        Data = JsonHelper.Deserialize<Dictionary<string, object?>>(form.FormDataJson),
        FormTemplateCode = form.FormTemplateCode,
        WorkflowState = form.WorkflowState,
        FinanceRelated = form.FinanceRelated,
        BankSignatoryMatter = form.BankSignatoryMatter,
        MoiFormId = null,
        Workflow = workflow,
        CreatedAt = form.CreatedAt.ToString("yyyy-MM-dd"),
        UpdatedAt = form.UpdatedAt.ToString("yyyy-MM-dd"),
    };

    public static FormResponse ToMoaResponse(MOAForm form, WorkflowInstanceDto? workflow = null)
    {
        var pack = MoaPackChecklistService.Parse(form);
        var (isValid, errors) = MoaPackChecklistService.Validate(form);
        return new FormResponse
        {
            Id = form.MOAFormId,
            JobId = form.JobRequestId,
            MoiFormId = form.MOIFormId,
            Company = form.Company,
            Data = JsonHelper.Deserialize<Dictionary<string, object?>>(form.FormDataJson),
            FormTemplateCode = form.FormTemplateCode,
            WorkflowState = workflow?.Status ?? string.Empty,
            FinanceRelated = form.FinanceRelated,
            BankSignatoryMatter = form.BankSignatoryMatter,
            ShareMovement = form.ShareMovement,
            PackChecklist = new MoaPackChecklistDto
            {
                InternalChecklistA = pack.InternalChecklistA,
                InternalChecklistB = pack.InternalChecklistB,
                CleanAgreementAttached = pack.CleanAgreementAttached,
                ShareholdingTableAttached = pack.ShareholdingTableAttached,
                SsmRegistrationNo = pack.SsmRegistrationNo,
                SsmNewRegistrationNo = pack.SsmNewRegistrationNo,
                SsmEntityType = pack.SsmEntityType,
                SsmStatus = pack.SsmStatus,
                SsmAsAtDate = pack.SsmAsAtDate,
            },
            PackValidationErrors = isValid ? null : errors,
            Workflow = workflow,
            CreatedAt = form.CreatedAt.ToString("yyyy-MM-dd"),
            UpdatedAt = form.UpdatedAt.ToString("yyyy-MM-dd"),
        };
    }
}
