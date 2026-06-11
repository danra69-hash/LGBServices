using System.Security.Claims;
using LGBApp.Backend.Models;

namespace LGBApp.Backend.Services;

public static class TaskFormVisibilityHelper
{
    public static bool AwaitingIntakeApproval(JobRequest job, MOIForm? moiForm = null) =>
        job.TaskType is "MOI" or "MOI Approval" or "Service"
        && (string.Equals(job.InternalHandoffStatus, JobHandoffStatuses.ClientSubmitted, StringComparison.OrdinalIgnoreCase)
            || job.Units.Any(u => string.Equals(u.InternalHandoffStatus, JobHandoffStatuses.ClientSubmitted, StringComparison.OrdinalIgnoreCase))
            || string.Equals(moiForm?.WorkflowState, MoiWorkflowStates.PendingAdminIntake, StringComparison.OrdinalIgnoreCase));

    public static bool UnitAwaitingIntakeApproval(JobRequestUnit unit, MOIForm? moiForm = null) =>
        string.Equals(unit.InternalHandoffStatus, JobHandoffStatuses.ClientSubmitted, StringComparison.OrdinalIgnoreCase)
        || string.Equals(moiForm?.WorkflowState, MoiWorkflowStates.PendingAdminIntake, StringComparison.OrdinalIgnoreCase);

    /// <summary>EF-safe intake check (handoff flags only, no MOI form navigation).</summary>
    public static bool JobHandoffAwaitingIntake(JobRequest job) =>
        job.TaskType is "MOI" or "MOI Approval" or "Service"
        && (string.Equals(job.InternalHandoffStatus, JobHandoffStatuses.ClientSubmitted, StringComparison.OrdinalIgnoreCase)
            || job.Units.Any(u => string.Equals(u.InternalHandoffStatus, JobHandoffStatuses.ClientSubmitted, StringComparison.OrdinalIgnoreCase)));

    public static bool CanInternalUserSeeJob(ClaimsPrincipal user, JobRequest job)
    {
        if (AuthHelper.IsAdmin(user) || AuthHelper.IsExternalUser(user))
            return AuthHelper.CanAccessJob(user, job);

        if (AwaitingIntakeApproval(job) && AuthHelper.CanApproveMoiIntake(user))
            return job.TaskType is "MOI" or "MOI Approval" or "Service";

        if (!AwaitingIntakeApproval(job)
            && job.TaskType is "MOI" or "MOI Approval" or "Service"
            && AuthHelper.CanApproveMoi(user))
            return true;

        if (job.TaskType == "MOA" && AuthHelper.CanApproveMoa(user))
            return true;

        if (AwaitingIntakeApproval(job))
            return false;

        return AuthHelper.CanAccessJob(user, job);
    }

    public static bool CanViewMoiForm(ClaimsPrincipal user, JobRequest job, MOIForm? form = null)
    {
        if (job.TaskType is not ("MOI" or "MOI Approval" or "Service"))
            return false;

        if (AuthHelper.IsExternalUser(user))
        {
            if (!AuthHelper.CanAccessCustomer(user, job.CustomerId))
                return false;

            if (AuthHelper.IsClientAdmin(user))
                return true;

            if (MoiVisibilityHelper.IsClientOnlyPhase(form, job))
                return true;

            if (AuthHelper.IsSignatoryForJob(user, job))
                return true;

            if (form?.WorkflowState == MoiWorkflowStates.PendingClientMoiApproval)
                return true;

            return AuthHelper.CanAccessCustomer(user, job.CustomerId);
        }

        if (!MoiVisibilityHelper.HasClientReleasedToInternal(form, job))
            return false;

        if (AuthHelper.IsAdmin(user))
            return true;

        if (!AuthHelper.IsInternalStaff(user))
            return false;

        if (AwaitingIntakeApproval(job))
            return AuthHelper.CanApproveMoiIntake(user);

        if (AuthHelper.CanApproveMoi(user))
            return true;

        return AuthHelper.CanAccessJob(user, job);
    }

