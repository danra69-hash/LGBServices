using LGBApp.Backend.Models;

namespace LGBApp.Backend.Services;

public static class JobHandoffResolver
{
    public static string ResolveEffectiveHandoff(
        JobRequest job,
        JobRequestUnit? unit = null,
        MOAForm? moa = null)
    {
        if (job.TotalQty > 1)
        {
            if (unit != null && !string.IsNullOrWhiteSpace(unit.InternalHandoffStatus))
                return unit.InternalHandoffStatus;

            if (moa?.JobRequestUnitId is int moaUnitId)
            {
                var moaUnit = job.Units.FirstOrDefault(u => u.JobRequestUnitId == moaUnitId);
                if (moaUnit != null && !string.IsNullOrWhiteSpace(moaUnit.InternalHandoffStatus))
                    return moaUnit.InternalHandoffStatus;
            }

            if (!string.IsNullOrWhiteSpace(job.InternalHandoffStatus))
                return job.InternalHandoffStatus;

            return string.Empty;
        }

        if (unit != null && !string.IsNullOrWhiteSpace(unit.InternalHandoffStatus))
            return unit.InternalHandoffStatus;

        return job.InternalHandoffStatus ?? string.Empty;
    }

    public static JobRequestUnit? ResolveUnit(JobRequest job, int? unitNumber, MOAForm? moa = null)
    {
        if (unitNumber.HasValue)
            return job.Units.FirstOrDefault(u => u.UnitNumber == unitNumber.Value);

        if (moa?.JobRequestUnitId is int moaUnitId)
            return job.Units.FirstOrDefault(u => u.JobRequestUnitId == moaUnitId);

        if (job.TotalQty <= 1)
            return job.Units.OrderBy(u => u.UnitNumber).FirstOrDefault();

        return null;
    }

    public static void MirrorJobHandoff(JobRequest job, JobRequestUnit? unit, string handoff)
    {
        if (unit != null && job.TotalQty > 1)
        {
            unit.InternalHandoffStatus = handoff;
            SyncJobHandoffFromUnits(job);
            return;
        }

        JobHandoffService.SetHandoff(job, handoff);

        var unitToMirror = unit
            ?? (job.TotalQty <= 1
                ? job.Units.OrderBy(u => u.UnitNumber).FirstOrDefault()
                : null);
        if (unitToMirror != null)
            unitToMirror.InternalHandoffStatus = handoff;
    }

    /// <summary>
    /// Job-level handoff is a summary only for multi-session packages — never copy one unit's
    /// terminal state onto siblings that have not started yet.
    /// </summary>
    public static void SyncJobHandoffFromUnits(JobRequest job)
    {
        if (job.TotalQty <= 1)
            return;

        var units = job.Units.OrderBy(u => u.UnitNumber).ToList();
        if (units.Count == 0)
            return;

        if (units.All(u => string.Equals(
                u.InternalHandoffStatus,
                JobHandoffStatuses.Completed,
                StringComparison.OrdinalIgnoreCase)))
        {
            JobHandoffService.SetHandoff(job, JobHandoffStatuses.Completed);
            return;
        }

        var active = units.FirstOrDefault(u => !string.Equals(
            u.InternalHandoffStatus,
            JobHandoffStatuses.Completed,
            StringComparison.OrdinalIgnoreCase));

        if (active == null)
        {
            JobHandoffService.SetHandoff(job, JobHandoffStatuses.Completed);
            return;
        }

        JobHandoffService.SetHandoff(job, active.InternalHandoffStatus ?? string.Empty);
    }

    public static bool IsMoaPrepHandoff(string handoff) =>
        handoff is JobHandoffStatuses.PendingPrep
            or JobHandoffStatuses.ResoInProgress
            or JobHandoffStatuses.AwaitingSecAssignment;

    public static bool IsMoaDraftSubmittable(
        JobRequest job,
        JobRequestUnit? unit,
        MOAForm? moa,
        MOIForm? moi)
    {
        var handoff = ResolveEffectiveHandoff(job, unit, moa);
        if (IsMoaPrepHandoff(handoff))
            return true;

        return moi?.WorkflowState is MoiWorkflowStates.PendingPrep or MoiWorkflowStates.PendingRecommendation
            && handoff != JobHandoffStatuses.ClientSubmitted;
    }

    public static bool IsMoaClientSignoffHandoff(string handoff) =>
        handoff is JobHandoffStatuses.ReadyForMoa or JobHandoffStatuses.MoaCirculation;
}
