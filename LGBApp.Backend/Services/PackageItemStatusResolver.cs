using LGBApp.Backend.Models;

namespace LGBApp.Backend.Services;

public static class PackageItemStatusResolver
{
    public static PackageItemStatusResult ResolveForUnit(
        JobRequest job,
        JobRequestUnit unit,
        MOIForm? moiForm = null)
    {
        var handoff = job.TotalQty > 1 ? unit.InternalHandoffStatus : job.InternalHandoffStatus;
        return ResolveMoiWithHandoff(job, moiForm, handoff);
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
        string handoff)
    {
        if (string.Equals(job.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.Completed);

        if (form != null)
        {
            var fromForm = ResolveMoiFromWorkflowState(form.WorkflowState, handoff, form);
            if (fromForm.HasValue)
                return fromForm.Value;
        }

        if (string.Equals(handoff, JobHandoffStatuses.ClientSubmitted, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.AwaitingIntake);

        if (handoff is JobHandoffStatuses.PendingPrep or JobHandoffStatuses.ResoInProgress)
            return Result(PackageItemStatuses.ResolutionPrep);

        if (string.Equals(handoff, JobHandoffStatuses.AdminReview, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.MoiSignOff);

        if (handoff is JobHandoffStatuses.ReadyForMoa or JobHandoffStatuses.MoaCirculation)
            return Result(PackageItemStatuses.MoiApproved);

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

        if (string.Equals(job.InternalHandoffStatus, JobHandoffStatuses.PendingExecute, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.PendingExecute);

        if (string.Equals(job.InternalHandoffStatus, JobHandoffStatuses.MoaCirculation, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.MoaCirculation);

        if (string.Equals(job.InternalHandoffStatus, JobHandoffStatuses.ReadyForMoa, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.ReadyForMoa);

        if (string.Equals(job.InternalHandoffStatus, JobHandoffStatuses.MoaSharonApproved, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.MoiSignOff);

        if (pairedMoiForm == null
            || !string.Equals(pairedMoiForm.WorkflowState, MoiWorkflowStates.Approved, StringComparison.OrdinalIgnoreCase))
            return Result(PackageItemStatuses.MoiNotComplete);

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
        handoff is JobHandoffStatuses.AdminReview
            or JobHandoffStatuses.MoaSharonApproved
            or JobHandoffStatuses.ReadyForMoa
            or JobHandoffStatuses.MoaCirculation
            or JobHandoffStatuses.PendingExecute;

    private static PackageItemStatusResult Result(string key) =>
        new(key, PackageItemStatuses.LabelFor(key));
}
