using LGBApp.Backend.Models;

namespace LGBApp.Backend.Services;

public static class MoiVisibilityHelper
{
    /// <summary>Client-only phase — internal staff must not see MOI content yet.</summary>
    public static bool IsClientOnlyPhase(MOIForm? form, JobRequest job)
    {
        if (form != null)
            return form.WorkflowState is MoiWorkflowStates.Draft or MoiWorkflowStates.PendingClientMoiApproval;

        return string.IsNullOrWhiteSpace(job.InternalHandoffStatus);
    }

    /// <summary>Client has released MOI to LGB (intake or later).</summary>
    public static bool HasClientReleasedToInternal(MOIForm? form, JobRequest job)
    {
        if (string.Equals(form?.WorkflowState, MoiWorkflowStates.PendingAdminIntake, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(job.InternalHandoffStatus, JobHandoffStatuses.ClientSubmitted, StringComparison.OrdinalIgnoreCase))
            return true;

        if (job.Units.Any(u => string.Equals(u.InternalHandoffStatus, JobHandoffStatuses.ClientSubmitted, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (form?.WorkflowState is MoiWorkflowStates.PendingPrep
            or MoiWorkflowStates.PendingRecommendation
            or MoiWorkflowStates.Approved)
            return true;

        return job.InternalHandoffStatus is JobHandoffStatuses.PendingPrep
            or JobHandoffStatuses.ResoInProgress
            or JobHandoffStatuses.AdminReview
            or JobHandoffStatuses.MoaSharonApproved
            or JobHandoffStatuses.ReadyForMoa
            or JobHandoffStatuses.MoaCirculation
            or JobHandoffStatuses.PendingExecute
            or JobHandoffStatuses.Completed;
    }
}
