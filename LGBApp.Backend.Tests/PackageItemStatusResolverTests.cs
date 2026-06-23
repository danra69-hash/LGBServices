using LGBApp.Backend.Models;
using LGBApp.Backend.Services;

namespace LGBApp.Backend.Tests;

public class PackageItemStatusResolverTests
{
    [Theory]
    [InlineData(JobHandoffStatuses.ClientSubmitted, MoiWorkflowStates.PendingAdminIntake, PackageItemStatuses.AwaitingIntake)]
    [InlineData(JobHandoffStatuses.AwaitingSecAssignment, MoiWorkflowStates.Approved, PackageItemStatuses.ResolutionPrep)]
    [InlineData(JobHandoffStatuses.PendingPrep, MoiWorkflowStates.PendingPrep, PackageItemStatuses.ResolutionPrep)]
    [InlineData(JobHandoffStatuses.ResoInProgress, MoiWorkflowStates.PendingRecommendation, PackageItemStatuses.PendingRecommendation)]
    [InlineData(JobHandoffStatuses.AdminReview, MoiWorkflowStates.PendingPrep, PackageItemStatuses.ResolutionPrep)]
    [InlineData(JobHandoffStatuses.AdminReview, MoiWorkflowStates.Approved, PackageItemStatuses.MoiApproved)]
    [InlineData(JobHandoffStatuses.ReadyForMoa, MoiWorkflowStates.Approved, PackageItemStatuses.ReadyForMoa)]
    [InlineData(JobHandoffStatuses.MoaCirculation, MoiWorkflowStates.Approved, PackageItemStatuses.MoaCirculation)]
    [InlineData(JobHandoffStatuses.PendingExecute, MoiWorkflowStates.Approved, PackageItemStatuses.Execution)]
    [InlineData(JobHandoffStatuses.Completed, MoiWorkflowStates.Approved, PackageItemStatuses.Completed)]
    public void ResolveForUnit_MapsHandoffAndMoiState(
        string handoff,
        string moiState,
        string expectedKey)
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer, handoff: handoff);
        job.Status = handoff == JobHandoffStatuses.Completed ? "Completed" : "In Progress";
        var unit = job.Units.First();
        unit.InternalHandoffStatus = handoff;
        if (handoff == JobHandoffStatuses.Completed)
            unit.Status = "Completed";
        var moi = db.SeedMoi(job, unit, moiState);

        var result = PackageItemStatusResolver.ResolveForUnit(job, unit, moi);

        Assert.Equal(expectedKey, result.Key);
    }

    [Fact]
    public void ResolveForUnit_NoMoi_NoHandoff_IsMoiNotReceived()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer);
        var unit = job.Units.First();

        var result = PackageItemStatusResolver.ResolveForUnit(job, unit);

        Assert.Equal(PackageItemStatuses.MoiNotReceived, result.Key);
    }

    [Fact]
    public void ResolveForUnit_MoiRejected_ShowsRejected()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer);
        var unit = job.Units.First();
        var moi = db.SeedMoi(job, unit, MoiWorkflowStates.MoiRejected);

        var result = PackageItemStatusResolver.ResolveForUnit(job, unit, moi);

        Assert.Equal(PackageItemStatuses.MoiRejected, result.Key);
    }

    [Fact]
    public void Resolve_MoaTask_AdminReview_WithApprovedPairedMoi_IsMoiSignOff()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = new JobRequest
        {
            CustomerId = customer.CustomerId,
            Customer = customer.Company,
            TaskType = "MOA",
            Service = "MOA",
            Status = "In Progress",
            InternalHandoffStatus = JobHandoffStatuses.AdminReview,
            TotalQty = 1,
            DateRequested = DateTime.UtcNow,
        };
        db.Context.JobRequests.Add(job);
        db.Context.SaveChanges();
        var pairedMoi = db.SeedMoi(job, workflowState: MoiWorkflowStates.Approved);

        var result = PackageItemStatusResolver.Resolve(job, pairedMoiForm: pairedMoi);

        Assert.Equal(PackageItemStatuses.MoiSignOff, result.Key);
    }
}
