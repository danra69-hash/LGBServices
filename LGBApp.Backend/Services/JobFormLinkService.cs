using System.Security.Claims;
using LGBApp.Backend.Data;
using LGBApp.Backend.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public static class JobFormLinkService
{
    public static async Task EnrichWithFormLinksAsync(
        AppDbContext context,
        IEnumerable<JobRequestResponse> jobs,
        ClaimsPrincipal? user = null)
    {
        var list = jobs.ToList();
        if (list.Count == 0) return;

        var ids = list.Select(j => j.Id).ToList();
        var moiForms = await context.MOIForms
            .Where(f => f.JobRequestId != null && ids.Contains(f.JobRequestId.Value))
            .ToListAsync();
        var moaForms = await context.MOAForms
            .Where(f => f.JobRequestId != null && ids.Contains(f.JobRequestId.Value))
            .ToListAsync();
        var serviceForms = await context.ServiceJobForms
            .Where(f => ids.Contains(f.JobRequestId))
            .ToListAsync();

        var jobEntities = user != null
            ? await context.JobRequests
                .Include(j => j.Units).ThenInclude(u => u.Assignees)
                .Where(j => ids.Contains(j.JobRequestId))
                .ToDictionaryAsync(j => j.JobRequestId)
            : new Dictionary<int, Models.JobRequest>();

        foreach (var job in list)
        {
            job.AwaitingIntakeApproval = TaskFormVisibilityHelper.AwaitingIntakeApproval(
                jobEntities.GetValueOrDefault(job.Id) ?? new Models.JobRequest
                {
                    JobRequestId = job.Id,
                    TaskType = job.TaskType,
                    InternalHandoffStatus = job.InternalHandoffStatus,
                });

            job.TaskPhase = ResolveTaskPhase(job);

            if (user == null || !jobEntities.TryGetValue(job.Id, out var entity))
            {
                ApplyRawFormLinks(job, moiForms, moaForms, serviceForms);
                continue;
            }

            var moi = moiForms.FirstOrDefault(f => f.JobRequestId == job.Id);
            var moa = moaForms.FirstOrDefault(f => f.JobRequestId == job.Id);
            var (kind, formId) = TaskFormVisibilityHelper.ResolveVisibleFormLink(
                user, entity, moi?.MOIFormId, moa?.MOAFormId);

            if (kind != null && formId.HasValue)
            {
                job.LinkedFormKind = kind;
                job.LinkedFormId = formId;
                continue;
            }

            if (string.Equals(job.TaskType, "MOA", StringComparison.OrdinalIgnoreCase))
            {
                if (moa != null && TaskFormVisibilityHelper.CanViewMoaForm(user, entity))
                {
                    job.LinkedFormKind = "MOA";
                    job.LinkedFormId = moa.MOAFormId;
                }
                continue;
            }

            if (job.TaskType is "MOI" or "MOI Approval")
            {
                if (moi != null && TaskFormVisibilityHelper.CanViewMoiForm(user, entity))
                {
                    job.LinkedFormKind = "MOI";
                    job.LinkedFormId = moi.MOIFormId;
                }
                continue;
            }

            var svc = serviceForms.FirstOrDefault(f => f.JobRequestId == job.Id);
            if (svc != null && AuthHelper.CanAccessJob(user, entity))
            {
                job.LinkedFormKind = "Service";
                job.LinkedFormId = svc.ServiceJobFormId;
            }
        }
    }

    private static void ApplyRawFormLinks(
        JobRequestResponse job,
        List<Models.MOIForm> moiForms,
        List<Models.MOAForm> moaForms,
        List<Models.ServiceJobForm> serviceForms)
    {
        if (string.Equals(job.TaskType, "MOA", StringComparison.OrdinalIgnoreCase))
        {
            var moa = moaForms.FirstOrDefault(f => f.JobRequestId == job.Id);
            if (moa != null) { job.LinkedFormKind = "MOA"; job.LinkedFormId = moa.MOAFormId; }
            return;
        }

        if (job.TaskType is "MOI" or "MOI Approval")
        {
            var moi = moiForms.FirstOrDefault(f => f.JobRequestId == job.Id);
            if (moi != null) { job.LinkedFormKind = "MOI"; job.LinkedFormId = moi.MOIFormId; }
            return;
        }

        var svc = serviceForms.FirstOrDefault(f => f.JobRequestId == job.Id);
        if (svc != null) { job.LinkedFormKind = "Service"; job.LinkedFormId = svc.ServiceJobFormId; }
    }

    private static string ResolveTaskPhase(JobRequestResponse job)
    {
        if (TaskFormVisibilityHelper.AwaitingIntakeApproval(
            new Models.JobRequest { TaskType = job.TaskType, InternalHandoffStatus = job.InternalHandoffStatus }))
            return "Awaiting admin intake";

        return job.TaskType switch
        {
            "MOI" => job.InternalHandoffStatus switch
            {
                JobHandoffStatuses.ClientSubmitted => "MOI submitted",
                JobHandoffStatuses.PendingPrep or JobHandoffStatuses.ResoInProgress => "Resolution prep",
                JobHandoffStatuses.AdminReview => "Admin MOI sign-off",
                JobHandoffStatuses.ReadyForMoa or JobHandoffStatuses.MoaCirculation => "MOI complete",
                JobHandoffStatuses.Completed => "Done",
                _ => "MOI in progress",
            },
            "MOI Approval" => "MOI approval",
            "MOA" => job.InternalHandoffStatus switch
            {
                JobHandoffStatuses.ReadyForMoa => "MOA preparation",
                JobHandoffStatuses.MoaCirculation => "MOA circulation",
                JobHandoffStatuses.Completed => "Done",
                _ => "MOA",
            },
            _ => job.Status,
        };
    }
}
