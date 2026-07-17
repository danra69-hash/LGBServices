using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LGBApp.Backend.Services;

/// <summary>
/// Evaluates due reminders (R3/R4/M3/M4). Default is log-only — set Reminders:SendEmail=true to deliver.
/// Elapsed time is wall-clock UTC (weekends count).
/// </summary>
public class ReminderEvaluationService
{
    public static readonly TimeSpan MoiHodInterval = TimeSpan.FromHours(24);
    public const int MoiHodMax = 2;
    public static readonly TimeSpan MoiRequesterAfter = TimeSpan.FromHours(48);
    public const int MoiRequesterMax = 1;
    public static readonly TimeSpan MoaApproverInterval = TimeSpan.FromHours(48);
    public const int MoaApproverMax = 3;
    public static readonly TimeSpan MoaCosecAfter = TimeSpan.FromHours(144);
    public const int MoaCosecMax = 1;

    private readonly AppDbContext _context;
    private readonly IAppClock _clock;
    private readonly WorkflowNotifier _notifier;
    private readonly IConfiguration _config;
    private readonly ILogger<ReminderEvaluationService> _logger;

    public ReminderEvaluationService(
        AppDbContext context,
        IAppClock clock,
        WorkflowNotifier notifier,
        IConfiguration config,
        ILogger<ReminderEvaluationService> logger)
    {
        _context = context;
        _clock = clock;
        _notifier = notifier;
        _config = config;
        _logger = logger;
    }

    public bool SendEmail =>
        string.Equals(_config["Reminders:SendEmail"], "true", StringComparison.OrdinalIgnoreCase);

    public async Task<int> ProcessDueAsync(CancellationToken ct = default)
    {
        var sent = 0;
        sent += await ProcessMoiRemindersAsync(ct);
        sent += await ProcessMoaRemindersAsync(ct);
        return sent;
    }

    private async Task<int> ProcessMoiRemindersAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var pending = await _context.MOIForms
            .Include(f => f.JobRequest)
            .Where(f => f.WorkflowState == MoiWorkflowStates.PendingClientMoiApproval)
            .ToListAsync(ct);

        var count = 0;
        foreach (var form in pending)
        {
            var anchor = form.ClientApprovalRequestedAt ?? form.UpdatedAt;
            var elapsed = now - anchor;
            var job = form.JobRequest;
            if (job == null && form.JobRequestId.HasValue)
                job = await _context.JobRequests.FindAsync([form.JobRequestId.Value], ct);
            if (job == null) continue;

            var customer = await WorkflowService.ResolveCustomerForMoiAsync(_context, form);
            if (customer == null) continue;

            // R3 — HOD / matrix approver every 24h, up to 2
            var hodDue = (int)(elapsed / MoiHodInterval);
            if (hodDue >= 1)
            {
                var targetSends = Math.Min(hodDue, MoiHodMax);
                if (await TrySendAsync(
                        ReminderKinds.MoiHodReminder,
                        ReminderKinds.TargetMoiForm,
                        form.MOIFormId,
                        targetSends,
                        MoiHodMax,
                        async () =>
                        {
                            if (SendEmail)
                                await _notifier.NotifyMoiPendingSignOffAsync(job, customer, form);
                            _logger.LogInformation(
                                "[Reminder] {Kind} moi={MoiId} company={Company} sendEmail={Send}",
                                ReminderKinds.MoiHodReminder, form.MOIFormId, form.Company, SendEmail);
                        },
                        ct))
                    count++;
            }

            // R4 — requester prompt once after 48h
            if (elapsed >= MoiRequesterAfter)
            {
                if (await TrySendAsync(
                        ReminderKinds.MoiRequesterPrompt,
                        ReminderKinds.TargetMoiForm,
                        form.MOIFormId,
                        1,
                        MoiRequesterMax,
                        async () =>
                        {
                            if (SendEmail)
                                await _notifier.NotifyMoiRequesterPromptAsync(job, customer, form);
                            _logger.LogInformation(
                                "[Reminder] {Kind} moi={MoiId} company={Company} sendEmail={Send}",
                                ReminderKinds.MoiRequesterPrompt, form.MOIFormId, form.Company, SendEmail);
                        },
                        ct))
                    count++;
            }
        }

