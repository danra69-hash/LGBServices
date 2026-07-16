using LGBApp.Backend.Models;
using LGBApp.Backend.Services;

namespace LGBApp.Backend.Tests;

public class SessionActivationTests
{
    [Fact]
    public void Qty1_AlwaysActive_NeverDormant()
    {
        var job = new JobRequest { TotalQty = 1 };
        var unit = new JobRequestUnit { UnitNumber = 1, Status = "Pending" };
        Assert.True(JobRequestUnitService.IsSessionActive(job, unit));
        Assert.False(JobRequestUnitService.IsSessionDormant(job, unit));
    }

    [Fact]
    public void MultiQty_WithoutStamp_IsDormant()
    {
        var job = new JobRequest { TotalQty = 10 };
        var unit = new JobRequestUnit { UnitNumber = 1, Status = "Pending" };
        Assert.True(JobRequestUnitService.IsSessionDormant(job, unit));
        Assert.False(JobRequestUnitService.IsSessionActive(job, unit));
    }

    [Fact]
    public async Task Activate_ClaimsLowestDormant_ThenNext()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer, totalQty: 3);
        await JobRequestUnitService.SyncUnitsForJobAsync(db.Context, job);
        await db.Context.Entry(job).Collection(j => j.Units).LoadAsync();

        Assert.Equal(3, job.Units.Count(u => JobRequestUnitService.IsSessionDormant(job, u)));

        var first = await JobRequestUnitService.TryActivateNextSessionAsync(db.Context, job);
        Assert.NotNull(first);
        Assert.Equal(1, first!.UnitNumber);
        Assert.True(first.ClientActivatedAt.HasValue);
        await db.Context.SaveChangesAsync();

        var second = await JobRequestUnitService.TryActivateNextSessionAsync(db.Context, job);
        Assert.NotNull(second);
        Assert.Equal(2, second!.UnitNumber);
        await db.Context.SaveChangesAsync();

        var third = await JobRequestUnitService.TryActivateNextSessionAsync(db.Context, job);
        Assert.NotNull(third);
        Assert.Equal(3, third!.UnitNumber);
        await db.Context.SaveChangesAsync();

        var none = await JobRequestUnitService.TryActivateNextSessionAsync(db.Context, job);
        Assert.Null(none);
    }

    [Fact]
    public async Task Activate_Qty1_ThrowsDomainException()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer, totalQty: 1);
        await JobRequestUnitService.SyncUnitsForJobAsync(db.Context, job);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => JobRequestUnitService.TryActivateNextSessionAsync(db.Context, job));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Backfill_MarksInFlightAsActive()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer, totalQty: 2);
        await JobRequestUnitService.SyncUnitsForJobAsync(db.Context, job);
        await db.Context.Entry(job).Collection(j => j.Units).LoadAsync();

        var u1 = job.Units.OrderBy(u => u.UnitNumber).First();
        u1.InternalHandoffStatus = JobHandoffStatuses.ClientSubmitted;
        await db.Context.SaveChangesAsync();

        await JobRequestUnitService.BackfillClientActivatedAsync(db.Context, job);
        Assert.True(u1.ClientActivatedAt.HasValue);
        Assert.False(job.Units.OrderBy(u => u.UnitNumber).Last().ClientActivatedAt.HasValue);
    }
}
