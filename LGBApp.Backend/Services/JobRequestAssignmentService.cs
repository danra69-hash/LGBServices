using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public static class JobRequestAssignmentService
{
    public static async Task<List<JobRequest>> ResolveRelatedJobsForMoaPrepAsync(AppDbContext context, JobRequest job)
    {
        var related = new List<JobRequest> { job };
        if (!job.CustomerId.HasValue)
            return related;

        var customerId = job.CustomerId.Value;
        var holder = job.AccountHolder?.Trim() ?? string.Empty;

        var peers = await context.JobRequests
            .Where(j => j.CustomerId == customerId && j.Status != "Canceled")
            .ToListAsync();

        foreach (var peer in peers)
        {
            if (peer.JobRequestId == job.JobRequestId)
                continue;

            var sameHolder = !string.IsNullOrWhiteSpace(holder)
                && string.Equals(peer.AccountHolder, holder, StringComparison.OrdinalIgnoreCase);

            if (peer.TaskType == "MOA" && sameHolder)
                related.Add(peer);

            if (peer.TaskType is "MOI" or "Service" && sameHolder)
                related.Add(peer);
        }

        return related.DistinctBy(j => j.JobRequestId).ToList();
    }

    public static async Task<JobRequest> AssignSecretarialTeamAsync(AppDbContext context, int jobId)
    {
        var job = await context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees)
            .FirstOrDefaultAsync(j => j.JobRequestId == jobId);
        if (job == null)
            throw new DomainException("Job not found.", StatusCodes.Status404NotFound);

        var moiForm = job.TaskType is "MOI" or "Service"
            ? await context.MOIForms.FirstOrDefaultAsync(f => f.JobRequestId == job.JobRequestId)
            : await MoiFormPairingService.FindMoiFormForCustomerAsync(context, job.CustomerId ?? 0);

        if (!SecretarialStaffService.IsReadyForSecretarialAssignment(job, moiForm))
            throw new DomainException("MOI must be signed off before assigning the secretarial team.");

        var staff = await SecretarialStaffService.GetSecretarialStaffAsync(context);
        if (staff.Count == 0)
            throw new DomainException("No secretarial staff accounts found.");

        var relatedJobs = await ResolveRelatedJobsForMoaPrepAsync(context, job);

        foreach (var relatedJob in relatedJobs)
        {
            await JobRequestUnitService.SyncUnitsForJobAsync(context, relatedJob);
            var related = await context.JobRequests
                .Include(j => j.Units).ThenInclude(u => u.Assignees)
                .FirstAsync(j => j.JobRequestId == relatedJob.JobRequestId);

            foreach (var unit in related.Units)
            {
                foreach (var user in staff)
                    await JobRequestUnitService.AddAssigneeAsync(context, unit, user);
            }

            if (related.Status == "Pending")
                related.Status = "In Progress";

            if (related.TaskType == "MOA"
                && string.IsNullOrWhiteSpace(related.InternalHandoffStatus))
            {
                JobHandoffService.SetHandoff(related, JobHandoffStatuses.ResoInProgress);
            }

            if (related.TaskType is "MOI" or "Service")
                await JobFormProvisioner.EnsureMoaFormsForJobAsync(context, related);
        }

        await JobHandoffService.OnSecretarialTeamAssignedAsync(context, job);
        await context.SaveChangesAsync();
        return await context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees).ThenInclude(a => a.User)
            .FirstAsync(j => j.JobRequestId == jobId);
    }

}
