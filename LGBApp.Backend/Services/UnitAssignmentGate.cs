using LGBApp.Backend.Models;

namespace LGBApp.Backend.Services;

public static class UnitAssignmentGate
{
    public static bool CanAssignUnit(
        JobRequest job,
        JobRequestUnit unit,
        IReadOnlyList<JobRequestUnit> units,
        MOIForm? moi,
        MOIForm? previousMoi)
    {
        if (!CanAssignSecretarialTeamForUnit(job, unit, moi))
            return false;

        if (unit.UnitNumber <= 1)
            return moi != null || !IsMoiNotReceived(job, unit, moi);

        var previous = units.FirstOrDefault(u => u.UnitNumber == unit.UnitNumber - 1);
        if (previous == null)
            return false;

        return IsUnitMoiComplete(job, previous, previousMoi);
    }

    public static bool CanAssignSecretarialTeamForUnit(JobRequest job, JobRequestUnit unit, MOIForm? moi)
    {
        var handoff = JobHandoffResolver.ResolveEffectiveHandoff(job, unit);

        if (string.Equals(handoff, JobHandoffStatuses.ClientSubmitted, StringComparison.OrdinalIgnoreCase))
            return false;

        if (handoff is JobHandoffStatuses.AwaitingSecAssignment)
            return true;

        if (handoff is JobHandoffStatuses.ReadyForMoa
            or JobHandoffStatuses.MoaCirculation
            or JobHandoffStatuses.Completed
            or JobHandoffStatuses.PendingExecute
            or JobHandoffStatuses.ExecutionSecComplete)
            return false;

        var display = PackageItemStatusResolver.ResolveForUnit(job, unit, moi);
        return display.Key is PackageItemStatuses.ResolutionPrep
            or PackageItemStatuses.PendingRecommendation
            or PackageItemStatuses.MoiSignOff
            or PackageItemStatuses.MoiApproved
            or PackageItemStatuses.ReadyForMoa
            or PackageItemStatuses.MoaCirculation
            or PackageItemStatuses.Execution
            or PackageItemStatuses.ExecutionPendingApproval
            or PackageItemStatuses.PendingExecute
            || string.Equals(handoff, JobHandoffStatuses.PendingPrep, StringComparison.OrdinalIgnoreCase)
            || string.Equals(handoff, JobHandoffStatuses.ResoInProgress, StringComparison.OrdinalIgnoreCase)
            || string.Equals(handoff, JobHandoffStatuses.AdminReview, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMoiNotReceived(JobRequest job, JobRequestUnit unit, MOIForm? moi) =>
        string.Equals(
            PackageItemStatusResolver.ResolveForUnit(job, unit, moi).Key,
            PackageItemStatuses.MoiNotReceived,
            StringComparison.OrdinalIgnoreCase);

    private static bool IsUnitMoiComplete(JobRequest job, JobRequestUnit unit, MOIForm? moi)
    {
        if (string.Equals(moi?.WorkflowState, MoiWorkflowStates.Approved, StringComparison.OrdinalIgnoreCase))
            return true;

        var handoff = unit.InternalHandoffStatus ?? string.Empty;
        if (handoff is JobHandoffStatuses.PendingPrep
            or JobHandoffStatuses.ResoInProgress
            or JobHandoffStatuses.AdminReview
            or JobHandoffStatuses.ReadyForMoa
            or JobHandoffStatuses.MoaCirculation
            or JobHandoffStatuses.MoaSharonApproved
            or JobHandoffStatuses.PendingExecute
            or JobHandoffStatuses.ExecutionSecComplete
            or JobHandoffStatuses.Completed)
            return true;

        var display = PackageItemStatusResolver.ResolveForUnit(job, unit, moi);
        return display.Key is PackageItemStatuses.MoiApproved
            or PackageItemStatuses.ResolutionPrep
            or PackageItemStatuses.PendingRecommendation
            or PackageItemStatuses.ReadyForMoa
            or PackageItemStatuses.MoaCirculation
            or PackageItemStatuses.Execution
            or PackageItemStatuses.ExecutionPendingApproval
            or PackageItemStatuses.PendingExecute
            or PackageItemStatuses.Completed;
    }
}
