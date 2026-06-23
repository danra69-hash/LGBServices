using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public static class JobRequestSyncService
{
    private static readonly string[] FormTaskTypes = ["MOI", "MOI Approval", "MOA"];

    public static async Task SyncAllCustomersAsync(AppDbContext context)
    {
        var customers = await context.Customers
            .Include(c => c.AccountHolders)
            .Include(c => c.Packages)
            .ToListAsync();

        var products = await context.Products.ToListAsync();
        var productsByName = products
            .GroupBy(p => p.PackageName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var customer in customers)
            await SyncCustomerWorkAsync(context, customer, productsByName);

        await context.SaveChangesAsync();

        var jobs = await context.JobRequests.ToListAsync();
        foreach (var job in jobs)
            await JobRequestUnitService.SyncUnitsForJobAsync(context, job);

        await JobWorkflowIntegrityService.RepairAllAsync(context);
    }

    public static async Task SyncCustomerWorkAsync(
        AppDbContext context,
        Customer customer,
        Dictionary<string, Product>? productsByName = null)
    {
        productsByName ??= (await context.Products.ToListAsync())
            .GroupBy(p => p.PackageName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        await context.Entry(customer).Collection(c => c.Packages).LoadAsync();
        await context.Entry(customer).Collection(c => c.AccountHolders).LoadAsync();

        await RemoveLegacyFormJobsAsync(context, customer);
        foreach (var package in customer.Packages)
            await SyncPackageServiceJobsAsync(context, customer, package, productsByName);

        var serviceJobs = await context.JobRequests
            .Where(j => j.CustomerId == customer.CustomerId && j.TaskType == "Service")
            .ToListAsync();
        await JobFormProvisioner.EnsureFormsForJobsAsync(context, serviceJobs);
    }

    /// <summary>
    /// MOI/MOA workflow runs per package service line — not as standalone customer-level form jobs.
    /// Remove legacy MOI / MOI Approval / MOA rows that predate per-item workflow.
    /// </summary>
    public static async Task RemoveLegacyFormJobsAsync(AppDbContext context, Customer customer)
    {
        var legacy = await context.JobRequests
            .Where(j => j.CustomerId == customer.CustomerId
                && j.CustomerPackageId == null
                && FormTaskTypes.Contains(j.TaskType))
            .ToListAsync();

        foreach (var job in legacy)
            context.JobRequests.Remove(job);
    }

    public static async Task SyncPackageServiceJobsAsync(
        AppDbContext context,
        Customer customer,
        CustomerPackage package,
        Dictionary<string, Product> productsByName)
    {
        Dictionary<string, int> serviceQuantities;
        Dictionary<string, int> addOnQuantities;

        if (CustomerPackageNames.IsAddOnsOnly(package.PackageName))
        {
            serviceQuantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            addOnQuantities = ResolveCustomerAddOnQuantities(package);
        }
        else if (!productsByName.TryGetValue(package.PackageName, out var product))
        {
            return;
        }
        else
        {
            serviceQuantities = JsonHelper.Deserialize<Dictionary<string, int>>(product.ServiceQuantitiesJson);
            var productAddOnQuantities = JsonHelper.Deserialize<Dictionary<string, int>>(product.AddOnQuantitiesJson);
            addOnQuantities = ResolveAddOnQuantities(package, productAddOnQuantities);
        }

        var desired = new List<(string Service, int Qty)>();
        foreach (var (service, qty) in serviceQuantities.Where(kv => kv.Value > 0))
            desired.Add((service, qty));
        foreach (var (addOn, qty) in addOnQuantities.Where(kv => kv.Value > 0))
            desired.Add((addOn, qty));

        var existing = await context.JobRequests
            .Where(j => j.CustomerPackageId == package.CustomerPackageId)
            .ToListAsync();
        existing = existing.Where(j => !FormTaskTypes.Contains(j.TaskType)).ToList();

        foreach (var (service, qty) in desired)
        {
            var job = existing.FirstOrDefault(j =>
                string.Equals(j.Service, service, StringComparison.OrdinalIgnoreCase));

            if (job == null)
            {
                context.JobRequests.Add(new JobRequest
                {
                    CustomerId = customer.CustomerId,
                    CustomerPackageId = package.CustomerPackageId,
                    Customer = customer.Company,
                    TaskType = "Service",
                    Service = service,
                    AccountHolder = string.Empty,
                    Status = "Pending",
                    DateRequested = DateTime.UtcNow,
                    TotalQty = qty,
                    UsedQty = 0,
                    JobAssignedTo = string.Empty,
                });
            }
            else
            {
                job.Customer = customer.Company;
                job.TotalQty = qty;
                if (job.UsedQty > job.TotalQty)
                    job.UsedQty = job.TotalQty;
            }
        }

        foreach (var job in existing)
        {
            if (job.Status is "Completed" or "Canceled")
                continue;

            var stillNeeded = desired.Any(d =>
                string.Equals(d.Service, job.Service, StringComparison.OrdinalIgnoreCase));

            if (!stillNeeded)
                context.JobRequests.Remove(job);
        }

        RemoveDuplicateJobs(context, existing);
    }

    /// <summary>
    /// Customer package add-ons (PricingJson) override catalog bundled quantities when qty &gt; 0;
    /// otherwise fall back to the product template bundled add-ons.
    /// </summary>
    internal static Dictionary<string, int> ResolveAddOnQuantities(
        CustomerPackage package,
        Dictionary<string, int> productAddOnQuantities)
    {
        var customerLines = ResolveCustomerAddOnQuantities(package);
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var allNames = productAddOnQuantities.Keys.Union(customerLines.Keys, StringComparer.OrdinalIgnoreCase);

        foreach (var name in allNames)
        {
            if (customerLines.TryGetValue(name, out var customerQty) && customerQty > 0)
            {
                result[name] = customerQty;
                continue;
            }

            if (productAddOnQuantities.TryGetValue(name, out var productQty) && productQty > 0)
                result[name] = productQty;
        }

        return result;
    }

    internal static Dictionary<string, int> ResolveCustomerAddOnQuantities(CustomerPackage package)
    {
        var pricing = string.IsNullOrWhiteSpace(package.PricingJson) || package.PricingJson == "{}"
            ? new Models.DTOs.PackagePricingDto()
            : JsonHelper.Deserialize<Models.DTOs.PackagePricingDto>(package.PricingJson);

        return (pricing.AddOnLines ?? [])
            .Where(l => !string.IsNullOrWhiteSpace(l.Name) && l.Qty > 0)
            .GroupBy(l => l.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Qty, StringComparer.OrdinalIgnoreCase);
    }

    private static void RemoveDuplicateJobs(AppDbContext context, List<JobRequest> existing)
    {
        var duplicateGroups = existing
            .Where(j => j.Status is not ("Completed" or "Canceled"))
            .GroupBy(j => $"{j.TaskType}|{j.Service}|{j.AccountHolder}", StringComparer.OrdinalIgnoreCase);

        foreach (var group in duplicateGroups)
        {
            foreach (var extra in group.OrderBy(j => j.JobRequestId).Skip(1))
                context.JobRequests.Remove(extra);
        }
    }

    private static (string Email, string Phone) ResolveHolderContact(Customer customer, string holderName)
    {
        var holder = customer.AccountHolders
            .FirstOrDefault(h => string.Equals(h.Name.Trim(), holderName.Trim(), StringComparison.OrdinalIgnoreCase));

        return (holder?.Email ?? string.Empty, holder?.Phone ?? string.Empty);
    }

    public static void LinkOrphanJobs(AppDbContext context)
    {
        var customersByCompany = context.Customers
            .AsEnumerable()
            .GroupBy(c => c.Company, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var job in context.JobRequests.Where(j => j.CustomerId == null))
        {
            if (customersByCompany.TryGetValue(job.Customer, out var customer))
            {
                job.CustomerId = customer.CustomerId;
                if (string.IsNullOrWhiteSpace(job.TaskType))
                    job.TaskType = "Service";
            }
        }
    }
}
