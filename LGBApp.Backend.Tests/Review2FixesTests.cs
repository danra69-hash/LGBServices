using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LGBApp.Backend.Tests;

public class Review2FixesTests
{
    [Fact]
    public void N2_ExpectedUpdatedAt_NoOneSecondTolerance()
    {
        var stored = new DateTime(2026, 7, 14, 10, 0, 0, DateTimeKind.Utc);
        var almost = stored.AddMilliseconds(500).ToString("O");
        var conflict = FormConcurrencyHelper.CheckExpectedUpdatedAt(stored, almost);
        Assert.IsType<ConflictObjectResult>(conflict);

        var exact = stored.ToString("O");
        Assert.Null(FormConcurrencyHelper.CheckExpectedUpdatedAt(stored, exact));
    }

    [Fact]
    public async Task N2_ParallelSave_OneWinsOneConflicts()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lgb-n2-{Guid.NewGuid():N}.db");
        try
        {
            await using (var setup = CreateContext(path))
            {
                await setup.Database.EnsureCreatedAsync();
                setup.MOAForms.Add(new MOAForm
                {
                    Company = "Acme",
                    FormDataJson = "{}",
                    PackChecklistJson = "{}",
                    ConcurrencyStamp = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    UpdatedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                });
                await setup.SaveChangesAsync();
            }

            await using var ctx1 = CreateContext(path);
            await using var ctx2 = CreateContext(path);
            var form1 = await ctx1.MOAForms.SingleAsync();
            var form2 = await ctx2.MOAForms.SingleAsync();
            Assert.Equal(form1.ConcurrencyStamp, form2.ConcurrencyStamp);

            form1.UpdatedAt = DateTime.UtcNow.AddSeconds(1);
            form1.ShareMovement = true;
            var conflict1 = await FormConcurrencyHelper.SaveWithConcurrencyAsync(ctx1, form1.UpdatedAt);
            Assert.Null(conflict1);

            form2.UpdatedAt = DateTime.UtcNow.AddSeconds(2);
            form2.FinanceRelated = true;
            var conflict2 = await FormConcurrencyHelper.SaveWithConcurrencyAsync(ctx2, form2.UpdatedAt);
            Assert.IsType<ConflictObjectResult>(conflict2);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task N1_RevertExecutedUnit_RollsHandoffToPendingExecute()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lgb-n1-{Guid.NewGuid():N}.db");
        try
        {
            await using var context = CreateContext(path);
            await context.Database.EnsureCreatedAsync();

            var job = new JobRequest
            {
                Customer = "Acme",
                Service = "Secretarial",
                TaskType = "Service",
                TotalQty = 1,
                UsedQty = 1,
                Status = "Completed",
                InternalHandoffStatus = JobHandoffStatuses.Completed,
                DateRequested = DateTime.UtcNow,
            };
            var unit = new JobRequestUnit
            {
                UnitNumber = 1,
                Status = "Completed",
                CompletedAt = DateTime.UtcNow,
                InternalHandoffStatus = JobHandoffStatuses.Completed,
            };
            job.Units.Add(unit);
            context.JobRequests.Add(job);
            await context.SaveChangesAsync();

            await JobRequestUnitService.RevertUnitCompleteAsync(context, unit, job);
            await context.SaveChangesAsync();

            // No assignees → Pending; handoff must leave Completed
            Assert.Equal("Pending", unit.Status);
            Assert.Equal(JobHandoffStatuses.PendingExecute, unit.InternalHandoffStatus);
            Assert.Equal(JobHandoffStatuses.PendingExecute, job.InternalHandoffStatus);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task N3_RemoveLastAssigneeMidPrep_ReturnsToAwaitingSecAssignment()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lgb-n3-{Guid.NewGuid():N}.db");
        try
        {
            await using var context = CreateContext(path);
            await context.Database.EnsureCreatedAsync();

            var user = new User
            {
                Email = "nita@test.local",
                Name = "Nita",
                PasswordHash = "x",
                Role = "User",
                CreatedAt = DateTime.UtcNow,
            };
            context.Users.Add(user);

            var job = new JobRequest
            {
                Customer = "Acme",
                Service = "Secretarial",
                TaskType = "Service",
                TotalQty = 1,
                UsedQty = 0,
                Status = "In Progress",
                InternalHandoffStatus = JobHandoffStatuses.PendingPrep,
                DateRequested = DateTime.UtcNow,
            };
            var unit = new JobRequestUnit
            {
                UnitNumber = 1,
                Status = "In Progress",
                InternalHandoffStatus = JobHandoffStatuses.PendingPrep,
            };
            job.Units.Add(unit);
            context.JobRequests.Add(job);
            await context.SaveChangesAsync();

            await JobRequestUnitService.AddAssigneeAsync(context, unit, user);
            await context.SaveChangesAsync();

            await JobRequestUnitService.RemoveAssigneeAsync(context, unit, user.UserId, job);
            await context.SaveChangesAsync();

            Assert.Equal("Pending", unit.Status);
            Assert.Equal(JobHandoffStatuses.AwaitingSecAssignment, unit.InternalHandoffStatus);
            Assert.Equal(JobHandoffStatuses.AwaitingSecAssignment, job.InternalHandoffStatus);
            Assert.Null(unit.AssignedUserId);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static AppDbContext CreateContext(string path)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        return new AppDbContext(options);
    }
}
