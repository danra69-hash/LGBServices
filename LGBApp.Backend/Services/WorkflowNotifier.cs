using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Services.Email;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

/// <summary>In-app notifications + email for MOI/MOA client signature actions.</summary>
public class WorkflowNotifier
{
    private readonly AppDbContext _context;
    private readonly IEmailSender _email;
    private readonly IConfiguration _config;
    private readonly ILogger<WorkflowNotifier> _logger;

    public WorkflowNotifier(
        AppDbContext context,
        IEmailSender email,
        IConfiguration config,
        ILogger<WorkflowNotifier> logger)
    {
        _context = context;
        _email = email;
        _config = config;
        _logger = logger;
    }

    public async Task NotifyMoiPendingSignOffAsync(JobRequest job, Customer customer, MOIForm form)
    {
        List<AccountHolder> holders;
        if (!string.IsNullOrWhiteSpace(form.RequiredApproverEmail)
            || !string.IsNullOrWhiteSpace(form.RequiredApproverName))
        {
            holders =
            [
                new AccountHolder
                {
                    Name = string.IsNullOrWhiteSpace(form.RequiredApproverName)
                        ? form.RequiredApproverEmail
                        : form.RequiredApproverName,
                    Email = form.RequiredApproverEmail,
                    NeedsMoiApproval = true,
                },
            ];
        }
        else
        {
            holders = customer.AccountHolders.Where(h => h.NeedsMoiApproval).ToList();
        }

        var userIds = await ResolveUserIdsAsync(holders, customer.CustomerId);
        var title = DisplayTitle(job, form);
        var message = $"{job.Customer} — {title} needs your MOI signature.";

        await WorkflowNotificationService.NotifyUsersAsync(
            _context,
            userIds,
            "moi_pending_signoff",
            "MOI awaiting your signature",
            message,
            job.JobRequestId,
            customer.CustomerId);

        await EmailUsersAsync(
            userIds,
            "Action required: sign MOI — " + job.Customer,
            BuildActionBody(job, title, "MOI", "Please sign in and complete your MOI approval."));
    }

    public async Task NotifyRemainingMoiSignersAsync(JobRequest job, Customer customer, MOIForm form)
    {
        var required = ClientApprovalService.GetRequiredMoiApprovalHolders(customer);
        var records = ClientApprovalService.ParseMoi(form);
        var pendingHolders = required.Where(h => !ClientApprovalService.HasSigned(records, h)).ToList();
        if (pendingHolders.Count == 0) return;

        var userIds = await ResolveUserIdsAsync(pendingHolders, customer.CustomerId);
        var title = DisplayTitle(job, form);
        var message = $"{job.Customer} — {title} is still awaiting your MOI signature.";

        await WorkflowNotificationService.NotifyUsersAsync(
            _context,
            userIds,
            "moi_pending_signoff",
            "MOI still awaiting your signature",
            message,
            job.JobRequestId,
            customer.CustomerId);

        await EmailUsersAsync(
            userIds,
            "Reminder: sign MOI — " + job.Customer,
            BuildActionBody(job, title, "MOI", "Your signature is still needed to release this MOI."));
    }

    public async Task NotifyMoaReadyForClientAsync(JobRequest job, Customer customer, MOIForm? pairedMoi = null)
    {
        var holders = customer.AccountHolders.Where(h => h.NeedsMoa).ToList();
        var userIds = await ResolveUserIdsAsync(holders, customer.CustomerId);
        if (userIds.Count == 0 && customer.CustomerId > 0)
        {
            userIds = await _context.Users
                .AsNoTracking()
                .Where(u => u.CustomerId == customer.CustomerId && u.Role != UserRoles.User)
                .Select(u => u.UserId)
                .ToListAsync();
        }

        var title = DisplayTitle(job, pairedMoi);
        var message = $"{job.Customer} — {title} MOA is ready for your signature.";

        await WorkflowNotificationService.NotifyUsersAsync(
            _context,
            userIds,
            "moa_ready",
            "MOA ready for sign-off",
            message,
            job.JobRequestId,
            customer.CustomerId);

        await EmailUsersAsync(
            userIds,
            "Action required: sign MOA — " + job.Customer,
            BuildActionBody(job, title, "MOA", "Please sign in and complete your MOA approval."));
    }

    public async Task NotifyRemainingMoaSignersAsync(JobRequest job, Customer customer, MOAForm form, MOIForm? pairedMoi = null)
    {
        var required = ClientApprovalService.GetRequiredMoaHolders(customer, form);
        var records = ClientApprovalService.ParseMoa(form);
        var pendingHolders = required.Where(h => !ClientApprovalService.HasSigned(records, h)).ToList();
        if (pendingHolders.Count == 0) return;

        var userIds = await ResolveUserIdsAsync(pendingHolders, customer.CustomerId);
        var title = DisplayTitle(job, pairedMoi);
        var message = $"{job.Customer} — {title} MOA is still awaiting your signature.";

        await WorkflowNotificationService.NotifyUsersAsync(
            _context,
            userIds,
            "moa_ready",
            "MOA still awaiting your signature",
            message,
            job.JobRequestId,
            customer.CustomerId);

        await EmailUsersAsync(
            userIds,
            "Reminder: sign MOA — " + job.Customer,
            BuildActionBody(job, title, "MOA", "Your signature is still needed to complete MOA circulation."));
    }

