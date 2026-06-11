using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public static class JobFormProvisioner
{
    private static readonly string[] FormTaskTypes = ["MOI", "MOI Approval", "MOA"];

    public static Task EnsureMoaFormAsync(AppDbContext context, JobRequest job) =>
        EnsureMoaFormCoreAsync(context, job);

    public static async Task EnsureMoaFormForMoiAsync(AppDbContext context, JobRequest job, MOIForm moiForm)
    {
        var exists = await context.MOAForms.AnyAsync(f =>
            f.MOIFormId == moiForm.MOIFormId
            || (f.JobRequestId == job.JobRequestId
                && f.JobRequestUnitId == moiForm.JobRequestUnitId
                && moiForm.JobRequestUnitId != null));
        if (exists)
            return;

        Customer? customer = job.CustomerId.HasValue
            ? await context.Customers.FindAsync(job.CustomerId.Value)
            : null;
        DivisionGroup? group = null;
        if (customer != null && !string.IsNullOrWhiteSpace(customer.DivisionGroupCode))
            group = await context.DivisionGroups.FirstOrDefaultAsync(g => g.Code == customer.DivisionGroupCode);

        var templateCode = WorkflowService.ResolveMoaTemplateCode(customer, group);
        var now = DateTime.UtcNow;
        var sessionLabel = job.TotalQty > 1 && moiForm.JobRequestUnitId.HasValue
            ? await context.JobRequestUnits
                .Where(u => u.JobRequestUnitId == moiForm.JobRequestUnitId)
                .Select(u => u.UnitNumber)
                .FirstOrDefaultAsync()
            : 0;

        context.MOAForms.Add(new MOAForm
        {
            JobRequestId = job.JobRequestId,
            JobRequestUnitId = moiForm.JobRequestUnitId,
            MOIFormId = moiForm.MOIFormId,
            Company = job.Customer,
            FormTemplateCode = templateCode,
            FinanceRelated = moiForm.FinanceRelated,
            BankSignatoryMatter = moiForm.BankSignatoryMatter,
            FormDataJson = JsonHelper.Serialize(new Dictionary<string, object?>
            {
                ["company"] = job.Customer,
                ["taskType"] = job.TaskType,
                ["service"] = job.Service,
                ["accountHolder"] = job.AccountHolder,
                ["projectInitiator"] = job.AccountHolder,
                ["unitNumber"] = sessionLabel > 0 ? sessionLabel : null,
                ["sessionLabel"] = sessionLabel > 0 ? $"session {sessionLabel}" : string.Empty,
                ["moiFormId"] = moiForm.MOIFormId,
            }),
            CreatedAt = now,
            UpdatedAt = now,
        });
        await context.SaveChangesAsync();
    }

    public static async Task EnsureFormForJobAsync(AppDbContext context, JobRequest job)
    {
        if (job.JobRequestId == 0)
            return;

        if (FormTaskTypes.Contains(job.TaskType, StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(job.TaskType, "MOA", StringComparison.OrdinalIgnoreCase))
                await EnsureMoaFormCoreAsync(context, job);
            else if (!string.Equals(job.TaskType, "MOI Approval", StringComparison.OrdinalIgnoreCase))
                await EnsureMoiFormAsync(context, job);
            return;
        }

        if (string.Equals(job.TaskType, "Service", StringComparison.OrdinalIgnoreCase)
            && job.CustomerPackageId.HasValue)
        {
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
        if (job.TotalQty > 1)
            return;

        var exists = await context.MOIForms.AnyAsync(f => f.JobRequestId == job.JobRequestId);
        if (exists) return;

        await JobRequestUnitService.SyncUnitsForJobAsync(context, job);
        var unit = job.Units.OrderBy(u => u.UnitNumber).FirstOrDefault();

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
            JobRequestUnitId = unit?.JobRequestUnitId,
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

    private static async Task EnsureMoaFormCoreAsync(AppDbContext context, JobRequest job)
    {
        var exists = await context.MOAForms.AnyAsync(f => f.JobRequestId == job.JobRequestId);
        if (exists) return;

        var moiForm = await context.MOIForms
            .FirstOrDefaultAsync(f => f.JobRequestId == job.JobRequestId);

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
