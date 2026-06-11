using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public static class MoiFormService
{
    public static async Task<MOIForm?> FindForUnitAsync(
        AppDbContext context,
        int jobId,
        int? jobRequestUnitId,
        bool singleUnitJob)
    {
        if (jobRequestUnitId.HasValue)
        {
            var byUnit = await context.MOIForms.FirstOrDefaultAsync(f =>
                f.JobRequestId == jobId && f.JobRequestUnitId == jobRequestUnitId);
            if (byUnit != null)
                return byUnit;
        }

        if (singleUnitJob)
        {
            return await context.MOIForms.FirstOrDefaultAsync(f =>
                f.JobRequestId == jobId
                && (f.JobRequestUnitId == null || f.JobRequestUnitId == jobRequestUnitId));
        }

        return null;
    }

    public static async Task<MOIForm> EnsureMoiForUnitAsync(
        AppDbContext context,
        JobRequest job,
        JobRequestUnit unit)
    {
        var singleUnit = job.TotalQty <= 1;
        var existing = await FindForUnitAsync(context, job.JobRequestId, unit.JobRequestUnitId, singleUnit);
        if (existing != null)
            return existing;

        Customer? customer = null;
        if (job.CustomerId.HasValue)
            customer = await context.Customers.FindAsync(job.CustomerId.Value);

        DivisionGroup? group = null;
        if (customer != null && !string.IsNullOrWhiteSpace(customer.DivisionGroupCode))
            group = await context.DivisionGroups.FirstOrDefaultAsync(g => g.Code == customer.DivisionGroupCode);

        var templateCode = await WorkflowService.ResolveMoiTemplateCodeAsync(
            context, customer, group, job.Service);
        var now = DateTime.UtcNow;
        var sessionLabel = job.TotalQty > 1 ? $" (session {unit.UnitNumber})" : string.Empty;

        var form = new MOIForm
        {
            JobRequestId = job.JobRequestId,
            JobRequestUnitId = job.TotalQty > 1 ? unit.JobRequestUnitId : unit.JobRequestUnitId,
            Company = job.Customer,
            FormTemplateCode = templateCode,
            WorkflowState = MoiWorkflowStates.Draft,
            FormDataJson = JsonHelper.Serialize(new Dictionary<string, object?>
            {
                ["company"] = job.Customer,
                ["taskType"] = job.TaskType,
                ["service"] = job.Service,
                ["accountHolder"] = job.AccountHolder,
                ["requestedBy"] = job.AccountHolder,
                ["unitNumber"] = unit.UnitNumber,
                ["sessionLabel"] = sessionLabel.Trim(),
            }),
            CreatedAt = now,
            UpdatedAt = now,
        };

        context.MOIForms.Add(form);
        await context.SaveChangesAsync();
        return form;
    }

    public static async Task<List<MOIForm>> ListForJobAsync(AppDbContext context, int jobId) =>
        await context.MOIForms
            .Where(f => f.JobRequestId == jobId)
            .OrderBy(f => f.JobRequestUnitId)
            .ThenBy(f => f.MOIFormId)
            .ToListAsync();

    public static JobRequestUnit? ResolveUnit(JobRequest job, int? unitNumber)
    {
        if (job.Units.Count == 0)
            return null;

        if (unitNumber.HasValue)
            return job.Units.FirstOrDefault(u => u.UnitNumber == unitNumber.Value);

        return job.TotalQty == 1
            ? job.Units.OrderBy(u => u.UnitNumber).FirstOrDefault()
            : null;
    }

    public static int? ResolveUnitNumber(FormRequest request)
    {
        if (request.UnitNumber.HasValue)
            return request.UnitNumber;

        if (request.Data.TryGetValue("unitNumber", out var raw) && raw != null)
        {
            if (raw is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Number)
                return je.GetInt32();
            if (int.TryParse(raw.ToString(), out var parsed))
                return parsed;
        }

        return null;
    }

    public static void StampUnitMetadata(
        Dictionary<string, object?> formData,
        JobRequest job,
        JobRequestUnit? unit)
    {
        if (unit == null)
            return;

        formData["unitNumber"] = unit.UnitNumber;
        formData["sessionLabel"] = job.TotalQty > 1 ? $"session {unit.UnitNumber}" : string.Empty;
        formData["service"] = formData.GetValueOrDefault("service") ?? job.Service;
        formData["typeOfDocument"] = formData.GetValueOrDefault("typeOfDocument") ?? job.Service;
    }
}
