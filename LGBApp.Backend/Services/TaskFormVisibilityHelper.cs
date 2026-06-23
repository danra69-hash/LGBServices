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
        if (AuthHelper.IsExternalUser(user))
            return AuthHelper.CanAccessJob(user, job);

        if (InternalWorkVisibilityHelper.AppliesClientReleaseGate(job)
            && !InternalWorkVisibilityHelper.IsJobLineReleasedToInternal(job, []))
        {
            if (AwaitingIntakeApproval(job) && AuthHelper.CanApproveMoiIntake(user))
                return job.TaskType is "MOI" or "MOI Approval" or "Service";

            return false;
        }

        if (AuthHelper.IsAdmin(user))
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

            if (form != null
                && string.Equals(form.WorkflowState, MoiWorkflowStates.Approved, StringComparison.OrdinalIgnoreCase))
                return true;

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

    public static bool CanViewMoaForm(ClaimsPrincipal user, JobRequest job, MOAForm? moa = null, MOIForm? moi = null)
    {
        var handoff = ResolveMoaHandoffStatus(job, moa);

        if (AuthHelper.IsClientSignatory(user))
        {
            if (!IsMoaPhaseForClient(handoff))
                return false;
            return AuthHelper.CanAccessCustomer(user, job.CustomerId);
        }

        if (AuthHelper.IsClientAdmin(user))
        {
            if (!AuthHelper.CanAccessCustomer(user, job.CustomerId))
                return false;
            return IsMoaPhaseForClient(handoff);
        }

        if (AuthHelper.IsAdmin(user))
            return job.TaskType is "MOA" or "Service";

        if (!AuthHelper.IsInternalStaff(user))
            return false;

        if (job.TaskType is not ("MOA" or "Service"))
            return false;

        if (!IsEffectiveMoaPhase(handoff, moi))
            return false;

        if (AuthHelper.CanApproveMoa(user))
            return true;

        return AuthHelper.IsAssignedToJob(user, job, moa?.JobRequestUnitId);
    }

    public static bool CanEditMoaForm(
        ClaimsPrincipal user,
        JobRequest job,
        MOAForm? moa = null,
        MOIForm? moi = null,
        bool workflowStarted = false)
    {
        if (AuthHelper.IsClientAdmin(user) || AuthHelper.IsClientSignatory(user))
            return false;

        if (!CanViewMoaForm(user, job, moa, moi))
            return false;

        var handoff = ResolveMoaHandoffStatus(job, moa);
        if (handoff is JobHandoffStatuses.MoaSharonApproved
            or JobHandoffStatuses.ReadyForMoa
            or JobHandoffStatuses.MoaCirculation)
            return false;

        if (handoff == JobHandoffStatuses.AdminReview
            && !AuthHelper.CanApproveMoa(user)
            && !AuthHelper.IsAdmin(user))
            return false;

        if (workflowStarted)
            return AuthHelper.IsAdmin(user) || AuthHelper.CanApproveMoa(user);

        return AuthHelper.IsAdmin(user)
            || AuthHelper.CanApproveMoa(user)
            || AuthHelper.IsAssignedToJob(user, job, moa?.JobRequestUnitId);
    }

    public static bool CanEditMoiForm(ClaimsPrincipal user, JobRequest job, string workflowState)
    {
        if (AuthHelper.IsClientSignatory(user))
        {
            if (job.TaskType == "MOI Approval")
                return false;

            return AuthHelper.IsSignatoryForJob(user, job)
                && workflowState is MoiWorkflowStates.Draft or MoiWorkflowStates.MoiRejected;
        }

        if (AuthHelper.IsClientAdmin(user))
            return AuthHelper.CanAccessCustomer(user, job.CustomerId)
                && workflowState is MoiWorkflowStates.Draft or MoiWorkflowStates.MoiRejected;

        if (AuthHelper.IsAdmin(user))
            return workflowState != MoiWorkflowStates.Approved;

        if (!CanViewMoiForm(user, job))
            return false;

        return workflowState is MoiWorkflowStates.PendingPrep or MoiWorkflowStates.PendingRecommendation;
    }

    public static bool ShouldPreferMoaOverMoi(JobRequest job, MOIForm? moi, string? handoff = null)
    {
        if (!string.Equals(job.TaskType, "Service", StringComparison.OrdinalIgnoreCase))
            return false;

        var effectiveHandoff = handoff ?? job.InternalHandoffStatus ?? string.Empty;
        if (IsMoaPhase(effectiveHandoff))
            return true;

        return moi?.WorkflowState is MoiWorkflowStates.PendingPrep or MoiWorkflowStates.PendingRecommendation
            && effectiveHandoff != JobHandoffStatuses.ClientSubmitted
            && moi.WorkflowState != MoiWorkflowStates.PendingAdminIntake
            || (string.Equals(moi?.WorkflowState, MoiWorkflowStates.Approved, StringComparison.OrdinalIgnoreCase)
                && effectiveHandoff is JobHandoffStatuses.AwaitingSecAssignment or JobHandoffStatuses.PendingPrep);
    }

    public static (string? Kind, int? Id) ResolveVisibleFormLink(
        ClaimsPrincipal user,
        JobRequest job,
        MOIForm? moi,
        MOAForm? moa)
    {
        if (moa != null && CanViewMoaForm(user, job, moa))
            return ("MOA", moa.MOAFormId);

        if (ShouldPreferMoaOverMoi(job, moi))
            return (null, null);

        if (job.TaskType is "MOI" or "MOI Approval" or "Service"
            && moi != null
            && CanViewMoiForm(user, job, moi))
            return ("MOI", moi.MOIFormId);

        if (job.TaskType == "MOA" && moa != null && CanViewMoaForm(user, job, moa))
            return ("MOA", moa.MOAFormId);

        return (null, null);
    }

    private static bool IsMoaPhase(string handoff) =>
        handoff is JobHandoffStatuses.PendingPrep
            or JobHandoffStatuses.ResoInProgress
            or JobHandoffStatuses.AdminReview
            or JobHandoffStatuses.MoaSharonApproved
            or JobHandoffStatuses.ReadyForMoa
            or JobHandoffStatuses.MoaCirculation
            or JobHandoffStatuses.PendingExecute
            or JobHandoffStatuses.ExecutionSecComplete
            or JobHandoffStatuses.Completed;

    private static bool IsEffectiveMoaPhase(string handoff, MOIForm? moi) =>
        IsMoaPhase(handoff)
        || (moi?.WorkflowState is MoiWorkflowStates.PendingPrep or MoiWorkflowStates.PendingRecommendation
            && handoff != JobHandoffStatuses.ClientSubmitted
            && moi.WorkflowState != MoiWorkflowStates.PendingAdminIntake)
        || (string.Equals(moi?.WorkflowState, MoiWorkflowStates.Approved, StringComparison.OrdinalIgnoreCase)
            && handoff is JobHandoffStatuses.AwaitingSecAssignment or JobHandoffStatuses.PendingPrep);

    private static bool IsMoaPhaseForClient(string handoff) =>
        handoff is JobHandoffStatuses.ReadyForMoa
            or JobHandoffStatuses.MoaCirculation
            or JobHandoffStatuses.PendingExecute
            or JobHandoffStatuses.ExecutionSecComplete
            or JobHandoffStatuses.Completed;

    private static string ResolveMoaHandoffStatus(JobRequest job, MOAForm? moa)
    {
        var unit = JobHandoffResolver.ResolveUnit(job, null, moa);
        return JobHandoffResolver.ResolveEffectiveHandoff(job, unit, moa);
    }
}