    private async Task<List<int>> ResolveUserIdsAsync(List<AccountHolder> holders, int customerId)
    {
        var ids = holders.Where(h => h.UserId.HasValue).Select(h => h.UserId!.Value).ToList();
        var emails = holders
            .Where(h => !h.UserId.HasValue && !string.IsNullOrWhiteSpace(h.Email))
            .Select(h => h.Email.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
        if (emails.Count > 0)
        {
            var byEmail = await _context.Users.AsNoTracking()
                .Where(u => emails.Contains(u.Email.ToLower()))
                .Select(u => u.UserId)
                .ToListAsync();
            ids.AddRange(byEmail);
        }

        return ids.Distinct().ToList();
    }

    public async Task NotifyMoiRequesterPromptAsync(JobRequest job, Customer customer, MOIForm form)
    {
        var title = DisplayTitle(job, form);
        var userIds = new List<int>();
        // Prefer submitter / holders who need MOI (requesters), else customer client users.
        var requesters = customer.AccountHolders.Where(h => h.NeedsMoi).ToList();
        userIds.AddRange(await ResolveUserIdsAsync(requesters, customer.CustomerId));
        if (userIds.Count == 0 && customer.CustomerId > 0)
        {
            userIds = await _context.Users.AsNoTracking()
                .Where(u => u.CustomerId == customer.CustomerId
                    && (u.Role == UserRoles.ClientAdmin || u.Role == UserRoles.ClientSignatory))
                .Select(u => u.UserId)
                .ToListAsync();
        }

        var message = $"{job.Customer} — {title} is still awaiting MOI approval after 48 hours.";
        await WorkflowNotificationService.NotifyUsersAsync(
            _context, userIds, "moi_requester_prompt", "MOI still awaiting approval", message,
            job.JobRequestId, customer.CustomerId);

        await EmailUsersAsync(
            userIds,
            "Update: MOI still awaiting approval — " + job.Customer,
            BuildActionBody(job, title, "MOI", "Your MOI has been waiting 48+ hours for approval. Follow up with the approver if needed."));
    }

    public async Task NotifyMoaStepReminderAsync(JobRequest job, Customer customer, MOAForm form, WorkflowStepInstance step)
    {
        var title = DisplayTitle(job, null);
        var message = $"{job.Customer} — MOA step “{step.DisplayName}” ({step.AssigneeName}) is still awaiting action.";
        var userIds = new List<int>();
        if (step.AssigneeUserId.HasValue)
            userIds.Add(step.AssigneeUserId.Value);
        else if (!string.IsNullOrWhiteSpace(step.AssigneeName))
        {
            var names = step.AssigneeName.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var holders = customer.AccountHolders
                .Where(h => names.Any(n => h.Name.Equals(n, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            userIds.AddRange(await ResolveUserIdsAsync(holders, customer.CustomerId));
            // Also match internal users by name
            var byName = await _context.Users.AsNoTracking()
                .Where(u => names.Contains(u.Name))
                .Select(u => u.UserId)
                .ToListAsync();
            userIds.AddRange(byName);
        }

        await WorkflowNotificationService.NotifyUsersAsync(
            _context, userIds.Distinct().ToList(), "moa_step_reminder", "MOA step awaiting action", message,
            job.JobRequestId, customer.CustomerId);

        await EmailUsersAsync(
            userIds,
            "Reminder: MOA approval — " + job.Customer,
            BuildActionBody(job, title, "MOA", $"Step “{step.DisplayName}” still needs your action."));
    }

    public async Task NotifyMoaCosecStalledAsync(JobRequest job, MOAForm form, WorkflowStepInstance step)
    {
        var title = string.IsNullOrWhiteSpace(job.Service) ? job.TaskType : job.Service;
        var cosecIds = await _context.Users.AsNoTracking()
            .Where(u => u.Role == UserRoles.Admin || u.Role == UserRoles.User)
            .Select(u => u.UserId)
            .ToListAsync();

        var message = $"{job.Customer} — MOA step “{step.DisplayName}” has been active over 144 hours.";
        await WorkflowNotificationService.NotifyUsersAsync(
            _context, cosecIds, "moa_cosec_stalled", "MOA stage stalled", message,
            job.JobRequestId, job.CustomerId);

        await EmailUsersAsync(
            cosecIds,
            "Alert: MOA stage stalled — " + job.Customer,
            BuildActionBody(job, title, "MOA",
                $"Step “{step.DisplayName}” ({step.AssigneeName}) has had no approval for 144+ hours. Please follow up."));
    }

    public async Task EmailUserIdsAsync(IEnumerable<int> userIds, string subject, string textBody) =>
        await EmailUsersAsync(userIds, subject, textBody);

    private async Task EmailUsersAsync(IEnumerable<int> userIds, string subject, string textBody)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return;

        var users = await _context.Users.AsNoTracking()
            .Where(u => ids.Contains(u.UserId) && u.Email != "")
            .Select(u => new { u.Email, u.Name })
            .ToListAsync();

        var html = "<p>" + System.Net.WebUtility.HtmlEncode(textBody).Replace("\n", "<br/>") + "</p>";
        foreach (var user in users)
        {
            try
            {
                await _email.SendAsync(user.Email, subject, textBody, html);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to email {Email} for workflow action", user.Email);
            }
        }
    }

    private string BuildActionBody(JobRequest job, string title, string formKind, string cta)
    {
        var frontend = (_config["App:PublicFrontendUrl"] ?? "").TrimEnd('/');
        var linkLine = string.IsNullOrWhiteSpace(frontend)
            ? "Sign in to LGB Services to take action."
            : $"Open: {frontend}";

        return
            $"Company: {job.Customer}\n" +
            $"Document: {title}\n" +
            $"Action: {formKind} approval\n\n" +
            $"{cta}\n\n" +
            $"{linkLine}\n";
    }

    private static string DisplayTitle(JobRequest job, MOIForm? form)
    {
        var curated = MoiFormMetadataHelper.ReadDocumentTitle(form);
        if (!string.IsNullOrWhiteSpace(curated))
            return curated;
        return string.IsNullOrWhiteSpace(job.Service) ? job.TaskType : job.Service;
    }
}
