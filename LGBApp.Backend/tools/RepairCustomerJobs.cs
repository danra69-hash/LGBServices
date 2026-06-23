// One-off repair helper — invoke from `dotnet run --project LGBApp.Backend -- repair-jobs`
using LGBApp.Backend.Data;
using LGBApp.Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LGBApp.Backend.Tools;

public static class RepairCustomerJobs
{
    public static async Task RunAsync(AppDbContext context)
    {
        var productsByName = (await context.Products.ToListAsync())
            .GroupBy(p => p.PackageName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var customers = await context.Customers
            .Include(c => c.Packages)
            .Include(c => c.AccountHolders)
            .ToListAsync();

        foreach (var customer in customers)
            await JobRequestSyncService.SyncCustomerWorkAsync(context, customer, productsByName);

        await context.SaveChangesAsync();

        foreach (var job in await context.JobRequests.Where(j => j.TaskType == "Service").ToListAsync())
            await JobRequestUnitService.SyncUnitsForJobAsync(context, job);

        await context.SaveChangesAsync();
        Console.WriteLine($"Repaired jobs for {customers.Count} customers.");
    }
}
