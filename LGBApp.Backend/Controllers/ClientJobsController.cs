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
    [Authorize(Roles = "Admin,ClientAdmin,Client")]
    public async Task<ActionResult<IEnumerable<JobRequestResponse>>> GetMyCompanyJobs(
        [FromQuery] bool includeCompleted = false)
    {
        if (!AuthHelper.IsAdmin(User) && !AuthHelper.IsExternalUser(User))
            return Forbid();

        var customerId = AuthHelper.CurrentCustomerId(User);
        if (!customerId.HasValue && !AuthHelper.IsAdmin(User))
            return BadRequest("External users must be linked to a customer.");

        var query = _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees).ThenInclude(a => a.User)
            .AsQueryable();

        if (!includeCompleted)
            query = query.Where(j => j.Status != "Completed" && j.Status != "Canceled");

        if (!AuthHelper.IsAdmin(User))
            query = query.Where(j => j.CustomerId == customerId);

        var jobs = await query
            .OrderByDescending(j => j.DateRequested)
            .ToListAsync();

        if (AuthHelper.IsClientUser(User))
        {
            var userId = AuthHelper.CurrentUserId(User);
            var name = AuthHelper.CurrentUserName(User);
            jobs = jobs.Where(j =>
                (userId.HasValue && j.Units.Any(u => JobRequestUnitService.IsUserAssigned(u, userId.Value)))
                || j.AssignedUserId == userId
                || (!string.IsNullOrWhiteSpace(name) && string.Equals(j.AccountHolder, name, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        var responses = jobs.Select(JobRequestMapper.ToResponse).ToList();
        await JobFormLinkService.EnrichWithFormLinksAsync(_context, responses);
        return responses;
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
        await JobFormProvisioner.EnsureFormForJobAsync(_context, job);

        var moiForm = await _context.MOIForms.FirstOrDefaultAsync(f => f.JobRequestId == job.JobRequestId);
        if (moiForm != null)
        {
            var data = JsonHelper.Deserialize<Dictionary<string, object?>>(moiForm.FormDataJson);
            if (request.TypeOfDocument != null) data["typeOfDocument"] = request.TypeOfDocument;
            if (request.DocumentTitle != null) data["documentTitle"] = request.DocumentTitle;
            data["requestedBy"] = requestedBy;
            data["service"] = service;
            data["adHoc"] = request.AdHoc;
            moiForm.FormDataJson = JsonHelper.Serialize(data);
            moiForm.UpdatedAt = DateTime.UtcNow;
            await JobHandoffService.OnClientMoiIssuedAsync(_context, job, moiForm);
        }

        job = await _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees)
            .FirstAsync(j => j.JobRequestId == job.JobRequestId);

        var response = JobRequestMapper.ToResponse(job);
        await JobFormLinkService.EnrichWithFormLinksAsync(_context, [response]);
        return CreatedAtAction(nameof(GetMyCompanyJobs), response);
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
        await JobFormLinkService.EnrichWithFormLinksAsync(_context, [response]);
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
        else
        {
            return BadRequest(new { message = "Specify markUnitComplete or markUnitIncomplete." });
        }

        await _context.SaveChangesAsync();
        job = await _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees)
            .FirstAsync(j => j.JobRequestId == jobId);

        var response = JobRequestMapper.ToResponse(job);
        await JobFormLinkService.EnrichWithFormLinksAsync(_context, [response]);
        return Ok(response);
    }
}
