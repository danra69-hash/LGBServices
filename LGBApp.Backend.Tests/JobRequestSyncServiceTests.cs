using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;
using LGBApp.Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Tests;

public class JobRequestSyncServiceTests
{
    [Fact]
    public async Task AddOnsOnlyPackage_SyncsAddOnServiceJobs()
    {
        using var db = new TestDbFactory();
        var customer = new Customer
        {
            Name = "Add On",
            Email = "addon@test.local",
            Company = "Add On Co",
            Status = "Active",
            Package = CustomerPackageNames.AddOnsOnly,
            Packages =
            [
                new CustomerPackage
                {
                    PackageName = CustomerPackageNames.AddOnsOnly,
                    PackageValue = 240,
                    Validity = "1 Year",
                    Status = "Active",
                    PurchasedDate = DateTime.UtcNow,
                    ExpiryDate = DateTime.UtcNow.AddYears(1),
                    PricingJson = JsonHelper.Serialize(new PackagePricingDto
                    {
                        BasePackagePrice = 0,
                        AddOnLines =
                        [
                            new AddOnLineDto { Name = "Overseas Support Service", Qty = 2, UnitPrice = 120 },
                        ],
                    }),
                },
            ],
        };
        db.Context.Customers.Add(customer);
        await db.Context.SaveChangesAsync();

        await JobRequestSyncService.SyncCustomerWorkAsync(db.Context, customer);
        await db.Context.SaveChangesAsync();

        var jobs = await db.Context.JobRequests
            .Where(j => j.CustomerId == customer.CustomerId)
            .ToListAsync();

        Assert.NotEmpty(jobs);
        Assert.Contains(jobs, j => j.Service == "Overseas Support Service" && j.TotalQty == 2);
    }
}
