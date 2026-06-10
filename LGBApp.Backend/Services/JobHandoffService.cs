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
    public const string ReadyForMoa = "ReadyForMoa";
    public const string MoaCirculation = "MoaCirculation";
    public const string Completed = "Completed";

    public static readonly string[] Ordered =
    [
        ClientSubmitted, PendingPrep, ResoInProgress, AdminReview, ReadyForMoa, MoaCirculation, Completed,
    ];
}

public static class JobHandoffService
{
    public static void SetHandoff(JobRequest job, string status)
    {
        job.InternalHandoffStatus = status;
    }

    public static async Task OnClientMoiIssuedAsync(AppDbContext context, JobRequest job, MOIForm moiForm)
    {
        SetHandoff(job, JobHandoffStatuses.ClientSubmitted);
        moiForm.WorkflowState = "PendingPrep";
        job.Status = "Pending";
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

        SetHandoff(job, JobHandoffStatuses.ResoInProgress);

        var approvalJob = await context.JobRequests
            .FirstOrDefaultAsync(j =>
                j.CustomerId == job.CustomerId
                && j.TaskType == "MOI Approval"
                && j.AccountHolder == job.AccountHolder
                && j.Status != "Completed");

        if (approvalJob != null)
        {
            approvalJob.Status = "In Progress";
            SetHandoff(approvalJob, JobHandoffStatuses.ResoInProgress);
        }

        await context.SaveChangesAsync();
    }

    public static async Task OnMoiApprovedAsync(AppDbContext context, MOIForm form)
    {
        if (!form.JobRequestId.HasValue) return;

        var job = await context.JobRequests.FindAsync(form.JobRequestId.Value);
        if (job == null) return;

        SetHandoff(job, JobHandoffStatuses.AdminReview);
        job.Status = "Completed";
        job.UsedQty = Math.Max(job.UsedQty, 1);

        var approvalJob = await context.JobRequests
            .FirstOrDefaultAsync(j =>
                j.CustomerId == job.CustomerId
                && j.TaskType == "MOI Approval"
                && j.AccountHolder == job.AccountHolder
                && j.Status != "Completed");

        if (approvalJob != null)
        {
            approvalJob.Status = "In Progress";
            SetHandoff(approvalJob, JobHandoffStatuses.AdminReview);
        }

        await context.SaveChangesAsync();
    }

    public static async Task AdvanceToReadyForMoaAsync(AppDbContext context, JobRequest job)
    {
        SetHandoff(job, JobHandoffStatuses.ReadyForMoa);

        var moaJob = await context.JobRequests
            .FirstOrDefaultAsync(j =>
                j.CustomerId == job.CustomerId
                && j.TaskType == "MOA"
                && j.AccountHolder == job.AccountHolder
                && j.Status != "Completed");

        if (moaJob != null)
        {
            moaJob.Status = "In Progress";
            SetHandoff(moaJob, JobHandoffStatuses.ReadyForMoa);
        }

        await context.SaveChangesAsync();
    }

    public static async Task OnMoaWorkflowStartedAsync(AppDbContext context, MOAForm form)
    {
        if (!form.JobRequestId.HasValue) return;
        var job = await context.JobRequests.FindAsync(form.JobRequestId.Value);
        if (job == null) return;
        SetHandoff(job, JobHandoffStatuses.MoaCirculation);
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
