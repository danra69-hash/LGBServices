using System.Security.Claims;
using LGBApp.Backend.Models;

namespace LGBApp.Backend.Services;

public static class TaskFormVisibilityHelper
{
    public static bool AwaitingIntakeApproval(JobRequest job) =>
        string.Equals(job.InternalHandoffStatus, JobHandoffStatuses.ClientSubmitted, StringComparison.OrdinalIgnoreCase)
        && job.TaskType is "MOI" or "MOI Approval";

    public static bool CanInternalUserSeeJob(ClaimsPrincipal user, JobRequest job)
    {
        if (AuthHelper.IsAdmin(user) || AuthHelper.IsExternalUser(user))
            return AuthHelper.CanAccessJob(user, job);

        if (AwaitingIntakeApproval(job))
            return false;

        return AuthHelper.CanAccessJob(user, job);
    }

    public static bool CanViewMoiForm(ClaimsPrincipal user, JobRequest job)
    {
        if (AuthHelper.IsExternalUser(user))
            return AuthHelper.CanAccessJob(user, job);

        if (AuthHelper.IsAdmin(user))
            return true;

        if (!AuthHelper.IsInternalStaff(user))
            return false;

        if (AwaitingIntakeApproval(job))
            return false;

        if (job.TaskType is not ("MOI" or "MOI Approval"))
            return false;

        return AuthHelper.CanAccessJob(user, job);
    }

    public static bool CanViewMoaForm(ClaimsPrincipal user, JobRequest job)
    {
        if (AuthHelper.IsExternalUser(user))
        {
            if (!AuthHelper.CanAccessJob(user, job))
                return false;
            return IsMoaPhase(job.InternalHandoffStatus);
        }

        if (AuthHelper.IsAdmin(user))
            return job.TaskType == "MOA";

        if (!AuthHelper.IsInternalStaff(user))
            return false;

        if (job.TaskType != "MOA")
            return false;

        if (!IsMoaPhase(job.InternalHandoffStatus))
            return false;

        return AuthHelper.CanAccessJob(user, job);
    }

    public static bool CanEditMoiForm(ClaimsPrincipal user, JobRequest job, string workflowState)
    {
        if (AuthHelper.IsExternalUser(user))
            return workflowState is "Draft" or "PendingPrep" or "PendingRecommendation";

        if (AuthHelper.IsAdmin(user))
            return workflowState != "Approved";

        if (!CanViewMoiForm(user, job))
            return false;

        return workflowState is "PendingPrep" or "PendingRecommendation";
    }

    public static (string? Kind, int? Id) ResolveVisibleFormLink(
        ClaimsPrincipal user,
        JobRequest job,
        int? moiFormId,
        int? moaFormId)
    {
        if (job.TaskType is "MOI" or "MOI Approval" && moiFormId.HasValue && CanViewMoiForm(user, job))
            return ("MOI", moiFormId);

        if (job.TaskType == "MOA" && moaFormId.HasValue && CanViewMoaForm(user, job))
            return ("MOA", moaFormId);

        return (null, null);
    }

    private static bool IsMoaPhase(string handoff) =>
        handoff is JobHandoffStatuses.ReadyForMoa
            or JobHandoffStatuses.MoaCirculation
            or JobHandoffStatuses.Completed
            or JobHandoffStatuses.AdminReview;
}
