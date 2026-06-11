using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public static class JobHandoffStatuses
{
    public const string ClientSubmitted = "ClientSubmitted";
    public const string PendingPrep = "PendingPrep";
    public const string ResoInProgress = "ResoInProgress";
    public const string AdminReview = "AdminReview";
    public const string MoaSharonApproved = "MoaSharonApproved";
    public const string ReadyForMoa = "ReadyForMoa";
    public const string MoaCirculation = "MoaCirculation";
    public const string PendingExecute = "PendingExecute";
    public const string Completed = "Completed";

    public static readonly string[] Ordered =
    [
        ClientSubmitted, PendingPrep, ResoInProgress, AdminReview,
        MoaSharonApproved, ReadyForMoa, MoaCirculation, PendingExecute, Completed,
    ];
}

public static class MoiWorkflowStates
{
    public const string Draft = "Draft";
    public const string PendingClientMoiApproval = "PendingClientMoiApproval";
    public const string PendingAdminIntake = "PendingAdminIntake";
    public const string PendingPrep = "PendingPrep";
    public const string PendingRecommendation = "PendingRecommendation";
    public const string Approved = "Approved";
}

public static class JobHandoffService
{
    public static void SetHandoff(JobRequest job, string status) =>
        job.InternalHandoffStatus = status;