    public static bool CanViewMoaForm(ClaimsPrincipal user, JobRequest job)
    {
        if (AuthHelper.IsClientSignatory(user))
        {
            if (!IsMoaPhaseForClient(job.InternalHandoffStatus))
                return false;
            return AuthHelper.CanAccessCustomer(user, job.CustomerId);
        }

        if (AuthHelper.IsClientAdmin(user))
        {
            if (!AuthHelper.CanAccessCustomer(user, job.CustomerId))
                return false;
            return IsMoaPhaseForClient(job.InternalHandoffStatus);
        }

        if (AuthHelper.IsAdmin(user))
            return job.TaskType is "MOA" or "Service";

        if (!AuthHelper.IsInternalStaff(user))
            return false;

        if (job.TaskType is not ("MOA" or "Service"))
            return false;

        if (!IsMoaPhase(job.InternalHandoffStatus))
            return false;

        if (AuthHelper.CanApproveMoa(user))
            return true;

        return AuthHelper.CanAccessJob(user, job);
    }

    public static bool CanEditMoaForm(ClaimsPrincipal user, JobRequest job, bool workflowStarted)
    {
        if (AuthHelper.IsClientAdmin(user) || AuthHelper.IsClientSignatory(user))
            return false;

        if (!CanViewMoaForm(user, job))
            return false;

        if (workflowStarted)
            return AuthHelper.IsAdmin(user) || AuthHelper.CanApproveMoa(user);

        return AuthHelper.IsAdmin(user)
            || AuthHelper.CanApproveMoa(user)
            || AuthHelper.CanAccessJob(user, job);
    }

    public static bool CanEditMoiForm(ClaimsPrincipal user, JobRequest job, string workflowState)
    {
        if (AuthHelper.IsClientSignatory(user))
        {
            if (job.TaskType == "MOI Approval")
                return false;

            return AuthHelper.IsSignatoryForJob(user, job)
                && workflowState is MoiWorkflowStates.Draft;
        }

        if (AuthHelper.IsClientAdmin(user))
            return AuthHelper.CanAccessCustomer(user, job.CustomerId)
                && workflowState is MoiWorkflowStates.Draft;

        if (AuthHelper.IsAdmin(user))
            return workflowState != MoiWorkflowStates.Approved;

        if (!CanViewMoiForm(user, job))
            return false;

        return workflowState is MoiWorkflowStates.PendingPrep or MoiWorkflowStates.PendingRecommendation;
    }

    public static (string? Kind, int? Id) ResolveVisibleFormLink(
        ClaimsPrincipal user,
        JobRequest job,
        MOIForm? moi,
        MOAForm? moa)
    {
        if (moa != null && CanViewMoaForm(user, job))
            return ("MOA", moa.MOAFormId);

        if (job.TaskType is "MOI" or "MOI Approval" or "Service"
            && moi != null
            && CanViewMoiForm(user, job, moi))
            return ("MOI", moi.MOIFormId);

        if (job.TaskType == "MOA" && moa != null && CanViewMoaForm(user, job))
            return ("MOA", moa.MOAFormId);

        return (null, null);
    }

    private static bool IsMoaPhase(string handoff) =>
        handoff is JobHandoffStatuses.MoaSharonApproved
            or JobHandoffStatuses.ReadyForMoa
            or JobHandoffStatuses.MoaCirculation
            or JobHandoffStatuses.PendingExecute
            or JobHandoffStatuses.Completed
            or JobHandoffStatuses.AdminReview;

    private static bool IsMoaPhaseForClient(string handoff) =>
        handoff is JobHandoffStatuses.ReadyForMoa
            or JobHandoffStatuses.MoaCirculation
            or JobHandoffStatuses.PendingExecute
            or JobHandoffStatuses.Completed;
}
