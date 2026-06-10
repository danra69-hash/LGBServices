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
public class JobRequestsController : ControllerBase
{
    private readonly AppDbContext _context;

    public JobRequestsController(AppDbContext context)
    {
        _context = context;
    }

    private IQueryable<JobRequest> JobQuery() =>
        _context.JobRequests
            .Include(j => j.Units)
            .ThenInclude(u => u.Assignees)
            .ThenInclude(a => a.User);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<JobRequestResponse>>> GetJobRequests(
        [FromQuery] int? customerPackageId,
        [FromQuery] bool includeCompleted = false)
    {
        var query = JobQuery();

        if (!includeCompleted)
            query = query.Where(j => j.Status != "Completed" && j.Status != "Canceled");

        if (customerPackageId.HasValue)
        {
            var package = await _context.CustomerPackages.FindAsync(customerPackageId.Value);
            if (package == null)
                return NotFound();

            query = query.Where(j =>
                j.CustomerPackageId == customerPackageId.Value
                || (j.CustomerId == package.CustomerId
                    && j.CustomerPackageId == null
                    && (j.TaskType == "MOI" || j.TaskType == "MOI Approval" || j.TaskType == "MOA")));
        }

        var jobs = await query
            .OrderBy(j => j.TaskType)
            .ThenBy(j => j.Service)
            .ThenBy(j => j.AccountHolder)
            .ToListAsync();

        if (AuthHelper.IsExternalUser(User))
        {
            var customerId = AuthHelper.CurrentCustomerId(User);
            if (!customerId.HasValue)
                return Ok(Array.Empty<JobRequestResponse>());
            jobs = jobs.Where(j => j.CustomerId == customerId).ToList();
        }
        else if (!AuthHelper.IsAdmin(User))
        {
            var userId = AuthHelper.CurrentUserId(User);
            if (!userId.HasValue)
                return Ok(Array.Empty<JobRequestResponse>());

            jobs = jobs
                .Where(j => !TaskFormVisibilityHelper.AwaitingIntakeApproval(j))
                .Where(j => j.Units.Any(u => JobRequestUnitService.IsUserAssigned(u, userId.Value))
                    || j.AssignedUserId == userId.Value)
                .ToList();
        }

        var responses = jobs.Select(JobRequestMapper.ToResponse).ToList();
        await JobFormLinkService.EnrichWithFormLinksAsync(_context, responses, User);
        return responses;
    }

