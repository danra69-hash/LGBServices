using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public static class WorkflowNotificationService
{
    public static async Task NotifyUserAsync(
        AppDbContext context,
        int userId,
        string eventType,
        string title,
        string message,
        int? jobRequestId = null,
        int? customerId = null)
    {
        context.AppNotifications.Add(new AppNotification
        {
            UserId = userId,
            EventType = eventType,
            Title = title,
            Message = message,
            JobRequestId = jobRequestId,
            CustomerId = customerId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();
    }

    public static async Task NotifyUsersAsync(
        AppDbContext context,
        IEnumerable<int> userIds,
        string eventType,
        string title,
        string message,
        int? jobRequestId = null,
        int? customerId = null)
    {
        var distinct = userIds.Distinct().ToList();
        if (distinct.Count == 0) return;

        foreach (var userId in distinct)
        {
            context.AppNotifications.Add(new AppNotification
            {
                UserId = userId,
                EventType = eventType,
                Title = title,
                Message = message,
                JobRequestId = jobRequestId,
                CustomerId = customerId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await context.SaveChangesAsync();
    }

    public static async Task NotifyMoiSubmittedAsync(AppDbContext context, JobRequest job)
    {
        var intakeUserIds = await context.Users
            .AsNoTracking()
            .Where(u => u.CanApproveMoiIntake)
            .Select(u => u.UserId)
            .ToListAsync();

        await NotifyUsersAsync(
            context,
            intakeUserIds,
            "moi_submitted",
            "MOI submitted",
            $"{job.Customer} — {job.Service} is awaiting intake review.",
            job.JobRequestId,
            job.CustomerId);
    }

    /// <summary>D1: client skipped MOI/MOA — alert Sharon / intake approvers with their note.</summary>
    public static async Task NotifyAdminBypassAsync(AppDbContext context, JobRequest job, string note)
    {
        var intakeUserIds = await context.Users
            .AsNoTracking()
            .Where(u => u.CanApproveMoiIntake || u.Role == UserRoles.Admin)
            .Select(u => u.UserId)
            .Distinct()
            .ToListAsync();

        var preview = note.Length > 160 ? note[..160] + "…" : note;
        await NotifyUsersAsync(
            context,
            intakeUserIds,
            "admin_bypass",
            "Client request (no MOI/MOA)",
            $"{job.Customer} — {job.Service}: {preview}",
            job.JobRequestId,
            job.CustomerId);
    }

    public static async Task NotifyIntakeApprovedAsync(AppDbContext context, JobRequest job)
    {
        var assigneeIds = await GetJobAssigneeUserIdsAsync(context, job);
        await NotifyUsersAsync(
            context,
            assigneeIds,
            "moi_intake_approved",
            "MOI intake approved",
            $"{job.Customer} — {job.Service} is ready for secretarial assignment.",
            job.JobRequestId,
            job.CustomerId);
    }

    public static async Task NotifyMoaReadyForClientAsync(AppDbContext context, JobRequest job, Customer customer)
    {
        var clientUserIds = customer.AccountHolders
            .Where(h => h.NeedsMoa && h.UserId.HasValue)
            .Select(h => h.UserId!.Value)
            .ToList();

        if (clientUserIds.Count == 0 && customer.CustomerId > 0)
        {
            clientUserIds = await context.Users
                .AsNoTracking()
                .Where(u => u.CustomerId == customer.CustomerId && u.Role != UserRoles.User)
                .Select(u => u.UserId)
                .ToListAsync();
        }

        await NotifyUsersAsync(
            context,
            clientUserIds,
            "moa_ready",
            "MOA ready for sign-off",
            $"{job.Customer} — {job.Service} MOA is ready for your signature.",
            job.JobRequestId,
            customer.CustomerId);
    }

    public static async Task NotifyJobCompletedAsync(AppDbContext context, JobRequest job)
    {
        var userIds = await GetJobAssigneeUserIdsAsync(context, job);
        var moaApprovers = await context.Users
            .AsNoTracking()
            .Where(u => u.CanApproveMoa)
            .Select(u => u.UserId)
            .ToListAsync();
        userIds.AddRange(moaApprovers);

        await NotifyUsersAsync(
            context,
            userIds,
            "job_completed",
            "Job completed",
            $"{job.Customer} — {job.Service} has been completed.",
            job.JobRequestId,
            job.CustomerId);
    }

    private static async Task<List<int>> GetJobAssigneeUserIdsAsync(AppDbContext context, JobRequest job)
    {
        await context.Entry(job).Collection(j => j.Units).LoadAsync();
        var ids = new List<int>();
        foreach (var unit in job.Units)
        {
            if (unit.AssignedUserId.HasValue)
                ids.Add(unit.AssignedUserId.Value);
            await context.Entry(unit).Collection(u => u.Assignees).LoadAsync();
            ids.AddRange(unit.Assignees.Select(a => a.UserId));
        }

        return ids.Distinct().ToList();
    }
}
