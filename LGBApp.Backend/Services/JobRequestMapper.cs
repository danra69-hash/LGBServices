using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;

namespace LGBApp.Backend.Services;

public static class JobRequestMapper
{
    public static JobRequestResponse ToResponse(JobRequest job) => new()
    {
        Id = job.JobRequestId,
        CustomerId = job.CustomerId,
        CustomerPackageId = job.CustomerPackageId,
        Customer = job.Customer,
        TaskType = string.IsNullOrWhiteSpace(job.TaskType) ? job.Service : job.TaskType,
        Service = job.Service,
        UsedQty = job.UsedQty,
        TotalQty = job.TotalQty,
        DateRequested = job.DateRequested.ToString("yyyy-MM-dd"),
        ScheduledDate = DateOnlyHelper.Format(job.ScheduledDate),
        DateCompleted = job.DateCompleted?.ToString("yyyy-MM-dd"),
        AccountHolder = job.AccountHolder,
        AccountHolderEmail = job.AccountHolderEmail,
        AccountHolderPhone = job.AccountHolderPhone,
        AssignedUserId = job.AssignedUserId,
        JobAssignedTo = job.JobAssignedTo,
        Status = job.Status,
        InternalHandoffStatus = job.InternalHandoffStatus,
        AssignmentComments = job.AssignmentComments,
        WorkflowMode = job.WorkflowMode ?? string.Empty,
        AdminBypassNote = job.AdminBypassNote ?? string.Empty,
        AdminBypassAt = job.AdminBypassAt?.ToString("O"),
        Units = job.Units
            .OrderBy(u => u.UnitNumber)
            .Select(JobRequestUnitService.ToDto)
            .ToList(),
    };

    public static void ApplyRequest(JobRequest job, JobRequestRequest request, bool isAdmin)
    {
        job.Customer = request.Customer;
        if (request.CustomerId.HasValue)
            job.CustomerId = request.CustomerId;
        if (request.CustomerPackageId.HasValue)
            job.CustomerPackageId = request.CustomerPackageId;
        job.Service = request.Service;
        if (!string.IsNullOrWhiteSpace(request.TaskType))
            job.TaskType = request.TaskType;
        job.UsedQty = request.UsedQty;
        job.TotalQty = request.TotalQty;
        job.DateRequested = DateOnlyHelper.Parse(request.DateRequested) ?? job.DateRequested;
        job.ScheduledDate = DateOnlyHelper.Parse(request.ScheduledDate);
        job.DateCompleted = string.IsNullOrWhiteSpace(request.DateCompleted)
            ? null
            : DateOnlyHelper.Parse(request.DateCompleted);
        job.AccountHolder = request.AccountHolder;
        job.Status = request.Status;
        job.AssignmentComments = request.AssignmentComments;

        if (isAdmin)
        {
            job.AssignedUserId = request.AssignedUserId;
            job.JobAssignedTo = request.JobAssignedTo ?? string.Empty;
        }
    }
}
