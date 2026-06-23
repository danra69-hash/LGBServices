using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Tests;

public sealed class TestDbFactory : IDisposable
{
    private readonly SqliteConnection _connection;

    public AppDbContext Context { get; }

    public TestDbFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();
    }

    public Customer SeedCustomer(
        string company = "Test Co",
        string moiHolder = "Alice",
        string moiApprover = "Bob",
        string moaHolder = "Carol")
    {
        var customer = new Customer
        {
            Name = moiHolder,
            Email = "alice@test.local",
            Company = company,
            Status = "Active",
            Cosec = true,
            MoiJson = JsonHelper.Serialize(new[] { moiHolder }),
            MoiApprovalJson = JsonHelper.Serialize(new[] { moiApprover }),
            MoaJson = JsonHelper.Serialize(new[] { moaHolder }),
            AccountHolders =
            [
                new AccountHolder { Name = moiHolder, Email = "alice@test.local", NeedsMoi = true },
                new AccountHolder { Name = moiApprover, Email = "bob@test.local", NeedsMoiApproval = true },
                new AccountHolder { Name = moaHolder, Email = "carol@test.local", NeedsMoa = true },
            ],
        };
        Context.Customers.Add(customer);
        Context.SaveChanges();
        return customer;
    }

    public JobRequest SeedServiceJob(Customer customer, int totalQty = 1, string handoff = "")
    {
        var job = new JobRequest
        {
            CustomerId = customer.CustomerId,
            Customer = customer.Company,
            TaskType = "Service",
            Service = "Secretarial record Checks",
            TotalQty = totalQty,
            UsedQty = 0,
            Status = "Pending",
            InternalHandoffStatus = handoff,
            DateRequested = DateTime.UtcNow,
            AccountHolder = customer.AccountHolders.First().Name,
        };
        Context.JobRequests.Add(job);
        Context.SaveChanges();

        for (var i = 1; i <= totalQty; i++)
        {
            Context.JobRequestUnits.Add(new JobRequestUnit
            {
                JobRequestId = job.JobRequestId,
                UnitNumber = i,
                Status = "Pending",
                InternalHandoffStatus = totalQty > 1 ? handoff : string.Empty,
            });
        }
        Context.SaveChanges();
        Context.Entry(job).Collection(j => j.Units).Load();
        return job;
    }

    public MOIForm SeedMoi(JobRequest job, JobRequestUnit? unit = null, string workflowState = MoiWorkflowStates.Draft)
    {
        var form = new MOIForm
        {
            JobRequestId = job.JobRequestId,
            JobRequestUnitId = unit?.JobRequestUnitId,
            Company = job.Customer,
            WorkflowState = workflowState,
            FormDataJson = "{}",
        };
        Context.MOIForms.Add(form);
        Context.SaveChanges();
        return form;
    }

    public MOAForm SeedMoa(JobRequest job, MOIForm? moi = null, JobRequestUnit? unit = null)
    {
        var form = new MOAForm
        {
            JobRequestId = job.JobRequestId,
            JobRequestUnitId = unit?.JobRequestUnitId,
            MOIFormId = moi?.MOIFormId,
            Company = job.Customer,
            PackChecklistJson = JsonHelper.Serialize(new MoaPackChecklist
            {
                InternalChecklistA = true,
                InternalChecklistB = true,
                CleanAgreementAttached = true,
                SsmRegistrationNo = "123",
                SsmEntityType = "Sdn Bhd",
                SsmStatus = "Active",
                SsmAsAtDate = "2026-01-01",
            }),
        };
        Context.MOAForms.Add(form);
        Context.SaveChanges();
        return form;
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