        return count;
    }

    private async Task<int> ProcessMoaRemindersAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var activeSteps = await _context.WorkflowStepInstances
            .Include(s => s.WorkflowInstance)
            .ThenInclude(i => i!.MoaForm)
            .Where(s => s.Status == "Active"
                && s.WorkflowInstance != null
                && s.WorkflowInstance.Status == "Active"
                && s.WorkflowInstance.FormType == "MOA")
            .ToListAsync(ct);

        var count = 0;
        foreach (var step in activeSteps)
        {
            var anchor = step.ActivatedAt
                ?? step.WorkflowInstance?.CreatedAt
                ?? now;
            var elapsed = now - anchor;
            var moa = step.WorkflowInstance?.MoaForm;
            if (moa == null && step.WorkflowInstance?.MoaFormId is int moaId)
                moa = await _context.MOAForms.FindAsync([moaId], ct);
            if (moa == null) continue;

            JobRequest? job = null;
            if (moa.JobRequestId.HasValue)
                job = await _context.JobRequests.FindAsync([moa.JobRequestId.Value], ct);
            if (job == null) continue;

            var customer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, moa.Company);

            // M3 — current approver every 48h, up to 3
            var due = (int)(elapsed / MoaApproverInterval);
            if (due >= 1)
            {
                var targetSends = Math.Min(due, MoaApproverMax);
                if (await TrySendAsync(
                        ReminderKinds.MoaApproverReminder,
                        ReminderKinds.TargetWorkflowStep,
                        step.WorkflowStepInstanceId,
                        targetSends,
                        MoaApproverMax,
                        async () =>
                        {
                            if (SendEmail && customer != null)
                                await _notifier.NotifyMoaStepReminderAsync(job, customer, moa, step);
                            _logger.LogInformation(
                                "[Reminder] {Kind} step={StepId} assignee={Assignee} sendEmail={Send}",
                                ReminderKinds.MoaApproverReminder, step.WorkflowStepInstanceId, step.AssigneeName, SendEmail);
                        },
                        ct))
                    count++;
            }

            // M4 — all cosec once after 144h
            if (elapsed >= MoaCosecAfter)
            {
                if (await TrySendAsync(
                        ReminderKinds.MoaCosecPrompt,
                        ReminderKinds.TargetWorkflowStep,
                        step.WorkflowStepInstanceId,
                        1,
                        MoaCosecMax,
                        async () =>
                        {
                            if (SendEmail)
                                await _notifier.NotifyMoaCosecStalledAsync(job, moa, step);
                            _logger.LogInformation(
                                "[Reminder] {Kind} step={StepId} sendEmail={Send}",
                                ReminderKinds.MoaCosecPrompt, step.WorkflowStepInstanceId, SendEmail);
                        },
                        ct))
                    count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Claim + bump SentCount toward targetSends (idempotent caps). Returns true if a send was recorded.
    /// </summary>
    private async Task<bool> TrySendAsync(
        string kind,
        string targetType,
        int targetId,
        int targetSends,
        int maxSends,
        Func<Task> sendAction,
        CancellationToken ct)
    {
        targetSends = Math.Min(targetSends, maxSends);
        var now = _clock.UtcNow;

        var log = await _context.ReminderLogs
            .FirstOrDefaultAsync(r =>
                r.Kind == kind && r.TargetEntityType == targetType && r.TargetEntityId == targetId, ct);

        if (log == null)
        {
            log = new ReminderLog
            {
                Kind = kind,
                TargetEntityType = targetType,
                TargetEntityId = targetId,
                SentCount = 0,
                CreatedAt = now,
            };
            _context.ReminderLogs.Add(log);
            await _context.SaveChangesAsync(ct);
        }

        if (log.SentCount >= targetSends || log.SentCount >= maxSends)
            return false;

        // Multi-instance claim: skip if another worker holds the claim
        if (log.ClaimedUntil.HasValue && log.ClaimedUntil.Value > now)
            return false;

        log.ClaimedUntil = now.AddSeconds(45);
        await _context.SaveChangesAsync(ct);

        // Re-read after claim
        await _context.Entry(log).ReloadAsync(ct);
        if (log.SentCount >= targetSends || log.SentCount >= maxSends)
            return false;

        try
        {
            await sendAction();
            log.SentCount++;
            log.LastSentAt = now;
            log.ClaimedUntil = null;
            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reminder send failed {Kind} {Type}/{Id}", kind, targetType, targetId);
            log.ClaimedUntil = null;
            await _context.SaveChangesAsync(ct);
            return false;
        }
    }
}
