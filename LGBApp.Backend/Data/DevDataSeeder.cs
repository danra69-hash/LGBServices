using LGBApp.Backend.Models;
using LGBApp.Backend.Services;

namespace LGBApp.Backend.Data;

/// <summary>
/// Seeds a demo customer on first run. Jobs and MOI/MOA forms are created by startup sync services.
/// </summary>
public static class DevDataSeeder
{
    public static void SeedIfEmpty(AppDbContext context)
    {
        if (context.Customers.Any())
            return;

        var purchased = DateTime.UtcNow.Date.AddMonths(-1);
        var expiry = purchased.AddYears(1);

        var customer = new Customer
        {
            Name = "Dan Ra",
            Email = "dra@lgb.test",
            Phone = "",
            Company = "Acme Corp",
            Status = "Active",
            Value = 6980,
            LastContact = DateTime.UtcNow.AddDays(-3),
            InvoiceBy = "Acme Corp",
            ChargeTo = "Acme Corp",
            Package = "Enterprise Plus",
            PackageValue = 6980,
            Cosec = true,
            MoiJson = JsonHelper.Serialize(new[] { "Dan Ra", "Daniel Ra" }),
            MoiApprovalJson = JsonHelper.Serialize(new[] { "Daniel Ra" }),
            MoaJson = JsonHelper.Serialize(new[] { "Dra 3" }),
            PurchasedDate = purchased,
            ExpiryDate = expiry,
            AccountHolders =
            [
                new AccountHolder
                {
                    Name = "Dan Ra",
                    Email = "dra@lgb.test",
                    NeedsMoi = true,
                },
                new AccountHolder
                {
                    Name = "Daniel Ra",
                    Email = "dra2@lgb.test",
                    NeedsMoi = true,
                    NeedsMoiApproval = true,
                },
                new AccountHolder
                {
                    Name = "Dra 3",
                    Email = "dra3@lgb.test",
                    NeedsMoa = true,
                },
            ],
            Packages =
            [
                new CustomerPackage
                {
                    PackageName = "Enterprise Plus",
                    PackageValue = 6980,
                    Validity = "1 Year",
                    PurchasedDate = purchased,
                    ExpiryDate = expiry,
                    Status = "Active",
                },
            ],
        };

        context.Customers.Add(customer);
        context.SaveChanges();
    }
}
