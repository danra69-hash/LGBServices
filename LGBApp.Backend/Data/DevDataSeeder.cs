using LGBApp.Backend.Models;
using LGBApp.Backend.Services;

namespace LGBApp.Backend.Data;

public static class DevDataSeeder
{
    public static void SeedIfEmpty(AppDbContext context)
    {
        if (context.Customers.Any())
            return;

        var customer = new Customer
        {
            Name = "Sarah Johnson",
            Email = "sarah.j@acme.com",
            Phone = "(555) 123-4567",
            Company = "Acme Corp",
            Status = "Active",
            Value = 125000,
            LastContact = DateTime.UtcNow.AddDays(-5),
            InvoiceBy = "Acme Corp",
            ChargeTo = "Acme Corp",
            Package = "Enterprise Package",
            PackageValue = 5930,
            Cosec = true,
            MoiJson = JsonHelper.Serialize(new[] { "Sarah Johnson" }),
            MoiApprovalJson = JsonHelper.Serialize(new[] { "Mike Davis" }),
            MoaJson = JsonHelper.Serialize(new[] { "Sarah Johnson" }),
            PurchasedDate = DateTime.UtcNow.AddMonths(-3),
            ExpiryDate = DateTime.UtcNow.AddMonths(9),
            AccountHolders =
            [
                new AccountHolder { Name = "Sarah Johnson", Email = "sarah.j@acme.com", Phone = "(555) 123-4567" },
                new AccountHolder { Name = "John Smith", Email = "john.s@acme.com", Phone = "(555) 123-4568" },
            ],
        };

        var product = new Product
        {
            PackageName = "Enterprise Package",
            ServicesJson = JsonHelper.Serialize(new[]
            {
                "Annual Return", "BO Declaration", "Prepare Resolution", "Follow up with Reso signatory",
            }),
            ServiceQuantitiesJson = JsonHelper.Serialize(new Dictionary<string, int>
            {
                ["Annual Return"] = 1,
                ["BO Declaration"] = 1,
                ["Prepare Resolution"] = 18,
                ["Follow up with Reso signatory"] = 18,
            }),
            Unit = "EACH",
            QtyPerYear = 1,
            PackagePrice = 5930,
            AddOnsJson = "[]",
            AddOnQuantitiesJson = "{}",
            AddOnsQty = 0,
            AddOnPrice = 0,
        };

        context.Customers.Add(customer);
        context.Products.Add(product);
        context.SaveChanges();

        context.JobRequests.Add(new JobRequest
        {
            Customer = "Acme Corp",
            Service = "Annual Return",
            UsedQty = 0,
            TotalQty = 1,
            DateRequested = DateTime.UtcNow.AddDays(-2),
            AccountHolder = "Sarah Johnson",
            JobAssignedTo = "—",
            Status = "Pending",
        });

        context.CompletedServices.Add(new CompletedService
        {
            Customer = "TechStart Inc",
            Service = "BO Declaration",
            UsedQty = 1,
            TotalQty = 1,
            DateRequested = DateTime.UtcNow.AddMonths(-2),
            DateCompleted = DateTime.UtcNow.AddMonths(-1),
            AccountHolder = "Michael Chen",
            JobAssignedTo = "Jane Smith",
            Status = "Completed",
        });

        context.SaveChanges();
    }
}
