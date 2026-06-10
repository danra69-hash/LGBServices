using LGBApp.Backend.Data;
using LGBApp.Backend.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public static class JobFormLinkService
{
    public static async Task EnrichWithFormLinksAsync(AppDbContext context, IEnumerable<JobRequestResponse> jobs)
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

        foreach (var job in list)
        {
            if (string.Equals(job.TaskType, "MOA", StringComparison.OrdinalIgnoreCase))
            {
                var moa = moaForms.FirstOrDefault(f => f.JobRequestId == job.Id);
                if (moa != null) { job.LinkedFormKind = "MOA"; job.LinkedFormId = moa.MOAFormId; }
                continue;
            }

            if (job.TaskType is "MOI" or "MOI Approval")
            {
                var moi = moiForms.FirstOrDefault(f => f.JobRequestId == job.Id);
                if (moi != null) { job.LinkedFormKind = "MOI"; job.LinkedFormId = moi.MOIFormId; }
                continue;
            }

            var svc = serviceForms.FirstOrDefault(f => f.JobRequestId == job.Id);
            if (svc != null) { job.LinkedFormKind = "Service"; job.LinkedFormId = svc.ServiceJobFormId; }
        }
    }
}