    public static async Task OnClientMoiIssuedAsync(
        AppDbContext context,
        JobRequest job,
        MOIForm moiForm,
        JobRequestUnit? unit = null)
    {
        moiForm.WorkflowState = MoiWorkflowStates.Draft;
        if (job.TotalQty <= 1 && string.IsNullOrWhiteSpace(job.InternalHandoffStatus))
            SetHandoff(job, string.Empty);
        if (unit != null && job.TotalQty > 1)
            unit.InternalHandoffStatus = string.Empty;
        if (job.Status == "Pending")
            job.Status = "In Progress";
        moiForm.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public static async Task OnMoiSubmittedForApprovalAsync(
        AppDbContext context,
        JobRequest job,
        MOIForm form,
        Customer customer)
    {
        var records = ClientApprovalService.ParseMoi(form);
        if (ClientApprovalService.MoiClientPhaseComplete(customer, records))
        {
            await OnClientMoiPhaseCompleteAsync(context, job, form);
            return;
        }

        var required = ClientApprovalService.GetRequiredMoiApproverNames(customer);
        if (required.Count == 0)
        {
            await OnClientMoiPhaseCompleteAsync(context, job, form);
            return;
        }

        form.WorkflowState = MoiWorkflowStates.PendingClientMoiApproval;
        form.UpdatedAt = DateTime.UtcNow;
        job.Status = "In Progress";

        await context.SaveChangesAsync();
    }

    public static async Task OnClientMoiApprovalRecordedAsync(
        AppDbContext context,
        JobRequest job,
        MOIForm form,
        Customer customer)
    {
        var records = ClientApprovalService.ParseMoi(form);
        if (!ClientApprovalService.MoiClientPhaseComplete(customer, records))
        {
            form.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            return;
        }

        await OnClientMoiPhaseCompleteAsync(context, job, form);
    }

    private static async Task OnClientMoiPhaseCompleteAsync(AppDbContext context, JobRequest job, MOIForm form)
    {
        form.WorkflowState = MoiWorkflowStates.PendingAdminIntake;
        form.UpdatedAt = DateTime.UtcNow;

        JobRequestUnit? unit = null;
        if (form.JobRequestUnitId.HasValue)
            unit = await context.JobRequestUnits.FindAsync(form.JobRequestUnitId.Value);

        if (unit != null && job.TotalQty > 1)
        {
            unit.InternalHandoffStatus = JobHandoffStatuses.ClientSubmitted;
            job.Status = "In Progress";
        }
        else
        {
            SetHandoff(job, JobHandoffStatuses.ClientSubmitted);
            job.Status = "Pending";
        }

        var docs = context.JobItemDocuments.Where(d => d.JobRequestId == job.JobRequestId);
        if (form.JobRequestUnitId.HasValue)
        {
            docs = docs.Where(d =>
                d.JobRequestUnitId == form.JobRequestUnitId
                || d.JobRequestUnitId == null);
        }

        foreach (var doc in await docs.ToListAsync())
            doc.VisibleToInternal = true;

        await context.SaveChangesAsync();
    }

    public static async Task OnMoiIntakeApprovedAsync(AppDbContext context, JobRequest job)
    {
        SetHandoff(job, JobHandoffStatuses.PendingPrep);
        job.Status = "In Progress";

        var moiForm = await context.MOIForms.FirstOrDefaultAsync(f => f.JobRequestId == job.JobRequestId);
        if (moiForm != null)
        {
            moiForm.WorkflowState = MoiWorkflowStates.PendingPrep;
            moiForm.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
    }

    public static async Task OnJobAcceptedAsync(AppDbContext context, JobRequest job)
    {
        if (job.TaskType is "MOI" or "MOI Approval")
        {
            if (job.InternalHandoffStatus is JobHandoffStatuses.ClientSubmitted or "")
                SetHandoff(job, JobHandoffStatuses.PendingPrep);
            if (job.Status == "Pending")
                job.Status = "In Progress";
        }
        await context.SaveChangesAsync();
    }

    public static async Task OnMoiRecommendedAsync(AppDbContext context, MOIForm form)
    {
        if (!form.JobRequestId.HasValue) return;

        var job = await context.JobRequests.FindAsync(form.JobRequestId.Value);
        if (job == null) return;

        form.WorkflowState = MoiWorkflowStates.Approved;
        form.UpdatedAt = DateTime.UtcNow;
        SetHandoff(job, JobHandoffStatuses.AdminReview);
        job.Status = "In Progress";

        await JobFormProvisioner.EnsureMoaFormAsync(context, job);
        await context.SaveChangesAsync();
    }

    public static async Task OnMoiApprovedAsync(AppDbContext context, MOIForm form)
    {
        await OnMoiRecommendedAsync(context, form);
    }

    public static async Task OnSharonMoaApprovedAsync(AppDbContext context, JobRequest job, MOAForm? moaForm)
    {
        if (job.InternalHandoffStatus != JobHandoffStatuses.AdminReview)
            throw new InvalidOperationException("MOA must be in head secretary review before approval.");

        SetHandoff(job, JobHandoffStatuses.MoaSharonApproved);
        if (moaForm != null)
        {
            moaForm.SharonApprovedAt = DateTime.UtcNow;
            moaForm.UpdatedAt = DateTime.UtcNow;
        }
        await context.SaveChangesAsync();
    }

    public static async Task AdvanceToReadyForMoaAsync(AppDbContext context, JobRequest job)
    {
        if (job.InternalHandoffStatus != JobHandoffStatuses.MoaSharonApproved)
            throw new InvalidOperationException("MOA must be approved by Sharon before release to client.");

        SetHandoff(job, JobHandoffStatuses.ReadyForMoa);
        job.Status = "In Progress";
        await context.SaveChangesAsync();
    }

    public static async Task OnClientMoaApprovalRecordedAsync(
        AppDbContext context,
        JobRequest job,
        MOAForm form,
        Customer customer)
    {
        var required = ClientApprovalService.GetRequiredMoaApproverNames(customer);
        var records = ClientApprovalService.ParseMoa(form);
        if (!ClientApprovalService.MoaClientPhaseComplete(customer, records))
        {
            SetHandoff(job, JobHandoffStatuses.MoaCirculation);
            form.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            return;
        }

        SetHandoff(job, JobHandoffStatuses.PendingExecute);
        job.Status = "In Progress";
        form.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
    }

    public static async Task OnMoaWorkflowStartedAsync(AppDbContext context, MOAForm form)
    {
        if (!form.JobRequestId.HasValue) return;
        var job = await context.JobRequests.FindAsync(form.JobRequestId.Value);
        if (job == null) return;
        // Internal LGB routing only — client circulation starts after Sharon releases (ReadyForMoa).
        if (string.IsNullOrWhiteSpace(job.InternalHandoffStatus)
            || job.InternalHandoffStatus is JobHandoffStatuses.PendingPrep
                or JobHandoffStatuses.ResoInProgress)
            SetHandoff(job, JobHandoffStatuses.AdminReview);
        job.Status = "In Progress";
        await context.SaveChangesAsync();
    }

    public static async Task OnMoiClientRejectedAsync(
        AppDbContext context,
        JobRequest job,
        MOIForm form,
        User user,
        string reason)
    {
        form.WorkflowState = MoiWorkflowStates.Draft;
        form.ClientApprovalsJson = "[]";
        form.UpdatedAt = DateTime.UtcNow;
        FormRejectionService.AddMoiRejection(form, new FormRejectionRecord
        {
            Stage = FormRejectionStages.MoiClientApproval,
            UserId = user.UserId,
            UserName = user.Name,
            Reason = reason.Trim(),
            RejectedAt = DateTime.UtcNow,
        });

        if (form.JobRequestUnitId.HasValue)
        {
            var unit = await context.JobRequestUnits.FindAsync(form.JobRequestUnitId.Value);
            if (unit != null)
                unit.InternalHandoffStatus = string.Empty;
        }
        else
            SetHandoff(job, string.Empty);

        job.Status = "In Progress";
        await context.SaveChangesAsync();
    }

    public static async Task OnMoiIntakeRejectedAsync(
        AppDbContext context,
        JobRequest job,
        MOIForm form,
        User user,
        string reason)
    {
        form.WorkflowState = MoiWorkflowStates.Draft;
        form.UpdatedAt = DateTime.UtcNow;
        FormRejectionService.AddMoiRejection(form, new FormRejectionRecord
        {
            Stage = FormRejectionStages.MoiIntake,
            UserId = user.UserId,
            UserName = user.Name,
            Reason = reason.Trim(),
            RejectedAt = DateTime.UtcNow,
        });

        if (form.JobRequestUnitId.HasValue)
        {
            var unit = await context.JobRequestUnits.FindAsync(form.JobRequestUnitId.Value);
            if (unit != null)
                unit.InternalHandoffStatus = string.Empty;
        }
        else
            SetHandoff(job, string.Empty);

        job.Status = "In Progress";

        var docs = context.JobItemDocuments.Where(d => d.JobRequestId == job.JobRequestId);
        if (form.JobRequestUnitId.HasValue)
        {
            docs = docs.Where(d =>
                d.JobRequestUnitId == form.JobRequestUnitId
                || d.JobRequestUnitId == null);
        }

        foreach (var doc in await docs.ToListAsync())
            doc.VisibleToInternal = false;

        await context.SaveChangesAsync();
    }

    public static async Task OnMoaSharonRejectedAsync(
        AppDbContext context,
        JobRequest job,
        MOAForm form,
        User user,
        string reason)
    {
        form.SharonApprovedAt = null;
        form.UpdatedAt = DateTime.UtcNow;
        FormRejectionService.AddMoaRejection(form, new FormRejectionRecord
        {
            Stage = FormRejectionStages.MoaSharonReview,
            UserId = user.UserId,
            UserName = user.Name,
            Reason = reason.Trim(),
            RejectedAt = DateTime.UtcNow,
        });

        SetHandoff(job, JobHandoffStatuses.AdminReview);
        job.Status = "In Progress";
        await context.SaveChangesAsync();
    }

    public static async Task OnMoaClientRejectedAsync(
        AppDbContext context,
        JobRequest job,
        MOAForm form,
        User user,
        string reason)
    {
        form.ClientApprovalsJson = "[]";
        form.SharonApprovedAt = null;
        form.UpdatedAt = DateTime.UtcNow;
        FormRejectionService.AddMoaRejection(form, new FormRejectionRecord
        {
            Stage = FormRejectionStages.MoaClientApproval,
            UserId = user.UserId,
            UserName = user.Name,
            Reason = reason.Trim(),
            RejectedAt = DateTime.UtcNow,
        });

        SetHandoff(job, JobHandoffStatuses.AdminReview);
        job.Status = "In Progress";
        await context.SaveChangesAsync();
    }

    public static async Task OnMoaWorkflowCompletedAsync(AppDbContext context, int moaFormId)
    {
        var form = await context.MOAForms.FindAsync(moaFormId);
        if (form?.JobRequestId == null) return;

        var job = await context.JobRequests.FindAsync(form.JobRequestId.Value);
        if (job == null) return;

        SetHandoff(job, JobHandoffStatuses.Completed);
        job.Status = "Completed";
        job.UsedQty = Math.Max(job.UsedQty, 1);
        job.DateCompleted = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }
}
