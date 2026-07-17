using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public static class JobHandoffStatuses
{
    public const string ClientSubmitted = "ClientSubmitted";
    public const string PendingPrep = "PendingPrep";
    public const string ResoInProgress = "ResoInProgress";
    public const string AdminReview = "AdminReview";
    public const string MoaSharonApproved = "MoaSharonApproved";
    public const string ReadyForMoa = "ReadyForMoa";
    public const string MoaCirculation = "MoaCirculation";
    /// <summary>MOI signed off by head sec — awaiting secretarial team assignment.</summary>
    public const string AwaitingSecAssignment = "AwaitingSecAssignment";
    public const string PendingExecute = "PendingExecute";
    public const string ExecutionSecComplete = "ExecutionSecComplete";
    public const string Completed = "Completed";
    /// <summary>D1: client skipped MOI/MOA — Sharon should read AdminBypassNote and action the work.</summary>
    public const string AdminBypass = "AdminBypass";

    public static readonly string[] Ordered =
    [
        ClientSubmitted, AwaitingSecAssignment, PendingPrep, ResoInProgress, AdminReview,
        MoaSharonApproved, ReadyForMoa, MoaCirculation, Completed,
    ];
}

public static class MoiWorkflowStates
{
    public const string Draft = "Draft";
    public const string PendingClientMoiApproval = "PendingClientMoiApproval";
    public const string PendingAdminIntake = "PendingAdminIntake";
    public const string PendingPrep = "PendingPrep";
    public const string PendingRecommendation = "PendingRecommendation";
    public const string Approved = "Approved";
    public const string MoiRejected = "MoiRejected";
}

public static class JobHandoffService
{
    public static void SetHandoff(JobRequest job, string status) =>
        job.InternalHandoffStatus = status;

