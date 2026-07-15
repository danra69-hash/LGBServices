using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;

namespace LGBApp.Backend.Services;

public static class FormMapper
{
    public static FormResponse ToMoiResponse(
        MOIForm form,
        WorkflowInstanceDto? workflow = null,
        Customer? customer = null) =>
        EnrichMoiApprovals(new FormResponse
        {
            Id = form.MOIFormId,
            JobId = form.JobRequestId,
            JobRequestUnitId = form.JobRequestUnitId,
            UnitNumber = ReadUnitNumber(form),
            Company = form.Company,
            Data = JsonHelper.Deserialize<Dictionary<string, object?>>(form.FormDataJson),
            FormTemplateCode = form.FormTemplateCode,
            WorkflowState = form.WorkflowState,
            FinanceRelated = form.FinanceRelated,
            BankSignatoryMatter = form.BankSignatoryMatter,
            MoiFormId = null,
            Workflow = workflow,
            CreatedAt = form.CreatedAt.ToString("yyyy-MM-dd"),
            UpdatedAt = form.UpdatedAt.ToString("O"),
            ConcurrencyStamp = form.ConcurrencyStamp.ToString("N"),
        }, form, customer);

    public static FormResponse ToMoaResponse(
        MOAForm form,
        WorkflowInstanceDto? workflow = null,
        Customer? customer = null)
    {
        var pack = MoaPackChecklistService.Parse(form);
        var (isValid, errors) = MoaPackChecklistService.Validate(form);
        return EnrichMoaApprovals(new FormResponse
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
            SharonApprovedAt = form.SharonApprovedAt?.ToString("yyyy-MM-dd"),
            SubmittedForAdminReviewAt = form.SubmittedForAdminReviewAt?.ToString("yyyy-MM-dd"),
            Workflow = workflow,
            CreatedAt = form.CreatedAt.ToString("yyyy-MM-dd"),
            UpdatedAt = form.UpdatedAt.ToString("O"),
            ConcurrencyStamp = form.ConcurrencyStamp.ToString("N"),
            UnitNumber = ReadMoaUnitNumber(form),
            JobRequestUnitId = form.JobRequestUnitId,
        }, form, customer);
    }

    private static int? ReadMoaUnitNumber(MOAForm form)
    {
        var data = JsonHelper.Deserialize<Dictionary<string, object?>>(form.FormDataJson);
        if (data.TryGetValue("unitNumber", out var raw) && raw != null
            && int.TryParse(raw.ToString(), out var n))
            return n;
        return null;
    }

    private static int? ReadUnitNumber(MOIForm form)
    {
        var data = JsonHelper.Deserialize<Dictionary<string, object?>>(form.FormDataJson);
        if (data.TryGetValue("unitNumber", out var raw) && raw != null
            && int.TryParse(raw.ToString(), out var n))
            return n;
        return null;
    }

    private static FormResponse EnrichMoiApprovals(FormResponse response, MOIForm form, Customer? customer)
    {
        var records = ClientApprovalService.ParseMoi(form);
        response.ClientApprovals = records.Select(r => new ClientApprovalDto
        {
            UserId = r.UserId,
            AccountHolderName = r.AccountHolderName,
            Comments = r.Comments,
            SignedAt = r.SignedAt.ToString("yyyy-MM-dd"),
            SignatureFileName = r.SignatureFileName,
            SignatureDataUrl = r.SignatureDataUrl,
        }).ToList();

        if (customer != null)
        {
            var requiredHolders = ClientApprovalService.GetRequiredMoiApprovalHolders(customer);
            response.RequiredApprovers = requiredHolders.Select(h => h.Name.Trim()).ToList();
            response.PendingApprovers = ClientApprovalService.MoiClientPhaseComplete(customer, records)
                ? []
                : ClientApprovalService.PendingApprovers(requiredHolders, records);
        }

        response.Rejections = FormRejectionService.ParseMoi(form)
            .Select(r => new FormRejectionDto
            {
                Stage = r.Stage,
                UserId = r.UserId,
                UserName = r.UserName,
                Reason = r.Reason,
                RejectedAt = r.RejectedAt.ToString("yyyy-MM-dd"),
            }).ToList();

        return response;
    }

    private static FormResponse EnrichMoaApprovals(FormResponse response, MOAForm form, Customer? customer)
    {
        var records = ClientApprovalService.ParseMoa(form);
        response.ClientApprovals = records.Select(r => new ClientApprovalDto
        {
            UserId = r.UserId,
            AccountHolderName = r.AccountHolderName,
            Comments = r.Comments,
            SignedAt = r.SignedAt.ToString("yyyy-MM-dd"),
            SignatureFileName = r.SignatureFileName,
            SignatureDataUrl = r.SignatureDataUrl,
        }).ToList();

        if (customer != null)
        {
            var requiredHolders = ClientApprovalService.GetRequiredMoaHolders(customer);
            response.RequiredApprovers = requiredHolders.Select(h => h.Name.Trim()).ToList();
            response.PendingApprovers = ClientApprovalService.PendingApprovers(requiredHolders, records);
        }

        response.Rejections = FormRejectionService.ParseMoa(form)
            .Select(r => new FormRejectionDto
            {
                Stage = r.Stage,
                UserId = r.UserId,
                UserName = r.UserName,
                Reason = r.Reason,
                RejectedAt = r.RejectedAt.ToString("yyyy-MM-dd"),
            }).ToList();

        return response;
    }
}
