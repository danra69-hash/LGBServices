using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

/// <summary>
/// When every package-linked job is Completed, notify all ClientSignatories once.
/// </summary>
public static class PackageCompletionNotifier
{
    public static async Task TryNotifyIfPackageCompleteAsync(
        AppDbContext context,
        JobRequest job,
        WorkflowNotifier? notifier = null)
    {
        if (!job.CustomerPackageId.HasValue || !job.CustomerId.HasValue)
            return;

        var packageId = job.CustomerPackageId.Value;
        var customerId = job.CustomerId.Value;

        var package = await context.CustomerPackages.FirstOrDefaultAsync(p => p.CustomerPackageId == packageId);
        if (package == null || package.CompletionNotifiedAt.HasValue)
            return;

        var jobs = await context.JobRequests
            .AsNoTracking()
            .Where(j => j.CustomerPackageId == packageId)
            .Select(j => new { j.JobRequestId, j.Status })
            .ToListAsync();

        if (jobs.Count == 0)
            return;

        if (jobs.Any(j => !string.Equals(j.Status, "Completed", StringComparison.OrdinalIgnoreCase)))
            return;

        var signatoryIds = await context.Users
            .AsNoTracking()
            .Where(u => u.CustomerId == customerId && u.Role == UserRoles.ClientSignatory)
            .Select(u => u.UserId)
            .ToListAsync();

        // Also include signatories with multi-company access to this customer.
        var extraIds = await context.SignatoryCustomerAccess
            .AsNoTracking()
            .Where(a => a.CustomerId == customerId)
            .Select(a => a.UserId)
            .ToListAsync();
        signatoryIds = signatoryIds.Concat(extraIds).Distinct().ToList();

        if (signatoryIds.Count == 0)
        {
            package.CompletionNotifiedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            return;
        }

        var company = job.Customer;
        var pkgName = package.PackageName;
        await WorkflowNotificationService.NotifyUsersAsync(
            context,
            signatoryIds,
            "package_complete",
            "Package complete",
            $"{company} — package “{pkgName}” is fully completed.",
            job.JobRequestId,
            customerId);

        if (notifier != null)
        {
            await notifier.EmailUserIdsAsync(
                signatoryIds,
                $"Package complete — {company}",
                $"All services in package “{pkgName}” for {company} are marked completed.\n\nSign in to review history and request further work with Cosec if needed.");
        }

        package.CompletionNotifiedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }
}
