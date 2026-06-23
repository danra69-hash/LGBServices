using LGBApp.Backend.Models;

namespace LGBApp.Backend.Services;

/// <summary>
/// Internal staff (including admin) should not see package work until the client releases MOI.</summary>
public static class InternalWorkVisibilityHelper
{
    private static readonly string[] PostIntakeUnitHandoffs =
    [
        JobHandoffStatuses.AwaitingSecAssignment,
        JobHandoffStatuses.PendingPrep,
        JobHandoffStatuses.ResoInProgress,
        JobHandoffStatuses.AdminReview,
        JobHandoffStatuses.MoaSharonApproved,
        JobHandoffStatuses.ReadyForMoa,
        JobHandoffStatuses.MoaCirculation,
        JobHandoffStatuses.PendingExecute,
        JobHandoffStatuses.ExecutionSecComplete,
        JobHandoffStatuses.Completed,
    ];

    public static bool AppliesClientReleaseGate(JobRequest job) =>
        job.TaskType is "Service" or "MOI" or "MOI Approval";

    public static bool UnitMatchesMoi(JobRequestUnit unit, MOIForm moi, JobRequest job) =>
        moi.JobRequestUnitId == unit.JobRequestUnitId
        || (job.TotalQty <= 1 && moi.JobRequestUnitId == null);

    public static MOIForm? ResolveMoiForUnit(IEnumerable<MOIForm> mois, JobRequestUnit unit, JobRequest job)
    {
        var byUnit = mois.FirstOrDefault(f => f.JobRequestUnitId == unit.JobRequestUnitId);
        if (byUnit != null)
            return byUnit;

        return job.TotalQty <= 1
            ? mois.FirstOrDefault(f => f.JobRequestUnitId == null)
            : null;
    }

    public static bool IsUnitReleasedToInternal(JobRequest job, JobRequestUnit unit, MOIForm? moiForm)
    {
        if (!AppliesClientReleaseGate(job))
            return true;

        var unitHandoff = unit.InternalHandoffStatus ?? string.Empty;
        if (string.Equals(unitHandoff, JobHandoffStatuses.ClientSubmitted, StringComparison.OrdinalIgnoreCase))
            return true;

        if (moiForm != null && UnitMatchesMoi(unit, moiForm, job))
        {
            if (string.Equals(moiForm.WorkflowState, MoiWorkflowStates.PendingAdminIntake, StringComparison.OrdinalIgnoreCase))
                return true;

            if (moiForm.WorkflowState is MoiWorkflowStates.PendingPrep
                or MoiWorkflowStates.PendingRecommendation
                or MoiWorkflowStates.Approved)
                return true;
        }

        return PostIntakeUnitHandoffs.Any(h =>
            string.Equals(unitHandoff, h, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsJobLineReleasedToInternal(JobRequest job, IReadOnlyList<MOIForm> mois)
    {
        if (!AppliesClientReleaseGate(job))
            return true;

        if (MoiVisibilityHelper.HasClientReleasedToInternal(mois.FirstOrDefault(), job))
            return true;

        if (job.TotalQty <= 1)
        {
            var unit = job.Units.OrderBy(u => u.UnitNumber).FirstOrDefault();
            if (unit == null)
                return false;

            return IsUnitReleasedToInternal(job, unit, ResolveMoiForUnit(mois, unit, job));
        }

        return job.Units.Any(u => IsUnitReleasedToInternal(job, u, ResolveMoiForUnit(mois, u, job)));
    }

    public static List<JobRequest> FilterJobsForInternal(
        IEnumerable<JobRequest> jobs,
        IReadOnlyDictionary<int, List<MOIForm>> moisByJobId)
    {
        var filtered = new List<JobRequest>();
        foreach (var job in jobs)
        {
            var mois = moisByJobId.GetValueOrDefault(job.JobRequestId) ?? [];
            if (!IsJobLineReleasedToInternal(job, mois))
                continue;

            if (job.TotalQty > 1)
            {
                var releasedUnits = job.Units
                    .Where(u => IsUnitReleasedToInternal(job, u, ResolveMoiForUnit(mois, u, job)))
                    .ToList();
                if (releasedUnits.Count == 0)
                    continue;

                foreach (var unit in job.Units.ToList())
                {
                    if (releasedUnits.All(r => r.JobRequestUnitId != unit.JobRequestUnitId))
                        job.Units.Remove(unit);
                }
            }

            filtered.Add(job);
        }

        return filtered;
    }
}
