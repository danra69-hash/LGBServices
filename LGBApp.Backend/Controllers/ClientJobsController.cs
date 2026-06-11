using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;
using LGBApp.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ClientJobsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ClientJobsController(AppDbContext context) => _context = context;

    [HttpGet("my-jobs")]
    [Authorize(Roles = "Admin,ClientAdmin,ClientSignatory")]
    public async Task<ActionResult<IEnumerable<JobRequestResponse>>> GetMyCompanyJobs(
        [FromQuery] bool includeCompleted = false)
    {
        if (!AuthHelper.IsAdmin(User) && !AuthHelper.IsExternalUser(User))
            return Forbid();

        var accessibleCustomerIds = AuthHelper.IsAdmin(User)
            ? null
            : AuthHelper.GetAccessibleCustomerIds(User).ToList();
        if (!AuthHelper.IsAdmin(User) && (accessibleCustomerIds?.Count ?? 0) == 0)
            return BadRequest("External users must be linked to a customer.");

        var query = _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees).ThenInclude(a => a.User)
            .AsQueryable();

        if (!includeCompleted)
            query = query.Where(j => j.Status != "Completed" && j.Status != "Canceled");

        if (accessibleCustomerIds != null)
            query = query.Where(j => j.CustomerId.HasValue && accessibleCustomerIds.Contains(j.CustomerId.Value));

        var jobs = await query
            .OrderByDescending(j => j.DateRequested)
            .ToListAsync();

        if (AuthHelper.IsExternalUser(User))
            jobs = jobs.Where(j => j.TaskType == "Service").ToList();

        var responses = jobs.Select(JobRequestMapper.ToResponse).ToList();
        await JobFormLinkService.EnrichWithFormLinksAsync(_context, responses, User);
        return responses;
    }

    [HttpPost("{jobId}/issue-moi")]
    [Authorize(Roles = "Admin,ClientAdmin,ClientSignatory")]
    public async Task<ActionResult<JobRequestResponse>> IssueMoiForJob(int jobId, IssueMoiRequest? request)
    {
        var job = await _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees)
            .FirstOrDefaultAsync(j => j.JobRequestId == jobId);
        if (job == null) return NotFound();

        if (!AuthHelper.IsAdmin(User)
            && !AuthHelper.CanManageClientJob(User, job)
            && !await AuthHelper.CanSignatoryIssueMoiAsync(_context, User, job))
            return Forbid();

        return await IssueMoiForJobCoreAsync(job, request ?? new IssueMoiRequest());
    }

    [HttpPost("issue-moi")]
    [Authorize(Roles = "Admin,ClientAdmin")]
    public async Task<ActionResult<JobRequestResponse>> IssueMoi(IssueMoiRequest request)
    {
        var customerId = AuthHelper.IsClientAdmin(User)
            ? AuthHelper.CurrentCustomerId(User)
            : AuthHelper.CurrentCustomerId(User) ?? request.CustomerId;
        if (!customerId.HasValue)
            return BadRequest("Customer is required (link user to customer or pass customerId).");

        var customer = await _context.Customers
            .Include(c => c.AccountHolders)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (customer == null) return NotFound("Customer not found.");

        var user = await _context.Users.FindAsync(AuthHelper.CurrentUserId(User));
        var requestedBy = string.IsNullOrWhiteSpace(request.RequestedBy)
            ? user?.Name ?? string.Empty
            : request.RequestedBy;

        var holder = customer.AccountHolders
            .FirstOrDefault(h => h.Name.Equals(requestedBy, StringComparison.OrdinalIgnoreCase))
            ?? customer.AccountHolders.FirstOrDefault();

        var initiation = DateTime.TryParse(request.InitiationDate, out var dt)
            ? dt
            : DateTime.UtcNow;

        var service = string.IsNullOrWhiteSpace(request.Service)
            ? (request.TypeOfDocument ?? "MOI")
            : request.Service;

        var job = new JobRequest
        {
            CustomerId = customer.CustomerId,
            CustomerPackageId = request.AdHoc ? null : request.CustomerPackageId,
            Customer = customer.Company,
            TaskType = "MOI",
            Service = service,
            AccountHolder = holder?.Name ?? requestedBy,
            AccountHolderEmail = holder?.Email ?? user?.Email ?? string.Empty,
            AccountHolderPhone = holder?.Phone ?? user?.Mobile ?? string.Empty,
            Status = "Pending",
            DateRequested = initiation,
            TotalQty = 1,
            UsedQty = 0,
            JobAssignedTo = string.Empty,
        };

        _context.JobRequests.Add(job);
        await _context.SaveChangesAsync();
        await JobRequestUnitService.SyncUnitsForJobAsync(_context, job);

        request.RequestedBy = requestedBy;
        request.Service = service;
        return await IssueMoiForJobCoreAsync(job, request, created: true);
    }

    private async Task<ActionResult<JobRequestResponse>> IssueMoiForJobCoreAsync(
        JobRequest job,
        IssueMoiRequest request,
        bool created = false)
    {
        var user = await _context.Users.FindAsync(AuthHelper.CurrentUserId(User));
        var requestedBy = string.IsNullOrWhiteSpace(request.RequestedBy)
            ? user?.Name ?? string.Empty
            : request.RequestedBy;

        if (string.IsNullOrWhiteSpace(job.AccountHolder))
        {
            job.AccountHolder = requestedBy;
            job.AccountHolderEmail = user?.Email ?? job.AccountHolderEmail;
            job.AccountHolderPhone = user?.Mobile ?? job.AccountHolderPhone;
        }

        await JobRequestUnitService.SyncUnitsForJobAsync(_context, job);
        await JobWorkflowIntegrityService.RepairJobAsync(_context, job);
        await _context.SaveChangesAsync();
        job = await _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees)
            .FirstAsync(j => j.JobRequestId == job.JobRequestId);

        var unitNumber = request.UnitNumber ?? (job.TotalQty == 1 ? 1 : null);
        if (!unitNumber.HasValue)
            return BadRequest(new { message = "Unit number is required for multi-session items." });

        var unit = MoiFormService.ResolveUnit(job, unitNumber);
        if (unit == null)
            return BadRequest(new { message = "Session not found for this item." });

        var moiForm = await MoiFormService.EnsureMoiForUnitAsync(_context, job, unit);

        var data = JsonHelper.Deserialize<Dictionary<string, object?>>(moiForm.FormDataJson);
        if (!string.IsNullOrWhiteSpace(request.TypeOfDocument))
            data["typeOfDocument"] = request.TypeOfDocument;
        if (!string.IsNullOrWhiteSpace(request.DocumentTitle))
            data["documentTitle"] = request.DocumentTitle;
        data["requestedBy"] = requestedBy;
        data["service"] = string.IsNullOrWhiteSpace(request.Service) ? job.Service : request.Service;
        data["jobId"] = job.JobRequestId;
        data["unitNumber"] = unit.UnitNumber;
        data["sessionLabel"] = job.TotalQty > 1 ? $"session {unit.UnitNumber}" : string.Empty;
        moiForm.FormDataJson = JsonHelper.Serialize(data);
        moiForm.UpdatedAt = DateTime.UtcNow;

        var unitHandoff = unit.InternalHandoffStatus;
        var alreadyIssued = moiForm.WorkflowState != MoiWorkflowStates.Draft
            || unitHandoff == JobHandoffStatuses.ClientSubmitted
            || (job.TotalQty <= 1 && job.InternalHandoffStatus == JobHandoffStatuses.ClientSubmitted);
        if (!alreadyIssued)
            await JobHandoffService.OnClientMoiIssuedAsync(_context, job, moiForm, unit);
        else
            await _context.SaveChangesAsync();

        job = await _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees)
            .FirstAsync(j => j.JobRequestId == job.JobRequestId);

        var response = JobRequestMapper.ToResponse(job);
        await JobFormLinkService.EnrichWithFormLinksAsync(_context, [response], User);
        if (created)
            return CreatedAtAction(nameof(GetMyCompanyJobs), response);
        return Ok(response);
    }

    [HttpPost("{jobId}/assign")]
    [Authorize(Roles = "Admin,ClientAdmin")]
    public async Task<ActionResult<JobRequestResponse>> AssignJob(int jobId, AssignJobRequest request)
    {
        var job = await _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees).ThenInclude(a => a.User)
            .FirstOrDefaultAsync(j => j.JobRequestId == jobId);

        if (job == null) return NotFound();

        if (!AuthHelper.CanManageClientJob(User, job))
            return Forbid();

        var assignee = await _context.Users.FindAsync(request.UserId);
        if (assignee == null)
            return BadRequest(new { message = "Selected user was not found." });

        if (request.Remove)
        {
            if (!AuthHelper.IsAdmin(User) && assignee.CustomerId != job.CustomerId)
                return Forbid();
        }
        else if (!AuthHelper.CanAssignClientJob(User, job, assignee) && !AuthHelper.IsAdmin(User))
        {
            return Forbid();
        }

        await JobRequestUnitService.SyncUnitsForJobAsync(_context, job);
        job = await _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees).ThenInclude(a => a.User)
            .FirstAsync(j => j.JobRequestId == jobId);

        var unit = JobRequestUnitService.ResolveUnit(job, request.UnitNumber);
        if (unit == null)
            return BadRequest(new { message = "No unit available to assign." });

        if (request.Remove)
            await JobRequestUnitService.RemoveAssigneeAsync(_context, unit, assignee.UserId);
        else
            await JobRequestUnitService.AddAssigneeAsync(_context, unit, assignee);

        if (!string.IsNullOrWhiteSpace(request.Comments))
            job.AssignmentComments = request.Comments;

        await JobRequestUnitService.RefreshJobAggregateAsync(_context, job);
        await JobRequestUnitService.SyncUnitToTrackerAsync(_context, unit, job);
        await _context.SaveChangesAsync();

        job = await _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees)
            .FirstAsync(j => j.JobRequestId == jobId);

        var response = JobRequestMapper.ToResponse(job);
        await JobFormLinkService.EnrichWithFormLinksAsync(_context, [response], User);
        return Ok(response);
    }

    [HttpPost("{jobId}/progress")]
    [Authorize(Roles = "Admin,ClientAdmin")]
    public async Task<ActionResult<JobRequestResponse>> RecordProgress(int jobId, JobProgressRequest request)
    {
        var job = await _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees).ThenInclude(a => a.User)
            .FirstOrDefaultAsync(j => j.JobRequestId == jobId);

        if (job == null) return NotFound();
        if (!AuthHelper.CanManageClientJob(User, job))
            return Forbid();

        await JobRequestUnitService.SyncUnitsForJobAsync(_context, job);
        job = await _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees)
            .FirstAsync(j => j.JobRequestId == jobId);

        var unitNumber = request.UnitNumber ?? (job.TotalQty == 1 ? 1 : null);
        if (!unitNumber.HasValue)
            return BadRequest(new { message = "Unit number is required for multi-quantity items." });

        var unit = job.Units.FirstOrDefault(u => u.UnitNumber == unitNumber.Value);
        if (unit == null)
            return BadRequest(new { message = "Unit not found." });

        if (request.MarkUnitComplete && request.MarkUnitIncomplete)
            return BadRequest(new { message = "Cannot mark a unit complete and incomplete in the same request." });

        if (request.ScheduledDate is not null)
        {
            unit.ScheduledDate = string.IsNullOrWhiteSpace(request.ScheduledDate)
                ? null
                : DateOnlyHelper.Parse(request.ScheduledDate);
            await JobRequestUnitService.SyncUnitToTrackerAsync(_context, unit, job);
        }

        if (request.MarkUnitComplete)
        {
            unit.Status = "Completed";
            unit.CompletedAt = DateTime.UtcNow;
            await JobRequestUnitService.SyncUnitToTrackerAsync(_context, unit, job);
            await JobRequestUnitService.RefreshJobAggregateAsync(_context, job);

            if (job.Status == "Completed")
            {
                _context.CompletedServices.Add(new CompletedService
                {
                    Customer = job.Customer,
                    Service = job.Service,
                    UsedQty = job.UsedQty,
                    TotalQty = job.TotalQty,
                    DateRequested = job.DateRequested,
                    DateCompleted = job.DateCompleted ?? DateTime.UtcNow,
                    AccountHolder = job.AccountHolder,
                    JobAssignedTo = job.JobAssignedTo,
                    Status = "Completed",
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }
        else if (request.MarkUnitIncomplete)
        {
            if (unit.Status != "Completed")
                return BadRequest(new { message = "Only completed units can be reverted." });

            var wasJobCompleted = job.Status == "Completed";
            await JobRequestUnitService.RevertUnitCompleteAsync(_context, unit, job);
            await JobRequestUnitService.RefreshJobAggregateAsync(_context, job);

            if (wasJobCompleted && job.Status != "Completed")
                await JobRequestUnitService.RemoveLatestCompletedServiceRecordAsync(_context, job);
        }
        else if (request.ScheduledDate is null)
        {
            return BadRequest(new { message = "Specify scheduledDate, markUnitComplete, or markUnitIncomplete." });
        }

        await _context.SaveChangesAsync();
        job = await _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees)
            .FirstAsync(j => j.JobRequestId == jobId);

        var response = JobRequestMapper.ToResponse(job);
        await JobFormLinkService.EnrichWithFormLinksAsync(_context, [response], User);
        return Ok(response);
    }
}
