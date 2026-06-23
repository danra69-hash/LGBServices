using LGBApp.Backend.Models;
using LGBApp.Backend.Services;

namespace LGBApp.Backend.Tests;

public class UnitAssignmentGateTests
{
    [Fact]
    public void CannotAssignSecretarialTeam_WhenAwaitingIntake()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer, handoff: JobHandoffStatuses.ClientSubmitted);
        var unit = job.Units.First();
        var moi = db.SeedMoi(job, unit, MoiWorkflowStates.PendingAdminIntake);

        Assert.False(UnitAssignmentGate.CanAssignSecretarialTeamForUnit(job, unit, moi));
    }

    [Fact]
    public void CanAssignSecretarialTeam_WhenAwaitingSecAssignment()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer, handoff: JobHandoffStatuses.AwaitingSecAssignment);
        var unit = job.Units.First();
        var moi = db.SeedMoi(job, unit, MoiWorkflowStates.Approved);

        Assert.True(UnitAssignmentGate.CanAssignSecretarialTeamForUnit(job, unit, moi));
    }

    [Fact]
    public void CannotAssignUnit2_UntilUnit1MoiComplete()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer, totalQty: 2);
        var unit1 = job.Units.First(u => u.UnitNumber == 1);
        var unit2 = job.Units.First(u => u.UnitNumber == 2);
        unit1.InternalHandoffStatus = JobHandoffStatuses.ClientSubmitted;
        var moi1 = db.SeedMoi(job, unit1, MoiWorkflowStates.PendingAdminIntake);

        Assert.False(UnitAssignmentGate.CanAssignUnit(job, unit2, job.Units.ToList(), null, moi1));
    }

    [Fact]
    public void CanAssignUnit2_AfterUnit1MoiApproved()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer, totalQty: 2, handoff: JobHandoffStatuses.PendingPrep);
        var unit1 = job.Units.First(u => u.UnitNumber == 1);
        var unit2 = job.Units.First(u => u.UnitNumber == 2);
        unit1.InternalHandoffStatus = JobHandoffStatuses.PendingPrep;
        var moi1 = db.SeedMoi(job, unit1, MoiWorkflowStates.Approved);
        var moi2 = db.SeedMoi(job, unit2, MoiWorkflowStates.Draft);

        Assert.True(UnitAssignmentGate.CanAssignUnit(job, unit2, job.Units.ToList(), moi2, moi1));
    }

    [Fact]
    public void CannotAssign_WhenReadyForMoa()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer, handoff: JobHandoffStatuses.ReadyForMoa);
        var unit = job.Units.First();
        var moi = db.SeedMoi(job, unit, MoiWorkflowStates.Approved);

        Assert.False(UnitAssignmentGate.CanAssignSecretarialTeamForUnit(job, unit, moi));
    }
}