    [HttpGet("my-tracker")]
    public async Task<ActionResult<IEnumerable<WorkTrackerItemDto>>> GetMyTracker()
    {
        var userId = AuthHelper.CurrentUserId(User);
        if (!userId.HasValue)
            return Ok(Array.Empty<WorkTrackerItemDto>());

        var units = await _context.JobRequestUnits
            .Include(u => u.Assignees)
            .ThenInclude(a => a.User)
            .Include(u => u.JobRequest)
            .Where(u => u.Status != "Completed"
                && u.JobRequest.InternalHandoffStatus != JobHandoffStatuses.ClientSubmitted
                && (u.AssignedUserId == userId.Value
                    || u.Assignees.Any(a => a.UserId == userId.Value)))
            .OrderBy(u => u.ScheduledDate ?? DateTime.MaxValue)
            .ThenBy(u => u.JobRequest.Customer)
            .ToListAsync();

        return units
            .Select(u => JobRequestUnitService.ToTrackerDto(u, u.JobRequest))
            .ToList();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<JobRequestResponse>> GetJobRequest(int id)
    {
        var job = await JobQuery().FirstOrDefaultAsync(j => j.JobRequestId == id);
        if (job == null)
            return NotFound();

        if (!AuthHelper.CanAccessJob(User, job))
            return Forbid();

        return JobRequestMapper.ToResponse(job);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<JobRequestResponse>> CreateJobRequest(JobRequestRequest request)
    {
        var job = new JobRequest();
        JobRequestMapper.ApplyRequest(job, request, isAdmin: true);
        _context.JobRequests.Add(job);
        await _context.SaveChangesAsync();
        await JobRequestUnitService.SyncUnitsForJobAsync(_context, job);
        await JobFormProvisioner.EnsureFormForJobAsync(_context, job);

        job = await JobQuery().FirstAsync(j => j.JobRequestId == job.JobRequestId);
        var created = JobRequestMapper.ToResponse(job);
        await JobFormLinkService.EnrichWithFormLinksAsync(_context, [created], User);
        return CreatedAtAction(nameof(GetJobRequest), new { id = job.JobRequestId }, created);
    }

    [HttpPost("{id}/approve-intake")]
    public async Task<ActionResult<JobRequestResponse>> ApproveMoiIntake(int id)
    {
        if (!AuthHelper.CanApproveMoiIntake(User))
            return Forbid();

        var job = await JobQuery().FirstOrDefaultAsync(j => j.JobRequestId == id);
        if (job == null) return NotFound();

        if (!TaskFormVisibilityHelper.AwaitingIntakeApproval(job))
            return BadRequest(new { message = "This task is not awaiting MOI intake approval." });

        await JobHandoffService.OnMoiIntakeApprovedAsync(_context, job);
        job = await JobQuery().FirstAsync(j => j.JobRequestId == id);
        var response = JobRequestMapper.ToResponse(job);
        await JobFormLinkService.EnrichWithFormLinksAsync(_context, [response], User);
        return Ok(response);
    }

    [HttpPost("{id}/assign")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignJob(int id, AssignJobRequest request)
    {
        var job = await JobQuery().FirstOrDefaultAsync(j => j.JobRequestId == id);
        if (job == null)
            return NotFound();

        var user = await _context.Users.FindAsync(request.UserId);
        if (user == null)
            return BadRequest(new { message = "Selected user was not found." });

        await JobRequestUnitService.SyncUnitsForJobAsync(_context, job);
        job = await JobQuery().FirstAsync(j => j.JobRequestId == id);

        var unit = JobRequestUnitService.ResolveUnit(job, request.UnitNumber);
        if (unit == null)
            return BadRequest(new { message = "No unit available to assign." });

        if (request.Remove)
            await JobRequestUnitService.RemoveAssigneeAsync(_context, unit, user.UserId);
        else
            await JobRequestUnitService.AddAssigneeAsync(_context, unit, user);

        if (!string.IsNullOrWhiteSpace(request.Comments))
            job.AssignmentComments = request.Comments;

        if (DateTime.TryParse(request.AcceptedDate, out var accepted))
            job.DateRequested = accepted;

        await JobRequestUnitService.RefreshJobAggregateAsync(_context, job);
        await JobRequestUnitService.SyncUnitToTrackerAsync(_context, unit, job);
        await JobHandoffService.OnJobAcceptedAsync(_context, job);
        await _context.SaveChangesAsync();

        job = await JobQuery().FirstAsync(j => j.JobRequestId == id);
        return Ok(JobRequestMapper.ToResponse(job));
    }

    [HttpPost("{id}/handoff")]
    public async Task<ActionResult<JobRequestResponse>> AdvanceHandoff(int id, JobHandoffRequest request)
    {
        var job = await JobQuery().FirstOrDefaultAsync(j => j.JobRequestId == id);
        if (job == null) return NotFound();

        if (!AuthHelper.CanAccessJob(User, job))
            return Forbid();

        var isAdmin = AuthHelper.IsAdmin(User);
        var action = request.Action?.Trim() ?? string.Empty;

        switch (action)
        {
            case "start-prep":
                if (!isAdmin && !AuthHelper.IsInternalStaff(User)) return Forbid();
                JobHandoffService.SetHandoff(job, JobHandoffStatuses.PendingPrep);
                if (job.Status == "Pending") job.Status = "In Progress";
                break;
            case "start-reso":
                if (!isAdmin && !AuthHelper.IsInternalStaff(User)) return Forbid();
                JobHandoffService.SetHandoff(job, JobHandoffStatuses.ResoInProgress);
                job.Status = "In Progress";
                break;
            case "submit-admin-review":
                if (!isAdmin && !AuthHelper.IsInternalStaff(User)) return Forbid();
                JobHandoffService.SetHandoff(job, JobHandoffStatuses.AdminReview);
                break;
            case "approve-for-moa":
                if (!isAdmin) return Forbid();
                await JobHandoffService.AdvanceToReadyForMoaAsync(_context, job);
                job = await JobQuery().FirstAsync(j => j.JobRequestId == id);
                return Ok(JobRequestMapper.ToResponse(job));
            default:
                return BadRequest("Unknown handoff action.");
        }

        if (!string.IsNullOrWhiteSpace(request.Comments))
            job.AssignmentComments = request.Comments;

        await _context.SaveChangesAsync();
        job = await JobQuery().FirstAsync(j => j.JobRequestId == id);
        var response = JobRequestMapper.ToResponse(job);
        await JobFormLinkService.EnrichWithFormLinksAsync(_context, [response], User);
        return Ok(response);
    }

    [HttpPost("{id}/progress")]
    public async Task<IActionResult> RecordProgress(int id, JobProgressRequest request)
    {
        var job = await JobQuery().FirstOrDefaultAsync(j => j.JobRequestId == id);
        if (job == null)
            return NotFound();

        if (!AuthHelper.CanAccessJob(User, job))
            return Forbid();

        await JobRequestUnitService.SyncUnitsForJobAsync(_context, job);
        job = await JobQuery().FirstAsync(j => j.JobRequestId == id);

        var unitNumber = request.UnitNumber ?? (job.TotalQty == 1 ? 1 : null);
        if (!unitNumber.HasValue)
            return BadRequest(new { message = "Unit number is required for multi-quantity items." });

        var unit = job.Units.FirstOrDefault(u => u.UnitNumber == unitNumber.Value);
        if (unit == null)
            return BadRequest(new { message = "Unit not found." });

        var isAdmin = AuthHelper.IsAdmin(User);
        if (!isAdmin)
        {
            var userId = AuthHelper.CurrentUserId(User);
            if (!userId.HasValue || !JobRequestUnitService.IsUserAssigned(unit, userId.Value))
                return Forbid();
        }

        if (request.UserId.HasValue && isAdmin)
        {
            var assignUser = await _context.Users.FindAsync(request.UserId.Value);
            if (assignUser == null)
                return BadRequest(new { message = "Selected user was not found." });
            await JobRequestUnitService.AddAssigneeAsync(_context, unit, assignUser);
        }

        if (request.ScheduledDate is not null)
        {
            if (!AuthHelper.IsClientAdmin(User))
                return BadRequest(new { message = "Scheduled dates are set by the client company." });

            unit.ScheduledDate = string.IsNullOrWhiteSpace(request.ScheduledDate)
                ? null
                : DateOnlyHelper.Parse(request.ScheduledDate);
            await JobRequestUnitService.SyncUnitToTrackerAsync(_context, unit, job);
        }

        if (request.MarkUnitComplete && request.MarkUnitIncomplete)
            return BadRequest(new { message = "Cannot mark a unit complete and incomplete in the same request." });

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
                    CreatedAt = DateTime.UtcNow
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
        else
        {
            await JobRequestUnitService.RefreshJobAggregateAsync(_context, job);
        }

        await _context.SaveChangesAsync();
        job = await JobQuery().FirstAsync(j => j.JobRequestId == id);
        return Ok(JobRequestMapper.ToResponse(job));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateJobRequest(int id, JobRequestRequest request)
    {
        var job = await JobQuery().FirstOrDefaultAsync(j => j.JobRequestId == id);
        if (job == null)
            return NotFound();

        var isAdmin = AuthHelper.IsAdmin(User);
        if (!isAdmin && !AuthHelper.CanAccessJob(User, job))
            return Forbid();

        var previousStatus = job.Status;
        JobRequestMapper.ApplyRequest(job, request, isAdmin);

        if (request.Status is "Completed" or "Canceled" && previousStatus != request.Status)
        {
            job.DateCompleted = DateTime.UtcNow;
            if (request.Status == "Completed" && job.UsedQty < job.TotalQty)
                job.UsedQty = job.TotalQty;

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
                Status = request.Status,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteJobRequest(int id)
    {
        var job = await _context.JobRequests.FindAsync(id);
        if (job == null)
            return NotFound();

        _context.JobRequests.Remove(job);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
