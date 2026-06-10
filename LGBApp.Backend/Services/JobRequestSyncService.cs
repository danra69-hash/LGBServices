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
        var productsByName = products.ToDictionary(p => p.PackageName, StringComparer.OrdinalIgnoreCase);

        foreach (var customer in customers)
            await SyncCustomerWorkAsync(context, customer, productsByName);

        await context.SaveChangesAsync();

        var jobs = await context.JobRequests.ToListAsync();
        foreach (var job in jobs)
            await JobRequestUnitService.SyncUnitsForJobAsync(context, job);
    }

    public static async Task SyncCustomerWorkAsync(
        AppDbContext context,
        Customer customer,
        Dictionary<string, Product>? productsByName = null)
    {
        productsByName ??= (await context.Products.ToListAsync())
            .ToDictionary(p => p.PackageName, StringComparer.OrdinalIgnoreCase);

        await context.Entry(customer).Collection(c => c.Packages).LoadAsync();
        await context.Entry(customer).Collection(c => c.AccountHolders).LoadAsync();

        await SyncCustomerFormJobsAsync(context, customer);
        foreach (var package in customer.Packages)
            await SyncPackageServiceJobsAsync(context, customer, package, productsByName);
    }

    public static async Task SyncCustomerFormJobsAsync(AppDbContext context, Customer customer)
    {
        var moi = JsonHelper.Deserialize<List<string>>(customer.MoiJson);
        var moiApproval = JsonHelper.Deserialize<List<string>>(customer.MoiApprovalJson);
        var moa = JsonHelper.Deserialize<List<string>>(customer.MoaJson);

        var desired = new List<(string TaskType, string Holder)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void AddDesired(string taskType, IEnumerable<string> names)
        {
            foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                var holder = name.Trim();
                var key = $"{taskType}|{holder}";
                if (seen.Add(key))
                    desired.Add((taskType, holder));
            }
        }

        AddDesired("MOI", moi);
        AddDesired("MOI Approval", moiApproval);
        AddDesired("MOA", moa);

        var existing = await context.JobRequests
            .Where(j => j.CustomerId == customer.CustomerId && j.CustomerPackageId == null)
            .ToListAsync();
        existing = existing.Where(j => FormTaskTypes.Contains(j.TaskType)).ToList();

        foreach (var (taskType, holder) in desired)
        {
            var job = existing.FirstOrDefault(j =>
                j.TaskType == taskType &&
                string.Equals(j.AccountHolder, holder, StringComparison.OrdinalIgnoreCase));

            var (email, phone) = ResolveHolderContact(customer, holder);

            if (job == null)
            {
                context.JobRequests.Add(new JobRequest
                {
                    CustomerId = customer.CustomerId,
                    CustomerPackageId = null,
                    Customer = customer.Company,
                    TaskType = taskType,
                    Service = taskType,
                    AccountHolder = holder,
                    AccountHolderEmail = email,
                    AccountHolderPhone = phone,
                    Status = "Pending",
                    DateRequested = DateTime.UtcNow,
                    TotalQty = 1,
                    UsedQty = 0,
                    JobAssignedTo = string.Empty,
                });
            }
            else
            {
                job.Customer = customer.Company;
                job.AccountHolderEmail = email;
                job.AccountHolderPhone = phone;
            }
        }

        foreach (var job in existing)
        {
            if (job.Status is "Completed" or "Canceled")
                continue;

            var stillNeeded = desired.Any(d =>
                d.TaskType == job.TaskType &&
                string.Equals(d.Holder, job.AccountHolder, StringComparison.OrdinalIgnoreCase));

            if (!stillNeeded)
                context.JobRequests.Remove(job);
        }

        RemoveDuplicateJobs(context, existing);
    }

    public static async Task SyncPackageServiceJobsAsync(
        AppDbContext context,
        Customer customer,
        CustomerPackage package,
        Dictionary<string, Product> productsByName)
    {
        if (!productsByName.TryGetValue(package.PackageName, out var product))
            return;

        var serviceQuantities = JsonHelper.Deserialize<Dictionary<string, int>>(product.ServiceQuantitiesJson);
        var addOnQuantities = JsonHelper.Deserialize<Dictionary<string, int>>(product.AddOnQuantitiesJson);

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
