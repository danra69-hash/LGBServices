using LGBApp.Backend.Models;
using LGBApp.Backend.Services;

namespace LGBApp.Backend.Tests;

public class JobHandoffResolverTests
{
    [Fact]
    public void MirrorJobHandoff_SingleQty_UpdatesUnitAndJob()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer, handoff: JobHandoffStatuses.ResoInProgress);
        var unit = job.Units.First();
        unit.InternalHandoffStatus = JobHandoffStatuses.PendingPrep;

        JobHandoffResolver.MirrorJobHandoff(job, unit, JobHandoffStatuses.AdminReview);

        Assert.Equal(JobHandoffStatuses.AdminReview, job.InternalHandoffStatus);
        Assert.Equal(JobHandoffStatuses.AdminReview, unit.InternalHandoffStatus);
        Assert.Equal(JobHandoffStatuses.AdminReview, JobHandoffResolver.ResolveEffectiveHandoff(job, unit));
    }
}