    public static async Task OnClientMoiIssuedAsync(
        AppDbContext context,
        JobRequest job,
        MOIForm moiForm,
        JobRequestUnit? unit = null)
    {
        moiForm.WorkflowState = MoiWorkflowStates.Draft;
        if (job.TotalQty <= 1 && string.IsNullOrWhiteSpace(job.InternalHandoffStatus))
            SetHandoff(job, string.Empty);
        if (unit != null && job.TotalQty > 1)
            unit.InternalHandoffStatus = string.Empty;
        if (job.Status == "Pending")
            job.Status = "In Progress";
        moiForm.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public static async Task OnMoiSubmittedForApprovalAsync(
        AppDbContext context,
        JobRequest job,
        MOIForm form,
        Customer customer,
        WorkflowNotifier? notifier = null,
        User? submitter = null)
    {
        await ClientApprovalService.TryBindMatrixApproverAsync(
            context,
            form,
            submitter,
            requestedByName: ClientApprovalService.ReadRequestedByName(form));

        if (string.IsNullOrWhiteSpace(form.RequiredApproverEmail)
            && string.IsNullOrWhiteSpace(form.RequiredApproverName))
        {
            Console.WriteLine(
                $"[MOI matrix] No Approval Matrix row for submitter={submitter?.Email ?? "(null)"} " +
                $"company={form.Company} moiFormId={form.MOIFormId} — falling back to company MOI approvers.");
        }

        var records = ClientApprovalService.ParseMoi(form);
        if (ClientApprovalService.MoiClientPhaseComplete(customer, form, records))
        {
            await OnClientMoiPhaseCompleteAsync(context, job, form);
            return;
        }

        var required = ClientApprovalService.GetRequiredMoiApproverNames(form, customer);
        if (required.Count == 0 && string.IsNullOrWhiteSpace(form.RequiredApproverName))
        {
            // No matrix row and no company approvers — advance (Admin will see unbound forms)
            await OnClientMoiPhaseCompleteAsync(context, job, form);
            return;
        }

        form.WorkflowState = MoiWorkflowStates.PendingClientMoiApproval;
        form.UpdatedAt = DateTime.UtcNow;
        job.Status = "In Progress";

        await context.SaveChangesAsync();
        if (notifier != null)
            await notifier.NotifyMoiPendingSignOffAsync(job, customer, form);
    }

    public static async Task OnClientMoiApprovalRecordedAsync(
        AppDbContext context,
        JobRequest job,
        MOIForm form,
        Customer customer,
        WorkflowNotifier? notifier = null)
    {
        var records = ClientApprovalService.ParseMoi(form);
        if (!ClientApprovalService.MoiClientPhaseComplete(customer, form, records))
        {
            form.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            if (notifier != null)
                await notifier.NotifyRemainingMoiSignersAsync(job, customer, form);
            return;
        }

        await OnClientMoiPhaseCompleteAsync(context, job, form);
    }

    private static async Task OnClientMoiPhaseCompleteAsync(AppDbContext context, JobRequest job, MOIForm form)
    {
        form.WorkflowState = MoiWorkflowStates.PendingAdminIntake;
        form.UpdatedAt = DateTime.UtcNow;

        JobRequestUnit? unit = null;
        if (form.JobRequestUnitId.HasValue)
            unit = await context.JobRequestUnits.FindAsync(form.JobRequestUnitId.Value);

        if (unit != null && job.TotalQty > 1)
        {
            unit.InternalHandoffStatus = JobHandoffStatuses.ClientSubmitted;
            job.Status = "In Progress";
        }
        else
        {
            SetHandoff(job, JobHandoffStatuses.ClientSubmitted);
            job.Status = "Pending";
        }

        var docs = context.JobItemDocuments.Where(d => d.JobRequestId == job.JobRequestId);
        if (form.JobRequestUnitId.HasValue)
        {
            docs = docs.Where(d =>
                d.JobRequestUnitId == form.JobRequestUnitId
                || d.JobRequestUnitId == null);
        }

        foreach (var doc in await docs.ToListAsync())
            doc.VisibleToInternal = true;

        await context.SaveChangesAsync();
        await WorkflowNotificationService.NotifyMoiSubmittedAsync(context, job);

        if (unit == null && form.JobRequestUnitId.HasValue)
            unit = await context.JobRequestUnits.FindAsync(form.JobRequestUnitId.Value);
        if (unit == null)
        {
            await context.Entry(job).Collection(j => j.Units).LoadAsync();
            unit = job.Units.OrderBy(u => u.UnitNumber).FirstOrDefault();
        }

        if (unit?.ScheduledDate.HasValue == true)
            await JobRequestUnitService.SyncUnitToTrackerAsync(context, unit, job);
    }

    public static async Task OnMoiIntakeApprovedAsync(AppDbContext context, JobRequest job, int? unitNumber = null)
    {
        MOIForm? moiForm;
        if (unitNumber.HasValue && job.TotalQty > 1)
        {
            var unit = job.Units.FirstOrDefault(u => u.UnitNumber == unitNumber.Value)
                ?? throw new InvalidOperationException("Session not found for MOI sign-off.");
            moiForm = await context.MOIForms
                .Where(f => f.JobRequestId == job.JobRequestId && f.JobRequestUnitId == unit.JobRequestUnitId)
                .FirstOrDefaultAsync();
        }
        else
        {
            moiForm = await context.MOIForms
                .Where(f => f.JobRequestId == job.JobRequestId)
                .OrderByDescending(f => f.UpdatedAt)
                .FirstOrDefaultAsync();
        }

        if (moiForm == null)
            throw new InvalidOperationException("No MOI form found for sign-off.");

        await OnMoiSharonSignOffAsync(context, job, moiForm, unitNumber);
    }

    public static async Task OnJobAcceptedAsync(AppDbContext context, JobRequest job)
    {
        if (job.TaskType is "MOI" or "MOI Approval")
        {
            if (job.InternalHandoffStatus is JobHandoffStatuses.ClientSubmitted or "")
                SetHandoff(job, JobHandoffStatuses.PendingPrep);
            if (job.Status == "Pending")
                job.Status = "In Progress";
        }
        await context.SaveChangesAsync();
    }

    public static async Task OnMoiRecommendedAsync(AppDbContext context, MOIForm form)
    {
        if (!form.JobRequestId.HasValue) return;

        var job = await context.JobRequests
            .Include(j => j.Units)
            .FirstOrDefaultAsync(j => j.JobRequestId == form.JobRequestId.Value);
        if (job == null) return;

        form.WorkflowState = MoiWorkflowStates.PendingRecommendation;
        form.UpdatedAt = DateTime.UtcNow;
        job.Status = "In Progress";

        if (form.JobRequestUnitId.HasValue && job.TotalQty > 1)
        {
            var unit = job.Units.FirstOrDefault(u => u.JobRequestUnitId == form.JobRequestUnitId.Value);
            if (unit != null
                && (string.IsNullOrWhiteSpace(unit.InternalHandoffStatus)
                    || unit.InternalHandoffStatus is JobHandoffStatuses.PendingPrep))
                unit.InternalHandoffStatus = JobHandoffStatuses.ResoInProgress;
        }
        else if (string.IsNullOrWhiteSpace(job.InternalHandoffStatus)
            || job.InternalHandoffStatus is JobHandoffStatuses.PendingPrep)
            SetHandoff(job, JobHandoffStatuses.ResoInProgress);

        await context.SaveChangesAsync();
    }

    /// <summary>Head secretary MOI sign-off — enter resolution prep and provision MOA immediately.</summary>
    public static async Task OnMoiSharonSignOffAsync(
        AppDbContext context,
        JobRequest job,
        MOIForm form,
        int? unitNumber = null)
    {
        form.WorkflowState = MoiWorkflowStates.PendingPrep;
        form.UpdatedAt = DateTime.UtcNow;
        job.Status = "In Progress";

        if (unitNumber.HasValue && job.TotalQty > 1)
        {
            var unit = job.Units.FirstOrDefault(u => u.UnitNumber == unitNumber.Value)
                ?? throw new InvalidOperationException("Session not found for MOI sign-off.");
            unit.InternalHandoffStatus = JobHandoffStatuses.PendingPrep;

            if (!job.Units.Any(u => string.Equals(u.InternalHandoffStatus, JobHandoffStatuses.ClientSubmitted, StringComparison.OrdinalIgnoreCase)))
                SetHandoff(job, JobHandoffStatuses.PendingPrep);
        }
        else
        {
            SetHandoff(job, JobHandoffStatuses.PendingPrep);
            foreach (var unit in job.Units)
            {
                if (string.Equals(unit.InternalHandoffStatus, JobHandoffStatuses.ClientSubmitted, StringComparison.OrdinalIgnoreCase))
                    unit.InternalHandoffStatus = JobHandoffStatuses.PendingPrep;
            }
        }

        await context.SaveChangesAsync();
        await JobFormProvisioner.EnsureMoaFormForMoiAsync(context, job, form);
        await WorkflowNotificationService.NotifyIntakeApprovedAsync(context, job);
    }

    public static async Task OnMoiApprovedAsync(AppDbContext context, MOIForm form)
    {
        if (!form.JobRequestId.HasValue) return;
        var job = await context.JobRequests
            .Include(j => j.Units)
            .FirstOrDefaultAsync(j => j.JobRequestId == form.JobRequestId.Value);
        if (job == null) return;

        int? unitNumber = null;
        if (form.JobRequestUnitId.HasValue && job.TotalQty > 1)
        {
            unitNumber = job.Units
                .FirstOrDefault(u => u.JobRequestUnitId == form.JobRequestUnitId.Value)
                ?.UnitNumber;
        }

        await OnMoiSharonSignOffAsync(context, job, form, unitNumber);
    }

    public static async Task OnSecretarialTeamAssignedAsync(AppDbContext context, JobRequest job)
    {
        job.Status = "In Progress";

        if (job.TotalQty > 1)
        {
            foreach (var unit in job.Units)
            {
                if (string.Equals(unit.InternalHandoffStatus, JobHandoffStatuses.AwaitingSecAssignment, StringComparison.OrdinalIgnoreCase))
                    unit.InternalHandoffStatus = JobHandoffStatuses.PendingPrep;
            }

            if (!job.Units.Any(u => string.Equals(u.InternalHandoffStatus, JobHandoffStatuses.AwaitingSecAssignment, StringComparison.OrdinalIgnoreCase)))
                SetHandoff(job, JobHandoffStatuses.PendingPrep);
        }
        else
        {
            SetHandoff(job, JobHandoffStatuses.PendingPrep);
            foreach (var unit in job.Units)
                unit.InternalHandoffStatus = JobHandoffStatuses.PendingPrep;
        }

        var moiForms = await context.MOIForms
            .Where(f => f.JobRequestId == job.JobRequestId)
            .ToListAsync();
        foreach (var moiForm in moiForms)
        {
            if (string.Equals(moiForm.WorkflowState, MoiWorkflowStates.Approved, StringComparison.OrdinalIgnoreCase))
            {
                moiForm.WorkflowState = MoiWorkflowStates.PendingPrep;
                moiForm.UpdatedAt = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// When secretarial staff are tagged on a unit, start MOA prep for that session immediately.
    /// </summary>
    public static async Task OnSecretarialStaffAssignedToUnitAsync(
        AppDbContext context,
        JobRequest job,
        JobRequestUnit unit,
        MOIForm? moi)
    {
        if (moi == null)
            return;

        var moiApproved = string.Equals(moi.WorkflowState, MoiWorkflowStates.Approved, StringComparison.OrdinalIgnoreCase);
        var moiInPrep = moi.WorkflowState is MoiWorkflowStates.PendingPrep or MoiWorkflowStates.PendingRecommendation;
        if (!moiApproved && !moiInPrep)
            return;

        if (moiApproved)
        {
            moi.WorkflowState = MoiWorkflowStates.PendingPrep;
            moi.UpdatedAt = DateTime.UtcNow;
        }

        var unitHandoff = unit.InternalHandoffStatus ?? string.Empty;
        if (string.Equals(unitHandoff, JobHandoffStatuses.AwaitingSecAssignment, StringComparison.OrdinalIgnoreCase)
            || (string.IsNullOrWhiteSpace(unitHandoff)
                && string.Equals(job.InternalHandoffStatus, JobHandoffStatuses.AwaitingSecAssignment, StringComparison.OrdinalIgnoreCase)))
        {
            unit.InternalHandoffStatus = JobHandoffStatuses.PendingPrep;
        }

        if (job.TotalQty <= 1)
        {
            SetHandoff(job, JobHandoffStatuses.PendingPrep);
            foreach (var jobUnit in job.Units)
                jobUnit.InternalHandoffStatus = JobHandoffStatuses.PendingPrep;
        }
        else if (!job.Units.Any(u =>
            string.Equals(u.InternalHandoffStatus, JobHandoffStatuses.AwaitingSecAssignment, StringComparison.OrdinalIgnoreCase)
            || string.Equals(u.InternalHandoffStatus, JobHandoffStatuses.ClientSubmitted, StringComparison.OrdinalIgnoreCase)))
        {
            SetHandoff(job, JobHandoffStatuses.PendingPrep);
        }

        if (job.Status == "Pending")
            job.Status = "In Progress";

        await JobFormProvisioner.EnsureMoaFormForMoiAsync(context, job, moi);
        await context.SaveChangesAsync();
    }

    public static async Task OnMoaSubmittedForAdminReviewAsync(
        AppDbContext context,
        JobRequest job,
        MOAForm form,
        JobRequestUnit? unit = null)
    {
        JobHandoffResolver.MirrorJobHandoff(job, unit, JobHandoffStatuses.AdminReview);

        form.SubmittedForAdminReviewAt = DateTime.UtcNow;
        form.UpdatedAt = DateTime.UtcNow;
        job.Status = "In Progress";
        await context.SaveChangesAsync();
    }

    public static async Task OnSharonMoaApprovedAsync(
        AppDbContext context,
        JobRequest job,
        MOAForm? moaForm,
        JobRequestUnit? unit = null)
    {
        var handoff = JobHandoffResolver.ResolveEffectiveHandoff(job, unit, moaForm);
        if (handoff != JobHandoffStatuses.AdminReview)
            throw new InvalidOperationException("MOA must be in head secretary review before approval.");

        JobHandoffResolver.MirrorJobHandoff(job, unit, JobHandoffStatuses.MoaSharonApproved);
        if (moaForm != null)
        {
            moaForm.SharonApprovedAt = DateTime.UtcNow;
            moaForm.UpdatedAt = DateTime.UtcNow;
        }
        await context.SaveChangesAsync();
    }

    public static async Task AdvanceToReadyForMoaAsync(
        AppDbContext context,
        JobRequest job,
        JobRequestUnit? unit = null,
        MOAForm? moaForm = null,
        WorkflowNotifier? notifier = null)
    {
        var handoff = JobHandoffResolver.ResolveEffectiveHandoff(job, unit, moaForm);
        if (handoff != JobHandoffStatuses.MoaSharonApproved)
            throw new InvalidOperationException("MOA must be approved by Sharon before release to client.");

        JobHandoffResolver.MirrorJobHandoff(job, unit, JobHandoffStatuses.ReadyForMoa);
        job.Status = "In Progress";
        await context.SaveChangesAsync();

        var customer = job.CustomerId.HasValue
            ? await context.Customers.Include(c => c.AccountHolders)
                .FirstOrDefaultAsync(c => c.CustomerId == job.CustomerId.Value)
            : await WorkflowService.ResolveCustomerForCompanyAsync(context, job.Customer);
        if (customer != null)
        {
            MOIForm? pairedMoi = null;
            if (notifier != null)
            {
                pairedMoi = await context.MOIForms
                    .AsNoTracking()
                    .Where(f => f.JobRequestId == job.JobRequestId)
                    .OrderByDescending(f => f.MOIFormId)
                    .FirstOrDefaultAsync();
                await notifier.NotifyMoaReadyForClientAsync(job, customer, pairedMoi);
            }
            else
            {
                await WorkflowNotificationService.NotifyMoaReadyForClientAsync(context, job, customer);
            }
        }
    }

    public static async Task OnClientMoaApprovalRecordedAsync(
        AppDbContext context,
        JobRequest job,
        MOAForm form,
        Customer customer,
        JobRequestUnit? unit = null,
        WorkflowNotifier? notifier = null)
    {
        var required = ClientApprovalService.GetRequiredMoaApproverNames(customer, form);
        var records = ClientApprovalService.ParseMoa(form);
        if (!ClientApprovalService.MoaClientPhaseComplete(customer, form, records))
        {
            JobHandoffResolver.MirrorJobHandoff(job, unit, JobHandoffStatuses.MoaCirculation);
            form.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            if (notifier != null)
            {
                var pairedMoi = await context.MOIForms
                    .AsNoTracking()
                    .Where(f => f.JobRequestId == job.JobRequestId)
                    .OrderByDescending(f => f.MOIFormId)
                    .FirstOrDefaultAsync();
                await notifier.NotifyRemainingMoaSignersAsync(job, customer, form, pairedMoi);
            }
            return;
        }

        JobHandoffResolver.MirrorJobHandoff(job, unit, JobHandoffStatuses.PendingExecute);
        job.Status = "In Progress";
        if (unit != null && unit.Status != "Completed")
            unit.Status = "In Progress";
        form.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
    }

    public static async Task OnExecutionCompletedAsync(
        AppDbContext context,
        JobRequest job,
        JobRequestUnit? unit = null,
        MOAForm? form = null)
    {
        JobHandoffResolver.MirrorJobHandoff(job, unit, JobHandoffStatuses.Completed);
        if (unit != null)
        {
            unit.Status = "Completed";
            unit.CompletedAt = DateTime.UtcNow;
            await JobRequestUnitService.SyncUnitToTrackerAsync(context, unit, job);
        }

        if (form != null)
            form.UpdatedAt = DateTime.UtcNow;

        await JobRequestUnitService.RefreshJobAggregateAsync(context, job);

        if (job.Status == "Completed")
        {
            context.CompletedServices.Add(new CompletedService
            {
                JobRequestId = job.JobRequestId,
                Customer = job.Customer,
                Service = job.Service,
                UsedQty = job.UsedQty,
                TotalQty = job.TotalQty,
                DateRequested = job.DateRequested,
                DateCompleted = job.DateCompleted ?? DateTime.UtcNow,
                AccountHolder = job.AccountHolder,
                JobAssignedTo = job.JobAssignedTo,
                Status = "Completed",
                CreatedAt = DateTime.UtcNow,
            });

            await context.SaveChangesAsync();
            await WorkflowNotificationService.NotifyJobCompletedAsync(context, job);
            await PackageCompletionNotifier.TryNotifyIfPackageCompleteAsync(context, job, notifier: null);
            return;
        }

        await context.SaveChangesAsync();
    }

    public static async Task OnMoaWorkflowStartedAsync(AppDbContext context, MOAForm form)
    {
        if (!form.JobRequestId.HasValue) return;
        var job = await context.JobRequests
            .Include(j => j.Units)
            .FirstOrDefaultAsync(j => j.JobRequestId == form.JobRequestId.Value);
        if (job == null) return;

        var unit = JobHandoffResolver.ResolveUnit(job, null, form);
        var handoff = JobHandoffResolver.ResolveEffectiveHandoff(job, unit, form);
        if (JobHandoffResolver.IsMoaPrepHandoff(handoff) || string.IsNullOrWhiteSpace(handoff))
            JobHandoffResolver.MirrorJobHandoff(job, unit, JobHandoffStatuses.ResoInProgress);

        job.Status = "In Progress";
        await context.SaveChangesAsync();
    }

    public static async Task OnMoiClientRejectedAsync(
        AppDbContext context,
        JobRequest job,
        MOIForm form,
        User user,
        string reason)
    {
        form.WorkflowState = MoiWorkflowStates.MoiRejected;
        form.ClientApprovalsJson = "[]";
        form.UpdatedAt = DateTime.UtcNow;
        FormRejectionService.AddMoiRejection(form, new FormRejectionRecord
        {
            Stage = FormRejectionStages.MoiClientApproval,
            UserId = user.UserId,
            UserName = user.Name,
            Reason = reason.Trim(),
            RejectedAt = DateTime.UtcNow,
        });

        if (form.JobRequestUnitId.HasValue)
        {
            var unit = await context.JobRequestUnits.FindAsync(form.JobRequestUnitId.Value);
            if (unit != null)
                unit.InternalHandoffStatus = string.Empty;
        }
        else
            SetHandoff(job, string.Empty);

        job.Status = "In Progress";
        await context.SaveChangesAsync();
    }

    public static async Task OnMoiIntakeRejectedAsync(
        AppDbContext context,
        JobRequest job,
        MOIForm form,
        User user,
        string reason)
    {
        form.WorkflowState = MoiWorkflowStates.MoiRejected;
        // Review #4 §5: intake reject allows content edits before resubmit — clear
        // signatures so clients must re-sign the revised MOI (same as client reject).
        form.ClientApprovalsJson = "[]";
        form.UpdatedAt = DateTime.UtcNow;
        FormRejectionService.AddMoiRejection(form, new FormRejectionRecord
        {
            Stage = FormRejectionStages.MoiIntake,
            UserId = user.UserId,
            UserName = user.Name,
            Reason = reason.Trim(),
            RejectedAt = DateTime.UtcNow,
        });

        if (form.JobRequestUnitId.HasValue)
        {
            var unit = await context.JobRequestUnits.FindAsync(form.JobRequestUnitId.Value);
            if (unit != null)
                unit.InternalHandoffStatus = string.Empty;
        }
        else
            SetHandoff(job, string.Empty);

        job.Status = "In Progress";

        var docs = context.JobItemDocuments.Where(d => d.JobRequestId == job.JobRequestId);
        if (form.JobRequestUnitId.HasValue)
        {
            docs = docs.Where(d =>
                d.JobRequestUnitId == form.JobRequestUnitId
                || d.JobRequestUnitId == null);
        }

        foreach (var doc in await docs.ToListAsync())
            doc.VisibleToInternal = false;

        await context.SaveChangesAsync();
    }

    public static async Task OnMoaSharonRejectedAsync(
        AppDbContext context,
        JobRequest job,
        MOAForm form,
        User user,
        string reason,
        JobRequestUnit? unit = null)
    {
        form.SharonApprovedAt = null;
        form.SubmittedForAdminReviewAt = null;
        form.UpdatedAt = DateTime.UtcNow;
        FormRejectionService.AddMoaRejection(form, new FormRejectionRecord
        {
            Stage = FormRejectionStages.MoaSharonReview,
            UserId = user.UserId,
            UserName = user.Name,
            Reason = reason.Trim(),
            RejectedAt = DateTime.UtcNow,
        });

        JobHandoffResolver.MirrorJobHandoff(job, unit, JobHandoffStatuses.ResoInProgress);
        job.Status = "In Progress";
        await context.SaveChangesAsync();
    }

    public static async Task OnMoaClientRejectedAsync(
        AppDbContext context,
        JobRequest job,
        MOAForm form,
        User user,
        string reason,
        JobRequestUnit? unit = null)
    {
        form.ClientApprovalsJson = "[]";
        form.SharonApprovedAt = null;
        form.SubmittedForAdminReviewAt = null;
        form.UpdatedAt = DateTime.UtcNow;
        FormRejectionService.AddMoaRejection(form, new FormRejectionRecord
        {
            Stage = FormRejectionStages.MoaClientApproval,
            UserId = user.UserId,
            UserName = user.Name,
            Reason = reason.Trim(),
            RejectedAt = DateTime.UtcNow,
        });

        JobHandoffResolver.MirrorJobHandoff(job, unit, JobHandoffStatuses.ResoInProgress);
        job.Status = "In Progress";
        await context.SaveChangesAsync();
    }

    public static async Task OnMoaWorkflowCompletedAsync(AppDbContext context, int moaFormId)
    {
        var form = await context.MOAForms.FindAsync(moaFormId);
        if (form?.JobRequestId == null) return;

        var job = await context.JobRequests
            .Include(j => j.Units)
            .FirstOrDefaultAsync(j => j.JobRequestId == form.JobRequestId.Value);
        if (job == null) return;

        var unit = JobHandoffResolver.ResolveUnit(job, null, form);
        JobHandoffResolver.MirrorJobHandoff(job, unit, JobHandoffStatuses.PendingExecute);
        job.Status = "In Progress";
        if (unit != null && unit.Status != "Completed")
            unit.Status = "In Progress";
        form.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
    }
}
