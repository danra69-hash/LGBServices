using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

/// <summary>
/// Repairs per-session MOI/MOA invariants so multi-qty package lines (e.g. 2× Prepare board meeting Minutes)
/// never share one form across sessions.
/// </summary>
public static class JobWorkflowIntegrityService
{
    public static async Task RepairAllAsync(AppDbContext context)
    {
        var jobs = await context.JobRequests
            .Include(j => j.Units)
            .Where(j => j.TaskType == "Service" && j.TotalQty > 1)
            .ToListAsync();

        foreach (var job in jobs)
            await RepairJobAsync(context, job);

        await context.SaveChangesAsync();
    }

    public static async Task RepairJobAsync(AppDbContext context, JobRequest job)
    {
        await JobRequestUnitService.SyncUnitsForJobAsync(context, job);
        await context.Entry(job).Collection(j => j.Units).LoadAsync();

        var units = job.Units.OrderBy(u => u.UnitNumber).ToList();
        if (units.Count == 0)
            return;

        await RepairOrphanMoisAsync(context, job, units);
        await RepairOrphanMoasAsync(context, job, units);
        NormalizeJobLevelHandoff(job, units);
    }

    private static async Task RepairOrphanMoisAsync(
        AppDbContext context,
        JobRequest job,
        List<JobRequestUnit> units)
    {
        var orphans = await context.MOIForms
            .Where(f => f.JobRequestId == job.JobRequestId && f.JobRequestUnitId == null)
            .OrderBy(f => f.MOIFormId)
            .ToListAsync();

        if (orphans.Count == 0)
            return;

        var unitsWithoutMoi = new List<JobRequestUnit>();
        foreach (var unit in units)
        {
            var hasUnitMoi = await context.MOIForms.AnyAsync(f =>
                f.JobRequestId == job.JobRequestId && f.JobRequestUnitId == unit.JobRequestUnitId);
            if (!hasUnitMoi)
                unitsWithoutMoi.Add(unit);
        }

        foreach (var orphan in orphans)
        {
            if (unitsWithoutMoi.Count == 0)
            {
                if (IsDisposableDraft(orphan))
                    context.MOIForms.Remove(orphan);
                continue;
            }

            var target = unitsWithoutMoi[0];
            unitsWithoutMoi.RemoveAt(0);
            AttachMoiToUnit(orphan, target);
            MigrateUnitHandoffFromJob(job, target, orphan);
        }

        if (orphans.Count == 1 && unitsWithoutMoi.Count > 0)
        {
            foreach (var target in unitsWithoutMoi.ToList())
                await CloneMoiForUnitAsync(context, job, orphans[0], target);
        }
    }

    private static async Task CloneMoiForUnitAsync(
        AppDbContext context,
        JobRequest job,
        MOIForm source,
        JobRequestUnit target)
    {
        var clone = new MOIForm
        {
            JobRequestId = job.JobRequestId,
            JobRequestUnitId = target.JobRequestUnitId,
            Company = source.Company,
            FormTemplateCode = source.FormTemplateCode,
            WorkflowState = MoiWorkflowStates.Draft,
            FinanceRelated = source.FinanceRelated,
            BankSignatoryMatter = source.BankSignatoryMatter,
            ClientApprovalsJson = "[]",
            RejectionsJson = "[]",
            FormDataJson = "{}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        AttachMoiToUnit(clone, target);
        var data = JsonHelper.Deserialize<Dictionary<string, object?>>(source.FormDataJson);
        data["company"] = source.Company;
        data["service"] = job.Service;
        clone.FormDataJson = JsonHelper.Serialize(data);
        context.MOIForms.Add(clone);
        await context.SaveChangesAsync();
    }

    private static async Task RepairOrphanMoasAsync(
        AppDbContext context,
        JobRequest job,
        List<JobRequestUnit> units)
    {
        var orphans = await context.MOAForms
            .Where(f => f.JobRequestId == job.JobRequestId && f.JobRequestUnitId == null)
            .OrderBy(f => f.MOAFormId)
            .ToListAsync();

        if (orphans.Count == 0)
            return;

        var moaUnitIds = await context.MOAForms
            .Where(f => f.JobRequestId == job.JobRequestId && f.JobRequestUnitId != null)
            .Select(f => f.JobRequestUnitId!.Value)
            .ToListAsync();

        var unitsWithoutMoa = units
            .Where(u => !moaUnitIds.Contains(u.JobRequestUnitId))
            .ToList();

        foreach (var orphan in orphans)
        {
            if (unitsWithoutMoa.Count == 0)
            {
                if (orphan.ClientApprovalsJson == "[]")
                    context.MOAForms.Remove(orphan);
                continue;
            }

            var target = unitsWithoutMoa[0];
            unitsWithoutMoa.RemoveAt(0);
            orphan.JobRequestUnitId = target.JobRequestUnitId;
            moaUnitIds.Add(target.JobRequestUnitId);
        }
    }

    private static void AttachMoiToUnit(MOIForm form, JobRequestUnit unit)
    {
        form.JobRequestUnitId = unit.JobRequestUnitId;
        var data = JsonHelper.Deserialize<Dictionary<string, object?>>(form.FormDataJson);
        data["unitNumber"] = unit.UnitNumber;
        data["sessionLabel"] = $"session {unit.UnitNumber}";
        form.FormDataJson = JsonHelper.Serialize(data);
        form.UpdatedAt = DateTime.UtcNow;
    }

    private static void MigrateUnitHandoffFromJob(JobRequest job, JobRequestUnit unit, MOIForm moi)
    {
        if (string.IsNullOrWhiteSpace(unit.InternalHandoffStatus)
            && !string.IsNullOrWhiteSpace(job.InternalHandoffStatus)
            && moi.WorkflowState is MoiWorkflowStates.PendingAdminIntake
                or MoiWorkflowStates.PendingPrep
                or MoiWorkflowStates.PendingRecommendation
                or MoiWorkflowStates.Approved)
        {
            unit.InternalHandoffStatus = job.InternalHandoffStatus;
        }
    }

    private static void NormalizeJobLevelHandoff(JobRequest job, List<JobRequestUnit> units)
    {
        if (units.Count <= 1)
            return;

        // Multi-session jobs use per-unit handoff; job-level status misleads session 2 when session 1 advances.
        if (units.Any(u => !string.IsNullOrWhiteSpace(u.InternalHandoffStatus)))
            job.InternalHandoffStatus = string.Empty;
    }

    private static bool IsDisposableDraft(MOIForm form) =>
        form.WorkflowState == MoiWorkflowStates.Draft
        && (form.ClientApprovalsJson == "[]" || string.IsNullOrWhiteSpace(form.ClientApprovalsJson))
        && (form.FormDataJson == "{}" || form.FormDataJson.Length < 80);
}
