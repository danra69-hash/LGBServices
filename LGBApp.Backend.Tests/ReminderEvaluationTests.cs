using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Services;
using LGBApp.Backend.Services.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace LGBApp.Backend.Tests;

public class ReminderEvaluationTests
{
    private sealed class FakeClock : IAppClock
    {
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    }

    private sealed class NoopEmail : IEmailSender
    {
        public Task SendAsync(
            string to,
            string subject,
            string textBody,
            string? htmlBody = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private static ReminderEvaluationService CreateEvaluator(
        TestDbFactory db,
        FakeClock clock,
        IConfiguration? config = null)
    {
        config ??= new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Reminders:SendEmail"] = "false",
            })
            .Build();

        var notifier = new WorkflowNotifier(
            db.Context,
            new NoopEmail(),
            config,
            NullLogger<WorkflowNotifier>.Instance);

        return new ReminderEvaluationService(
            db.Context,
            clock,
            notifier,
            config,
            NullLogger<ReminderEvaluationService>.Instance);
    }

    [Fact]
    public async Task MoiHod_SendsExactlyTwo_At24hAnd48h_NeverMore()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer);
        var form = db.SeedMoi(job, workflowState: MoiWorkflowStates.PendingClientMoiApproval);
        var t0 = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        form.ClientApprovalRequestedAt = t0;
        form.UpdatedAt = t0;
        await db.Context.SaveChangesAsync();

        var clock = new FakeClock { UtcNow = t0 };
        var svc = CreateEvaluator(db, clock);

        Assert.Equal(0, await svc.ProcessDueAsync());

        clock.UtcNow = t0.AddHours(24);
        Assert.Equal(1, await svc.ProcessDueAsync());
        Assert.Equal(0, await svc.ProcessDueAsync()); // already at target for this hour

        var log = db.Context.ReminderLogs.Single(r => r.Kind == ReminderKinds.MoiHodReminder);
        Assert.Equal(1, log.SentCount);

        clock.UtcNow = t0.AddHours(48);
        var n48 = await svc.ProcessDueAsync();
        Assert.True(n48 >= 1); // second HOD (+ requester prompt may also fire)
        await svc.ProcessDueAsync();
        log = db.Context.ReminderLogs.Single(r => r.Kind == ReminderKinds.MoiHodReminder);
        Assert.Equal(2, log.SentCount);

        clock.UtcNow = t0.AddHours(96);
        await svc.ProcessDueAsync();
        log = db.Context.ReminderLogs.Single(r => r.Kind == ReminderKinds.MoiHodReminder);
        Assert.Equal(2, log.SentCount);
    }

    [Fact]
    public async Task MoiRequester_PromptOnce_At48h()
    {
        using var db = new TestDbFactory();
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer);
        var form = db.SeedMoi(job, workflowState: MoiWorkflowStates.PendingClientMoiApproval);
        var t0 = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        form.ClientApprovalRequestedAt = t0;
        await db.Context.SaveChangesAsync();

        var clock = new FakeClock { UtcNow = t0.AddHours(47) };
        var svc = CreateEvaluator(db, clock);
        await svc.ProcessDueAsync();
        Assert.False(db.Context.ReminderLogs.Any(r => r.Kind == ReminderKinds.MoiRequesterPrompt));

        clock.UtcNow = t0.AddHours(48);
        await svc.ProcessDueAsync();
        var log = db.Context.ReminderLogs.Single(r => r.Kind == ReminderKinds.MoiRequesterPrompt);
        Assert.Equal(1, log.SentCount);

        clock.UtcNow = t0.AddHours(200);
        await svc.ProcessDueAsync();
        Assert.Equal(1, db.Context.ReminderLogs.Single(r => r.Kind == ReminderKinds.MoiRequesterPrompt).SentCount);
    }

    [Fact]
    public async Task MoaApprover_CapsAtThree_Every48h()
    {
        using var db = new TestDbFactory();
        WorkflowConfigSeeder.SeedReferenceData(db.Context);
        var customer = db.SeedCustomer();
        var job = db.SeedServiceJob(customer);
        var moi = db.SeedMoi(job, workflowState: MoiWorkflowStates.Approved);
        var moa = db.SeedMoa(job, moi);
        await WorkflowService.InitializeMoaWorkflowAsync(db.Context, moa, customer);

        var step = db.Context.WorkflowStepInstances.First(s => s.Status == "Active");
        var t0 = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc);
        step.ActivatedAt = t0;
        await db.Context.SaveChangesAsync();

        var clock = new FakeClock { UtcNow = t0 };
        var svc = CreateEvaluator(db, clock);

        clock.UtcNow = t0.AddHours(48);
        await svc.ProcessDueAsync();
        clock.UtcNow = t0.AddHours(96);
        await svc.ProcessDueAsync();
        clock.UtcNow = t0.AddHours(144);
        await svc.ProcessDueAsync();
        clock.UtcNow = t0.AddHours(200);
        await svc.ProcessDueAsync();

        var log = db.Context.ReminderLogs.Single(r => r.Kind == ReminderKinds.MoaApproverReminder);
        Assert.Equal(3, log.SentCount);

        var cosec = db.Context.ReminderLogs.Single(r => r.Kind == ReminderKinds.MoaCosecPrompt);
        Assert.Equal(1, cosec.SentCount);
    }
}
