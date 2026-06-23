using System.Security.Claims;
using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public static class JobFormLinkService
{
    public static async Task EnrichWithFormLinksAsync(
        AppDbContext context,
        IEnumerable<JobRequestResponse> jobs,
        ClaimsPrincipal? user = null)
    {
        var list = jobs.ToList();
        if (list.Count == 0) return;

        var ids = list.Select(j => j.Id).ToList();
        var moiForms = await context.MOIForms
            .Where(f => f.JobRequestId != null && ids.Contains(f.JobRequestId.Value))
            .ToListAsync();
        var moaForms = await context.MOAForms
            .Where(f => f.JobRequestId != null && ids.Contains(f.JobRequestId.Value))
            .ToListAsync();
        var serviceForms = await context.ServiceJobForms
            .Where(f => ids.Contains(f.JobRequestId))
            .ToListAsync();

        var pairedMoiJobIds = list
            .Where(j => j.TaskType is "MOI Approval" or "MOA")
            .Select(j => FindPairedMoiJob(list, j))
            .Where(j => j != null)
            .Select(j => j!.Id)
            .Distinct()
            .Where(id => !ids.Contains(id))
            .ToList();

        var approvalCustomerIds = list
            .Where(j => j.TaskType == "MOI Approval" && j.CustomerId.HasValue)
            .Select(j => j.CustomerId!.Value)
            .Distinct()
            .ToList();

        if (approvalCustomerIds.Count > 0)
        {
            var customerMoiJobIds = await context.JobRequests
                .Where(j => j.CustomerId != null
                    && approvalCustomerIds.Contains(j.CustomerId.Value)
                    && j.TaskType == "MOI")
                .Select(j => j.JobRequestId)
                .ToListAsync();
            pairedMoiJobIds = pairedMoiJobIds.Concat(customerMoiJobIds).Distinct().ToList();
        }

        if (pairedMoiJobIds.Count > 0)
        {
            var extraMoiForms = await context.MOIForms
                .Where(f => f.JobRequestId != null && pairedMoiJobIds.Contains(f.JobRequestId.Value))
                .ToListAsync();
            moiForms.AddRange(extraMoiForms);
        }

        var moiByJobId = moiForms
            .Where(f => f.JobRequestId.HasValue)
            .GroupBy(f => f.JobRequestId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var jobEntities = user != null
            ? await context.JobRequests
                .Include(j => j.Units).ThenInclude(u => u.Assignees)
                .Where(j => ids.Contains(j.JobRequestId))
                .ToDictionaryAsync(j => j.JobRequestId)
            : new Dictionary<int, JobRequest>();

        foreach (var job in list)
        {
            var moiForIntake = moiByJobId.GetValueOrDefault(job.Id);
            job.AwaitingIntakeApproval = TaskFormVisibilityHelper.AwaitingIntakeApproval(
                jobEntities.GetValueOrDefault(job.Id) ?? new JobRequest
                {
                    JobRequestId = job.Id,
                    TaskType = job.TaskType,
                    InternalHandoffStatus = job.InternalHandoffStatus,
                    Units = job.Units.Select(u => new JobRequestUnit
                    {
                        UnitNumber = u.UnitNumber,
                        InternalHandoffStatus = u.InternalHandoffStatus ?? string.Empty,
                    }).ToList(),
                },
                moiForIntake);

            var moiForm = moiByJobId.GetValueOrDefault(job.Id);
            var pairedMoiJob = FindPairedMoiJob(list, job);
            var pairedMoiForm = pairedMoiJob != null ? moiByJobId.GetValueOrDefault(pairedMoiJob.Id) : null;

            var display = jobEntities.TryGetValue(job.Id, out var entity)
                ? PackageItemStatusResolver.Resolve(entity, moiForm, pairedMoiForm)
                : PackageItemStatusResolver.ResolveFromResponse(job, moiForm, pairedMoiForm);

            job.DisplayStatus = display.Label;
            job.DisplayStatusKey = display.Key;
            job.TaskPhase = display.Label;

            if (user == null || !jobEntities.TryGetValue(job.Id, out entity))
            {
                ApplyRawFormLinks(job, list, moiForms, moaForms, serviceForms);
                FinishUnitEnrichment(job, entity, moiForms, moaForms, user);
                continue;
            }

            var moi = moiForms.FirstOrDefault(f => f.JobRequestId == job.Id);
            var moa = moaForms.FirstOrDefault(f => f.JobRequestId == job.Id);
            job.HasMoiForm = moi != null;
            job.HasMoaForm = moa != null;
            job.MoiWorkflowState = moi?.WorkflowState;

            var (kind, formId) = TaskFormVisibilityHelper.ResolveVisibleFormLink(
                user, entity, moi, moa);

            if (kind != null && formId.HasValue)
            {
                job.LinkedFormKind = kind;
                job.LinkedFormId = formId;
            }
            else if (string.Equals(job.TaskType, "MOA", StringComparison.OrdinalIgnoreCase))
            {
                if (moa != null && TaskFormVisibilityHelper.CanViewMoaForm(user, entity, moa))
                {
                    job.LinkedFormKind = "MOA";
                    job.LinkedFormId = moa.MOAFormId;
                }
            }
            else if (moa != null && TaskFormVisibilityHelper.CanViewMoaForm(user, entity, moa))
            {
                job.LinkedFormKind = "MOA";
                job.LinkedFormId = moa.MOAFormId;
            }
            else if (!TaskFormVisibilityHelper.ShouldPreferMoaOverMoi(entity, moi)
                && job.TaskType is "MOI" or "MOI Approval" or "Service")
            {
                var moiForLink = job.TaskType is "MOI" or "Service" ? moi : pairedMoiForm != null
                    ? moiForms.FirstOrDefault(f => f.MOIFormId == pairedMoiForm.MOIFormId)
                    : moi;
                if (moiForLink != null && TaskFormVisibilityHelper.CanViewMoiForm(user, entity, moiForLink))
                {
                    job.LinkedFormKind = "MOI";
                    job.LinkedFormId = moiForLink.MOIFormId;
                }
            }
            else
            {
                var svc = serviceForms.FirstOrDefault(f => f.JobRequestId == job.Id);
                if (svc != null && AuthHelper.CanAccessJob(user, entity))
                {
                    job.LinkedFormKind = "Service";
                    job.LinkedFormId = svc.ServiceJobFormId;
                }
            }

            FinishUnitEnrichment(job, entity, moiForms, moaForms, user);
        }
    }

    private static void FinishUnitEnrichment(
        JobRequestResponse job,
        JobRequest? entity,
        List<MOIForm> moiForms,
        List<MOAForm> moaForms,
        ClaimsPrincipal? user)
    {
        entity ??= new JobRequest
        {
            JobRequestId = job.Id,
            TaskType = job.TaskType,
            CustomerId = job.CustomerId,
            TotalQty = job.TotalQty,
            InternalHandoffStatus = job.InternalHandoffStatus ?? string.Empty,
            Units = job.Units.Select(u => new JobRequestUnit
            {
                JobRequestUnitId = u.Id,
                UnitNumber = u.UnitNumber,
                InternalHandoffStatus = u.InternalHandoffStatus ?? string.Empty,
            }).ToList(),
        };

        var jobMois = moiForms.Where(f => f.JobRequestId == job.Id).ToList();
        var jobMoas = moaForms.Where(f => f.JobRequestId == job.Id).ToList();
        EnrichJobUnits(job, entity, jobMois, jobMoas, user);
    }

    private static void EnrichJobUnits(
        JobRequestResponse job,
        JobRequest entity,
        List<MOIForm> jobMois,
        List<MOAForm> jobMoas,
        ClaimsPrincipal? user)
    {
        if (job.Units.Count == 0)
            return;

        foreach (var unitDto in job.Units)
        {
            var unitEntity = entity.Units.FirstOrDefault(u => u.UnitNumber == unitDto.UnitNumber);
            if (unitEntity == null)
                continue;

            unitDto.InternalHandoffStatus = unitEntity.InternalHandoffStatus;

            var moi = ResolveMoiForUnit(jobMois, unitEntity, job.TotalQty);
            var moa = jobMoas.FirstOrDefault(f => f.JobRequestUnitId == unitEntity.JobRequestUnitId)
                ?? (job.TotalQty <= 1 ? jobMoas.FirstOrDefault(f => f.JobRequestUnitId == null) : null);

            unitDto.HasMoiForm = moi != null;
            unitDto.HasMoaForm = moa != null;
            unitDto.MoiFormId = moi?.MOIFormId;
            unitDto.MoiWorkflowState = moi?.WorkflowState;
            unitDto.RequiredExecutionDate = MoiFormMetadataHelper.ReadRequiredExecutionDate(moi);

            var display = PackageItemStatusResolver.ResolveForUnit(entity, unitEntity, moi);
            unitDto.DisplayStatus = display.Label;
            unitDto.DisplayStatusKey = display.Key;
            unitDto.AwaitingIntakeApproval = TaskFormVisibilityHelper.UnitAwaitingIntakeApproval(unitEntity, moi);

            var canView = user?.Identity?.IsAuthenticated == true;
            if (moa != null && (!canView || TaskFormVisibilityHelper.CanViewMoaForm(user!, entity, moa)))
            {
                unitDto.LinkedFormKind = "MOA";
                unitDto.LinkedFormId = moa.MOAFormId;
            }
            else if (!TaskFormVisibilityHelper.ShouldPreferMoaOverMoi(entity, moi, unitEntity.InternalHandoffStatus)
                && moi != null
                && (!canView || TaskFormVisibilityHelper.CanViewMoiForm(user!, entity, moi)))
            {
                unitDto.LinkedFormKind = "MOI";
                unitDto.LinkedFormId = moi.MOIFormId;
            }
        }

        if (job.TotalQty > 1)
        {
            job.LinkedFormKind = null;
            job.LinkedFormId = null;
            job.HasMoiForm = job.Units.Any(u => u.HasMoiForm);
            job.HasMoaForm = job.Units.Any(u => u.HasMoaForm);
            if (job.Units.Any(u => u.AwaitingIntakeApproval))
                job.AwaitingIntakeApproval = true;
        }
    }

    private static MOIForm? ResolveMoiForUnit(List<MOIForm> mois, JobRequestUnit unit, int totalQty)
    {
        var byUnit = mois.FirstOrDefault(f => f.JobRequestUnitId == unit.JobRequestUnitId);
        if (byUnit != null)
            return byUnit;

        if (totalQty <= 1)
            return mois.FirstOrDefault(f => f.JobRequestUnitId == null);

        // Multi-session: never attach a job-level MOI to every unit.
        return null;
    }

    private static JobRequestResponse? FindPairedMoiJob(List<JobRequestResponse> jobs, JobRequestResponse job)
    {
        if (job.TaskType == "MOI Approval")
        {
            return jobs.FirstOrDefault(j =>
                j.CustomerId == job.CustomerId
                && j.TaskType == "MOI"
                && string.Equals(j.AccountHolder, job.AccountHolder, StringComparison.OrdinalIgnoreCase));
        }

        if (job.TaskType != "MOA")
            return null;

        return jobs.FirstOrDefault(j =>
            j.CustomerId == job.CustomerId
            && string.Equals(j.AccountHolder, job.AccountHolder, StringComparison.OrdinalIgnoreCase)
            && j.TaskType == "MOI");
    }

    private static void ApplyRawFormLinks(
        JobRequestResponse job,
        List<JobRequestResponse> allJobs,
        List<MOIForm> moiForms,
        List<MOAForm> moaForms,
        List<ServiceJobForm> serviceForms)
    {
        if (string.Equals(job.TaskType, "MOA", StringComparison.OrdinalIgnoreCase))
        {
            var moa = moaForms.FirstOrDefault(f => f.JobRequestId == job.Id);
            job.HasMoaForm = moa != null;
            if (moa != null) { job.LinkedFormKind = "MOA"; job.LinkedFormId = moa.MOAFormId; }
            return;
        }

        if (job.TaskType is "MOI" or "MOI Approval" or "Service")
        {
            var moi = moiForms.FirstOrDefault(f => f.JobRequestId == job.Id);
            if (moi == null && job.TaskType == "MOI Approval")
            {
                var pairedJob = FindPairedMoiJob(allJobs, job);
                if (pairedJob != null)
                    moi = moiForms.FirstOrDefault(f => f.JobRequestId == pairedJob.Id);
            }
            job.HasMoiForm = moi != null;
            job.MoiWorkflowState = moi?.WorkflowState;
            if (moi != null) { job.LinkedFormKind = "MOI"; job.LinkedFormId = moi.MOIFormId; }
            return;
        }

        var svc = serviceForms.FirstOrDefault(f => f.JobRequestId == job.Id);
        if (svc != null) { job.LinkedFormKind = "Service"; job.LinkedFormId = svc.ServiceJobFormId; }
    }
}
