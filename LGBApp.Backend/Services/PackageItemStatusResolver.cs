using LGBApp.Backend.Models;

namespace LGBApp.Backend.Services;

public static class PackageItemStatusResolver
{
    public static PackageItemStatusResult ResolveForUnit(
        JobRequest job,
        JobRequestUnit unit,
        MOIForm? moiForm = null)
    {
        var handoff = job.TotalQty > 1
            ? (unit.InternalHandoffStatus ?? string.Empty)
            : (!string.IsNullOrWhiteSpace(unit.InternalHandoffStatus)
                ? unit.InternalHandoffStatus
                : job.InternalHandoffStatus ?? string.Empty);

        if (IsMoaPostApprovalHandoff(handoff))
        {
            return ResolveMoaHandoff(job, moiForm, handoff);
        }

        return ResolveMoiWithHandoff(job, moiForm, handoff, unit);
    }

    private static bool IsMoaPostApprovalHandoff(string handoff) =>
        handoff is JobHandoffStatuses.ReadyForMoa
            or JobHandoffStatuses.MoaCirculation
            or JobHandoffStatuses.PendingExecute
            or JobHandoffStatuses.ExecutionSecComplete
            or JobHandoffStatuses.Completed;

    private static PackageItemStatusResult ResolveMoaHandoff(
        JobRequest job,
        MOIForm? moi,
        string handoff)
    {
        handoff = NormalizeLegacyHandoff(handoff);
        if (string.Equals(job.Status, "Completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(handoff, JobHandoffStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.Completed);

        if (handoff is JobHandoffStatuses.PendingExecute or JobHandoffStatuses.ExecutionSecComplete)
            return Result(PackageItemStatuses.Execution);

        if (string.Equals(handoff, JobHandoffStatuses.MoaCirculation, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.MoaCirculation);

        if (string.Equals(handoff, JobHandoffStatuses.ReadyForMoa, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.ReadyForMoa);

        if (string.Equals(handoff, JobHandoffStatuses.MoaSharonApproved, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.MoiSignOff);

        if (string.Equals(handoff, JobHandoffStatuses.AdminReview, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.MoiSignOff);

        if (handoff is JobHandoffStatuses.PendingPrep or JobHandoffStatuses.ResoInProgress)
        {
            if (moi != null
                && string.Equals(moi.WorkflowState, MoiWorkflowStates.Approved, StringComparison.OrdinalIgnoreCase))
                return Result(PackageItemStatuses.MoiApproved);
            return Result(PackageItemStatuses.ResolutionPrep);
        }

        if (moi == null
            || !string.Equals(moi.WorkflowState, MoiWorkflowStates.Approved, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.MoiNotComplete);

        return Result(PackageItemStatuses.MoiApproved);
    }

    public static PackageItemStatusResult Resolve(
        JobRequest job,
        MOIForm? moiForm = null,
        MOIForm? pairedMoiForm = null)
    {
        if (string.Equals(job.Status, "Canceled", StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.Canceled);

        if (moiForm != null && job.TaskType is "MOI" or "Service")
        {
            if (job.TaskType == "Service" && IsMoaHandoff(job.InternalHandoffStatus))
                return ResolveMoa(job, moiForm);
            return ResolveMoi(job, moiForm);
        }

        return job.TaskType switch
        {
            "MOI" => ResolveMoi(job, moiForm),
            "MOI Approval" => ResolveMoiApproval(job, pairedMoiForm),
            "MOA" => ResolveMoa(job, pairedMoiForm),
            _ => ResolveService(job),
        };
    }

    public static PackageItemStatusResult ResolveFromResponse(
        Models.DTOs.JobRequestResponse job,
        MOIForm? moiForm = null,
        MOIForm? pairedMoiForm = null)
    {
        var entity = new JobRequest
        {
            JobRequestId = job.Id,
            TaskType = job.TaskType,
            Status = job.Status,
            InternalHandoffStatus = job.InternalHandoffStatus ?? string.Empty,
            AssignedUserId = job.AssignedUserId,
            Units = job.Units.Select(u => new JobRequestUnit
            {
                AssignedUserId = u.AssignedUserId,
                Assignees = u.Assignees?.Select(a => new JobRequestUnitAssignee { UserId = a.UserId }).ToList() ?? [],
            }).ToList(),
        };
        return Resolve(entity, moiForm, pairedMoiForm);
    }

    private static PackageItemStatusResult ResolveMoi(JobRequest job, MOIForm? form) =>
        ResolveMoiWithHandoff(job, form, job.InternalHandoffStatus);

    private static PackageItemStatusResult ResolveMoiWithHandoff(
        JobRequest job,
        MOIForm? form,
        string handoff,
        JobRequestUnit? unit = null)
    {
        handoff = NormalizeLegacyHandoff(handoff);
        if (job.TotalQty <= 1
            && string.Equals(job.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.Completed);

        if (unit != null
            && job.TotalQty > 1
            && (string.Equals(unit.InternalHandoffStatus, JobHandoffStatuses.Completed, StringComparison.OrdinalIgnoreCase)
                || string.Equals(unit.Status, "Completed", StringComparison.OrdinalIgnoreCase)))
            return Result(PackageItemStatuses.Completed);

        if (form != null)
        {
            var fromForm = ResolveMoiFromWorkflowState(form.WorkflowState, handoff, form);
            if (fromForm.HasValue)
                return fromForm.Value;
        }

        if (string.Equals(handoff, JobHandoffStatuses.ClientSubmitted, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.AwaitingIntake);

        if (string.Equals(handoff, JobHandoffStatuses.AwaitingSecAssignment, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.ResolutionPrep);

        if (handoff is JobHandoffStatuses.PendingPrep or JobHandoffStatuses.ResoInProgress)
            return Result(PackageItemStatuses.ResolutionPrep);

        if (string.Equals(handoff, JobHandoffStatuses.AdminReview, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.MoiSignOff);

        if (handoff is JobHandoffStatuses.ReadyForMoa or JobHandoffStatuses.MoaCirculation)
            return Result(PackageItemStatuses.MoiApproved);

        if (handoff is JobHandoffStatuses.PendingExecute or JobHandoffStatuses.ExecutionSecComplete)
            return Result(PackageItemStatuses.Execution);

        if (string.Equals(handoff, JobHandoffStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.Completed);

        if (form == null && string.IsNullOrWhiteSpace(handoff))
            return Result(PackageItemStatuses.MoiNotReceived);

        return Result(PackageItemStatuses.ResolutionPrep);
    }

    private static PackageItemStatusResult? ResolveMoiFromWorkflowState(
        string workflowState,
        string handoff,
        MOIForm? form = null)
    {
        if (workflowState == MoiWorkflowStates.MoiRejected
            || (workflowState == MoiWorkflowStates.Draft
                && form != null
                && FormRejectionService.ParseMoi(form).Count > 0))
            return Result(PackageItemStatuses.MoiRejected);

        return workflowState switch
        {
            MoiWorkflowStates.Approved when handoff == JobHandoffStatuses.AwaitingSecAssignment
                => Result(PackageItemStatuses.ResolutionPrep),
            MoiWorkflowStates.Approved => Result(PackageItemStatuses.MoiApproved),
            MoiWorkflowStates.PendingClientMoiApproval => Result(PackageItemStatuses.PendingSignOff),
            MoiWorkflowStates.PendingRecommendation => Result(PackageItemStatuses.PendingRecommendation),
            MoiWorkflowStates.PendingAdminIntake => Result(PackageItemStatuses.AwaitingIntake),
            MoiWorkflowStates.PendingPrep => Result(PackageItemStatuses.ResolutionPrep),
            MoiWorkflowStates.Draft when string.IsNullOrWhiteSpace(handoff) => Result(PackageItemStatuses.MoiNotReceived),
            MoiWorkflowStates.Draft when handoff == JobHandoffStatuses.ClientSubmitted => Result(PackageItemStatuses.AwaitingIntake),
            _ => null,
        };
    }

    private static PackageItemStatusResult ResolveMoiApproval(JobRequest job, MOIForm? pairedMoiForm)
    {
        if (string.Equals(job.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.Completed);

        if (pairedMoiForm == null)
            return Result(PackageItemStatuses.AwaitingMoi);

        if (string.Equals(pairedMoiForm.WorkflowState, MoiWorkflowStates.Approved, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.Approved);

        if (pairedMoiForm.WorkflowState is MoiWorkflowStates.PendingClientMoiApproval)
            return Result(PackageItemStatuses.PendingSignOff);

        if (pairedMoiForm.WorkflowState is MoiWorkflowStates.PendingRecommendation)
            return Result(PackageItemStatuses.AwaitingMoi);

        if (job.InternalHandoffStatus is JobHandoffStatuses.AdminReview or JobHandoffStatuses.ResoInProgress)
            return Result(PackageItemStatuses.PendingSignOff);

        return Result(PackageItemStatuses.AwaitingMoi);
    }

    private static PackageItemStatusResult ResolveMoa(JobRequest job, MOIForm? pairedMoiForm)
    {
        if (string.Equals(job.Status, "Completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(job.InternalHandoffStatus, JobHandoffStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.Completed);

        if (string.Equals(job.InternalHandoffStatus, JobHandoffStatuses.MoaCirculation, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.MoaCirculation);

        if (job.InternalHandoffStatus is JobHandoffStatuses.PendingExecute or JobHandoffStatuses.ExecutionSecComplete)
            return Result(PackageItemStatuses.Execution);

        if (string.Equals(job.InternalHandoffStatus, JobHandoffStatuses.ReadyForMoa, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.ReadyForMoa);

        if (string.Equals(job.InternalHandoffStatus, JobHandoffStatuses.MoaSharonApproved, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.MoiSignOff);

        if (pairedMoiForm == null
            || !string.Equals(pairedMoiForm.WorkflowState, MoiWorkflowStates.Approved, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.MoiNotComplete);

        if (job.InternalHandoffStatus is JobHandoffStatuses.PendingPrep or JobHandoffStatuses.ResoInProgress)
            return Result(PackageItemStatuses.MoiApproved);

        if (string.Equals(job.InternalHandoffStatus, JobHandoffStatuses.AdminReview, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.MoiSignOff);

        return Result(PackageItemStatuses.MoiNotComplete);
    }

    private static PackageItemStatusResult ResolveService(JobRequest job)
    {
        if (string.Equals(job.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.Completed);

        if (HasAssignee(job))
            return Result(PackageItemStatuses.InProgress);

        return Result(PackageItemStatuses.NotStarted);
    }

    private static bool HasAssignee(JobRequest job)
    {
        if (job.AssignedUserId.HasValue)
            return true;

        return job.Units.Any(u =>
            u.AssignedUserId.HasValue
            || u.Assignees.Count > 0);
    }

    private static bool IsMoaHandoff(string handoff) =>
        NormalizeLegacyHandoff(handoff) is JobHandoffStatuses.PendingPrep
            or JobHandoffStatuses.ResoInProgress
            or JobHandoffStatuses.AdminReview
            or JobHandoffStatuses.MoaSharonApproved
            or JobHandoffStatuses.ReadyForMoa
            or JobHandoffStatuses.MoaCirculation;

    private static string NormalizeLegacyHandoff(string handoff) => handoff;

    private static PackageItemStatusResult Result(string key) =>
        new(key, PackageItemStatuses.LabelFor(key));
}
