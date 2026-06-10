using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public static class JobFormProvisioner
{
    private static readonly string[] FormTaskTypes = ["MOI", "MOI Approval", "MOA"];

    public static async Task EnsureFormForJobAsync(AppDbContext context, JobRequest job)
    {
        if (job.JobRequestId == 0)
            return;

        if (FormTaskTypes.Contains(job.TaskType, StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(job.TaskType, "MOA", StringComparison.OrdinalIgnoreCase))
                await EnsureMoaFormAsync(context, job);
            else
                await EnsureMoiFormAsync(context, job);
            return;
        }

        await EnsureServiceJobFormAsync(context, job);
    }

    public static async Task EnsureFormsForJobsAsync(AppDbContext context, IEnumerable<JobRequest> jobs)
    {
        foreach (var job in jobs)
            await EnsureFormForJobAsync(context, job);
    }

    private static async Task EnsureMoiFormAsync(AppDbContext context, JobRequest job)
    {
        var exists = await context.MOIForms.AnyAsync(f => f.JobRequestId == job.JobRequestId);
        if (exists) return;

        Customer? customer = null;
        if (job.CustomerId.HasValue)
            customer = await context.Customers.FindAsync(job.CustomerId.Value);

        DivisionGroup? group = null;
        if (customer != null && !string.IsNullOrWhiteSpace(customer.DivisionGroupCode))
            group = await context.DivisionGroups.FirstOrDefaultAsync(g => g.Code == customer.DivisionGroupCode);

        var templateCode = await WorkflowService.ResolveMoiTemplateCodeAsync(
            context, customer, group, job.Service);
        var now = DateTime.UtcNow;

        context.MOIForms.Add(new MOIForm
        {
            JobRequestId = job.JobRequestId,
            Company = job.Customer,
            FormTemplateCode = templateCode,
            WorkflowState = "Draft",
            FormDataJson = JsonHelper.Serialize(new Dictionary<string, object?>
            {
                ["company"] = job.Customer,
                ["taskType"] = job.TaskType,
                ["service"] = job.Service,
                ["accountHolder"] = job.AccountHolder,
                ["requestedBy"] = job.AccountHolder,
            }),
            CreatedAt = now,
            UpdatedAt = now,
        });
        await context.SaveChangesAsync();
    }

    private static async Task EnsureMoaFormAsync(AppDbContext context, JobRequest job)
    {
        var exists = await context.MOAForms.AnyAsync(f => f.JobRequestId == job.JobRequestId);
        if (exists) return;

        MOIForm? moiForm = null;
        if (job.CustomerId.HasValue)
        {
            moiForm = await context.MOIForms
                .Where(f => f.Company == job.Customer && f.WorkflowState == "Approved")
                .OrderByDescending(f => f.UpdatedAt)
                .FirstOrDefaultAsync();
        }

        Customer? customer = job.CustomerId.HasValue
            ? await context.Customers.FindAsync(job.CustomerId.Value)
            : null;
        DivisionGroup? group = null;
        if (customer != null && !string.IsNullOrWhiteSpace(customer.DivisionGroupCode))
            group = await context.DivisionGroups.FirstOrDefaultAsync(g => g.Code == customer.DivisionGroupCode);

        var templateCode = WorkflowService.ResolveMoaTemplateCode(customer, group);
        var now = DateTime.UtcNow;

        var moa = new MOAForm
        {
            JobRequestId = job.JobRequestId,
            MOIFormId = moiForm?.MOIFormId,
            Company = job.Customer,
            FormTemplateCode = templateCode,
            FormDataJson = JsonHelper.Serialize(new Dictionary<string, object?>
            {
                ["company"] = job.Customer,
                ["taskType"] = job.TaskType,
                ["accountHolder"] = job.AccountHolder,
                ["projectInitiator"] = job.AccountHolder,
            }),
            CreatedAt = now,
            UpdatedAt = now,
        };
        context.MOAForms.Add(moa);
        await context.SaveChangesAsync();
    }

    private static async Task EnsureServiceJobFormAsync(AppDbContext context, JobRequest job)
    {
        var exists = await context.ServiceJobForms.AnyAsync(f => f.JobRequestId == job.JobRequestId);
        if (exists) return;

        var now = DateTime.UtcNow;
        context.ServiceJobForms.Add(new ServiceJobForm
        {
            JobRequestId = job.JobRequestId,
            Company = job.Customer,
            Service = job.Service,
            Status = "Draft",
            FormDataJson = JsonHelper.Serialize(new Dictionary<string, object?>
            {
                ["company"] = job.Customer,
                ["service"] = job.Service,
                ["accountHolder"] = job.AccountHolder,
                ["dateRequested"] = job.DateRequested.ToString("yyyy-MM-dd"),
                ["notes"] = string.Empty,
            }),
            CreatedAt = now,
            UpdatedAt = now,
        });
        await context.SaveChangesAsync();
    }
}
