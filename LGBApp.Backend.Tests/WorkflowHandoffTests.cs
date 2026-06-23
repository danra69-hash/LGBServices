using LGBApp.Backend.Models;
using LGBApp.Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Tests;

public class WorkflowHandoffTests
{
    [Fact]
    public async Task SharonSignOff_SetsPendingPrep_ProvisionsMoa_AndPendingPrepMoi()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer, handoff: JobHandoffStatuses.ClientSubmitted);
        var moi = db.SeedMoi(job, workflowState: MoiWorkflowStates.PendingAdminIntake);

        await JobHandoffService.OnMoiSharonSignOffAsync(db.Context, job, moi);

        Assert.Equal(MoiWorkflowStates.PendingPrep, moi.WorkflowState);
        Assert.Equal(JobHandoffStatuses.PendingPrep, job.InternalHandoffStatus);
        Assert.True(await db.Context.MOAForms.AnyAsync(f => f.JobRequestId == job.JobRequestId));
    }

    [Fact]
    public async Task SecretarialAssignment_AfterSignOff_MovesToPendingPrep()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer, handoff: JobHandoffStatuses.AwaitingSecAssignment);
        var moi = db.SeedMoi(job, workflowState: MoiWorkflowStates.Approved);

        await JobHandoffService.OnSecretarialTeamAssignedAsync(db.Context, job);

        Assert.Equal(JobHandoffStatuses.PendingPrep, job.InternalHandoffStatus);
        Assert.Equal(MoiWorkflowStates.PendingPrep, moi.WorkflowState);
    }

    [Fact]
    public async Task SecretarialStaffAssignedToUnit_ProvisionsMoa_AndPendingPrep()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer, handoff: JobHandoffStatuses.AwaitingSecAssignment);
        var moi = db.SeedMoi(job, workflowState: MoiWorkflowStates.Approved);
        var unit = job.Units.First();

        await JobHandoffService.OnSecretarialStaffAssignedToUnitAsync(db.Context, job, unit, moi);

        Assert.Equal(MoiWorkflowStates.PendingPrep, moi.WorkflowState);
        Assert.Equal(JobHandoffStatuses.PendingPrep, job.InternalHandoffStatus);
        Assert.True(await db.Context.MOAForms.AnyAsync(f => f.JobRequestId == job.JobRequestId));
    }

    [Fact]
    public void SecretarialAssignment_Blocked_BeforeSharonSignOff()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer, handoff: JobHandoffStatuses.ClientSubmitted);
        var moi = db.SeedMoi(job, workflowState: MoiWorkflowStates.PendingAdminIntake);

        Assert.False(SecretarialStaffService.IsReadyForSecretarialAssignment(job, moi));
    }

    [Fact]
    public void SecretarialAssignment_Allowed_AfterSharonSignOff()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer, handoff: JobHandoffStatuses.AwaitingSecAssignment);
        var moi = db.SeedMoi(job, workflowState: MoiWorkflowStates.Approved);

        Assert.True(SecretarialStaffService.IsReadyForSecretarialAssignment(job, moi));
    }

    [Fact]
    public async Task Recommend_DoesNotAutoApprove_OrProvisionMoaEarly()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer, handoff: JobHandoffStatuses.PendingPrep);
        var moi = db.SeedMoi(job, workflowState: MoiWorkflowStates.PendingPrep);

        await JobHandoffService.OnMoiRecommendedAsync(db.Context, moi);

        Assert.Equal(MoiWorkflowStates.PendingRecommendation, moi.WorkflowState);
        Assert.Equal(JobHandoffStatuses.ResoInProgress, job.InternalHandoffStatus);
        Assert.False(db.Context.MOAForms.Any(f => f.JobRequestId == job.JobRequestId));
    }

    [Fact]
    public async Task FinalMoaSignOff_EntersExecuting_NotCompleted()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer, handoff: JobHandoffStatuses.MoaCirculation);
        job.Status = "In Progress";
        var moi = db.SeedMoi(job, workflowState: MoiWorkflowStates.PendingPrep);
        var moa = db.SeedMoa(job, moi);
        moa.ClientApprovalsJson = JsonHelper.Serialize(new[]
        {
            new ClientApprovalRecord { AccountHolderName = "Carol", SignedAt = DateTime.UtcNow },
        });
        db.Context.SaveChanges();

        await JobHandoffService.OnClientMoaApprovalRecordedAsync(db.Context, job, moa, customer);

        Assert.Equal("In Progress", job.Status);
        Assert.Equal(JobHandoffStatuses.PendingExecute, job.InternalHandoffStatus);
        Assert.NotEqual("Completed", job.Status);
        Assert.False(db.Context.CompletedServices.Any());
    }

    [Fact]
    public async Task ExecutionComplete_MarksJobCompleted_AndRecordsService()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer, handoff: JobHandoffStatuses.PendingExecute);
        job.Status = "In Progress";
        var unit = job.Units.First();
        unit.InternalHandoffStatus = JobHandoffStatuses.PendingExecute;
        unit.Status = "In Progress";
        var moa = db.SeedMoa(job);
        db.Context.SaveChanges();

        await JobHandoffService.OnExecutionCompletedAsync(db.Context, job, unit, moa);

        Assert.Equal("Completed", job.Status);
        Assert.Equal(JobHandoffStatuses.Completed, job.InternalHandoffStatus);
        Assert.True(db.Context.CompletedServices.Any());
    }

    [Fact]
    public async Task PartialMoaSignOff_EntersCirculation_NotCompleted()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer(moaHolder: "Carol");
        customer.MoiApprovalJson = JsonHelper.Serialize(new[] { "Bob", "Dave" });
        customer.AccountHolders.Add(new AccountHolder { Name = "Dave", Email = "dave@test.local", NeedsMoa = true });
        customer.MoaJson = JsonHelper.Serialize(new[] { "Carol", "Dave" });
        db.Context.SaveChanges();

        var job = db.SeedServiceJob(customer, handoff: JobHandoffStatuses.ReadyForMoa);
        job.Status = "In Progress";
        var moa = db.SeedMoa(job);
        moa.ClientApprovalsJson = JsonHelper.Serialize(new[]
        {
            new ClientApprovalRecord { AccountHolderName = "Carol", SignedAt = DateTime.UtcNow },
        });
        db.Context.SaveChanges();

        await JobHandoffService.OnClientMoaApprovalRecordedAsync(db.Context, job, moa, customer);

        Assert.Equal(JobHandoffStatuses.MoaCirculation, job.InternalHandoffStatus);
        Assert.NotEqual("Completed", job.Status);
    }

    [Fact]
    public async Task MoiClientReject_SetsRejectedState()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer);
        var moi = db.SeedMoi(job, workflowState: MoiWorkflowStates.PendingClientMoiApproval);
        var user = new User { Email = "bob@test.local", Name = "Bob", PasswordHash = "x", Role = "ClientSignatory" };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        await JobHandoffService.OnMoiClientRejectedAsync(db.Context, job, moi, user, "Needs correction");

        Assert.Equal(MoiWorkflowStates.MoiRejected, moi.WorkflowState);
    }

    [Fact]
    public async Task MultiSession_SharonSignOff_OnlyAffectsTargetUnit()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer, totalQty: 2);
        job.InternalHandoffStatus = JobHandoffStatuses.ClientSubmitted;
        var unit1 = job.Units.First(u => u.UnitNumber == 1);
        var unit2 = job.Units.First(u => u.UnitNumber == 2);
        unit1.InternalHandoffStatus = JobHandoffStatuses.ClientSubmitted;
        var moi1 = db.SeedMoi(job, unit1, MoiWorkflowStates.PendingAdminIntake);

        await JobHandoffService.OnMoiSharonSignOffAsync(db.Context, job, moi1, unitNumber: 1);

        Assert.Equal(JobHandoffStatuses.PendingPrep, unit1.InternalHandoffStatus);
        Assert.True(string.IsNullOrWhiteSpace(unit2.InternalHandoffStatus));
    }
}
