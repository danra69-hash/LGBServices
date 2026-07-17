using LGBApp.Backend.Data;
using LGBApp.Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Tools;

/// <summary>§7.3: heavy CubeV import + job bootstrap — run via <c>dotnet run -- seed-full</c>, not on every boot.</summary>
public static class SeedFullCommand
{
    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Console.WriteLine("[seed-full] Importing CubeV customers and bootstrapping jobs…");

        CubeVCustomerSeeder.SeedIfNeeded(context);
        CosecWorkdoneImporter.Seed(context);
        await BillingPartyService.SeedFromLegacyCustomerFieldsAsync(context);
        await CustomerClientAdminProvisioner.EnsureAllCustomersHaveClientAdminAsync(context);
        await CustomerSignatoryProvisioner.EnsureAllCustomerSignatoriesAsync(context);
        await scope.ServiceProvider.GetRequiredService<SignatoryAccessService>()
            .BackfillFromHoldersAsync(context);
        FigmaProductCatalog.SyncCatalog(context);
        JobRequestSyncService.LinkOrphanJobs(context);
        await context.SaveChangesAsync();

        var needsFullJobBootstrap = !context.JobRequests.Any() && context.Customers.Any();
        if (needsFullJobBootstrap)
        {
            Console.WriteLine("[seed-full] Bootstrapping job requests…");
            await JobRequestSyncService.SyncAllCustomersAsync(context);
            await JobWorkflowIntegrityService.RepairAllAsync(context);
            var allJobs = await context.JobRequests.ToListAsync();
            await JobFormProvisioner.EnsureFormsForJobsAsync(context, allJobs);
            Console.WriteLine($"[seed-full] Job bootstrap complete — {allJobs.Count} jobs.");
        }
        else
        {
            var jobsMissingMoi = await context.JobRequests
                .Where(j => j.TaskType == "Service"
                    && j.CustomerPackageId != null
                    && j.TotalQty <= 1
                    && !context.MOIForms.Any(m => m.JobRequestId == j.JobRequestId))
                .ToListAsync();
            if (jobsMissingMoi.Count > 0)
                await JobFormProvisioner.EnsureFormsForJobsAsync(context, jobsMissingMoi);
            Console.WriteLine("[seed-full] Customers exist; skipped full job bootstrap (already have jobs).");
        }

        FormCustomerIdBackfill.Apply(context);
        Console.WriteLine("[seed-full] Done.");
    }
}
