using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public static class JobRequestUnitService
{
    public static async Task SyncUnitsForJobAsync(AppDbContext context, JobRequest job)
    {
        var existing = await context.JobRequestUnits
            .Where(u => u.JobRequestId == job.JobRequestId)
            .ToListAsync();

        for (var n = 1; n <= job.TotalQty; n++)
        {
            if (existing.All(u => u.UnitNumber != n))
            {
                context.JobRequestUnits.Add(new JobRequestUnit
                {
                    JobRequestId = job.JobRequestId,
                    UnitNumber = n,
                    Status = "Pending",
                });
            }
        }

        foreach (var extra in existing.Where(u => u.UnitNumber > job.TotalQty && u.Status != "Completed"))
            context.JobRequestUnits.Remove(extra);

        await context.SaveChangesAsync();
        await RefreshJobAggregateAsync(context, job);
    }

    public static async Task SyncAllJobUnitsAsync(AppDbContext context)
    {
        var jobs = await context.JobRequests.ToListAsync();
        foreach (var job in jobs)
            await SyncUnitsForJobAsync(context, job);
    }

    public static JobRequestUnit? ResolveUnit(JobRequest job, int? unitNumber)
    {
        var units = job.Units.OrderBy(u => u.UnitNumber).ToList();
        if (units.Count == 0)
            return null;

        if (unitNumber.HasValue)
            return units.FirstOrDefault(u => u.UnitNumber == unitNumber.Value);

        return units.FirstOrDefault(u => u.Status == "Pending" && !HasAssignees(u))
            ?? units.FirstOrDefault(u => u.Status != "Completed");
    }

    public static bool HasAssignees(JobRequestUnit unit) =>
        unit.Assignees.Count > 0 || unit.AssignedUserId.HasValue;

    public static async Task<List<JobRequestUnitAssignee>> LoadAssigneesAsync(AppDbContext context, JobRequestUnit unit)
    {
        return await context.JobRequestUnitAssignees
            .Include(a => a.User)
            .Where(a => a.JobRequestUnitId == unit.JobRequestUnitId)
            .OrderBy(a => a.JobRequestUnitAssigneeId)
            .ToListAsync();
    }

    public static async Task AddAssigneeAsync(AppDbContext context, JobRequestUnit unit, User user)
    {
        var exists = await context.JobRequestUnitAssignees
            .AnyAsync(a => a.JobRequestUnitId == unit.JobRequestUnitId && a.UserId == user.UserId);
        if (exists)
            return;

        context.JobRequestUnitAssignees.Add(new JobRequestUnitAssignee
        {
            JobRequestUnitId = unit.JobRequestUnitId,
            UserId = user.UserId,
        });

        await SyncUnitAssigneeFieldsAsync(context, unit);
        if (unit.Status == "Pending")
            unit.Status = "In Progress";
    }

    public static async Task RemoveAssigneeAsync(AppDbContext context, JobRequestUnit unit, int userId)
    {
        var row = await context.JobRequestUnitAssignees
            .FirstOrDefaultAsync(a => a.JobRequestUnitId == unit.JobRequestUnitId && a.UserId == userId);
        if (row != null)
            context.JobRequestUnitAssignees.Remove(row);

        await SyncUnitAssigneeFieldsAsync(context, unit);
        if (!unit.AssignedUserId.HasValue && unit.Status == "In Progress")
            unit.Status = "Pending";
    }

    public static async Task SyncUnitAssigneeFieldsAsync(AppDbContext context, JobRequestUnit unit)
    {
        var assignees = await LoadAssigneesAsync(context, unit);
        var names = assignees
            .Select(a => a.User.Name.Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        unit.AssignedUserId = assignees.FirstOrDefault()?.UserId;
        unit.AssignedUserName = string.Join(", ", names);
    }

    public static async Task RefreshJobAggregateAsync(AppDbContext context, JobRequest job)
    {
        var units = await context.JobRequestUnits
            .Include(u => u.Assignees)
            .ThenInclude(a => a.User)
            .Where(u => u.JobRequestId == job.JobRequestId)
            .OrderBy(u => u.UnitNumber)
            .ToListAsync();

        foreach (var unit in units)
            await SyncUnitAssigneeFieldsAsync(context, unit);

        job.UsedQty = units.Count(u => u.Status == "Completed");

        var assigneeNames = units
            .SelectMany(u => u.Assignees.Select(a => a.User.Name))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        job.JobAssignedTo = string.Join(", ", assigneeNames);
        job.AssignedUserId = units
            .SelectMany(u => u.Assignees)
            .Select(a => (int?)a.UserId)
            .FirstOrDefault();
        job.ScheduledDate = units
            .Where(u => u.ScheduledDate.HasValue && u.Status != "Completed")
            .OrderBy(u => u.ScheduledDate)
            .FirstOrDefault()?.ScheduledDate;

        if (job.UsedQty >= job.TotalQty && job.TotalQty > 0)
        {
            job.Status = "Completed";
            job.DateCompleted ??= DateTime.UtcNow;
        }
        else
        {
            job.DateCompleted = null;
            if (units.Any(u => u.Status is "In Progress" or "Completed"))
                job.Status = "In Progress";
            else
                job.Status = "Pending";
        }
    }

    public static async Task RevertUnitCompleteAsync(AppDbContext context, JobRequestUnit unit, JobRequest job)
    {
        if (unit.Status != "Completed")
            return;

        unit.CompletedAt = null;
        var assignees = await LoadAssigneesAsync(context, unit);
        unit.Status = assignees.Count > 0 || unit.AssignedUserId.HasValue ? "In Progress" : "Pending";
        await SyncUnitToTrackerAsync(context, unit, job);
    }

    public static async Task RemoveLatestCompletedServiceRecordAsync(AppDbContext context, JobRequest job)
    {
        var recorded = await context.CompletedServices
            .Where(c => c.Customer == job.Customer
                && c.Service == job.Service
                && c.AccountHolder == job.AccountHolder
                && c.Status == "Completed")
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (recorded != null)
            context.CompletedServices.Remove(recorded);
    }

    public static async Task SyncUnitToTrackerAsync(AppDbContext context, JobRequestUnit unit, JobRequest job)
    {
        if (!unit.ScheduledDate.HasValue)
        {
            await RemoveLinkedScheduleItemAsync(context, unit);
            return;
        }

        var mois = await context.MOIForms
            .Where(f => f.JobRequestId == job.JobRequestId)
            .ToListAsync();
        var moi = InternalWorkVisibilityHelper.ResolveMoiForUnit(mois, unit, job);
        if (!InternalWorkVisibilityHelper.IsUnitReleasedToInternal(job, unit, moi))
        {
            await RemoveLinkedScheduleItemAsync(context, unit);
            return;
        }

        if (!job.CustomerId.HasValue)
            return;

        var customerPackageId = job.CustomerPackageId;
        if (!customerPackageId.HasValue)
        {
            var pkg = await context.CustomerPackages
                .Where(p => p.CustomerId == job.CustomerId && p.Status == "Active")
                .OrderByDescending(p => p.PurchasedDate)
                .FirstOrDefaultAsync();
            customerPackageId = pkg?.CustomerPackageId;
        }

        if (!customerPackageId.HasValue)
            return;

        var assignees = await LoadAssigneesAsync(context, unit);
        var assigneeLabel = assignees.Count > 0
            ? string.Join(", ", assignees.Select(a => a.User.Name))
            : unit.AssignedUserName;

        var label = string.IsNullOrWhiteSpace(job.TaskType) || job.TaskType == "Service"
            ? job.Service
            : job.TaskType;
        var title = job.TotalQty > 1 ? $"{label} #{unit.UnitNumber}" : label;
        if (!string.IsNullOrWhiteSpace(job.AccountHolder))
            title += $" — {job.AccountHolder}";

        var scheduledAt = unit.ScheduledDate.Value;
        var primaryUserId = assignees.FirstOrDefault()?.UserId ?? unit.AssignedUserId;

        PackageScheduleItem? item = null;
        if (unit.PackageScheduleItemId.HasValue)
            item = await context.PackageScheduleItems.FindAsync(unit.PackageScheduleItemId.Value);

        if (item == null)
        {
            item = new PackageScheduleItem
            {
                CustomerId = job.CustomerId.Value,
                CustomerPackageId = customerPackageId.Value,
                ItemType = "work",
                Title = title,
                ScheduledAt = scheduledAt,
                Status = unit.Status == "Completed" ? "completed" : "scheduled",
                SequenceNumber = unit.UnitNumber,
                JobRequestUnitId = unit.JobRequestUnitId,
                AssignedUserId = primaryUserId,
                Notes = assigneeLabel,
            };
            context.PackageScheduleItems.Add(item);
            await context.SaveChangesAsync();
            unit.PackageScheduleItemId = item.PackageScheduleItemId;
            return;
        }

        item.Title = title;
        item.ScheduledAt = scheduledAt;
        item.SequenceNumber = unit.UnitNumber;
        item.Status = unit.Status == "Completed" ? "completed" : "scheduled";
        item.AssignedUserId = primaryUserId;
        item.Notes = assigneeLabel;
        item.JobRequestUnitId = unit.JobRequestUnitId;
    }

    private static async Task RemoveLinkedScheduleItemAsync(AppDbContext context, JobRequestUnit unit)
    {
        if (!unit.PackageScheduleItemId.HasValue)
            return;

        var linked = await context.PackageScheduleItems.FindAsync(unit.PackageScheduleItemId.Value);
        if (linked != null)
            context.PackageScheduleItems.Remove(linked);
        unit.PackageScheduleItemId = null;
    }

    public static bool IsUserAssigned(JobRequestUnit unit, int userId) =>
        unit.Assignees.Any(a => a.UserId == userId) || unit.AssignedUserId == userId;

    public static JobRequestUnitDto ToDto(JobRequestUnit unit)
    {
        var assignees = unit.Assignees
            .OrderBy(a => a.JobRequestUnitAssigneeId)
            .Select(a => new UnitAssigneeDto
            {
                UserId = a.User.UserId,
                UserName = a.User.Name,
            })
            .ToList();

        return new JobRequestUnitDto
        {
            Id = unit.JobRequestUnitId,
            UnitNumber = unit.UnitNumber,
            AssignedUserId = assignees.FirstOrDefault()?.UserId ?? unit.AssignedUserId,
            AssignedUserName = assignees.Count > 0
                ? string.Join(", ", assignees.Select(a => a.UserName))
                : unit.AssignedUserName,
            Assignees = assignees,
            ScheduledDate = DateOnlyHelper.Format(unit.ScheduledDate),
            Status = unit.Status,
            InternalHandoffStatus = unit.InternalHandoffStatus,
        };
    }

    public static WorkTrackerItemDto ToTrackerDto(JobRequestUnit unit, JobRequest job)
    {
        var assignees = unit.Assignees
            .OrderBy(a => a.JobRequestUnitAssigneeId)
            .ToList();
        var primary = assignees.FirstOrDefault();

        return new WorkTrackerItemDto
        {
            UnitId = unit.JobRequestUnitId,
            JobId = job.JobRequestId,
            UnitNumber = unit.UnitNumber,
            CustomerPackageId = job.CustomerPackageId,
            Customer = job.Customer,
            TaskType = job.TaskType,
            Service = job.Service,
            AccountHolder = job.AccountHolder,
            ScheduledDate = DateOnlyHelper.Format(unit.ScheduledDate),
            DateRequested = job.DateRequested.ToString("yyyy-MM-dd"),
            Status = unit.Status,
            AssignedUserId = primary?.UserId ?? unit.AssignedUserId,
            AssignedUserName = assignees.Count > 0
                ? string.Join(", ", assignees.Select(a => a.User.Name))
                : unit.AssignedUserName,
            Assignees = assignees.Select(a => new UnitAssigneeDto
            {
                UserId = a.User.UserId,
                UserName = a.User.Name,
            }).ToList(),
            InternalHandoffStatus = JobHandoffResolver.ResolveEffectiveHandoff(job, unit),
        };
    }
}
