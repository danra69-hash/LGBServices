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
        [FromQuery] bool includeCompleted = false,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        if (!AuthHelper.IsAdmin(User) && !AuthHelper.IsExternalUser(User))
            return Forbid();

        var accessibleCustomerIds = AuthHelper.IsAdmin(User)
            ? null
            : AuthHelper.GetAccessibleCustomerIds(User).ToList();
        if (!AuthHelper.IsAdmin(User) && (accessibleCustomerIds?.Count ?? 0) == 0)
            return BadRequest(new { message = "External users must be linked to a customer." });

        var query = _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees).ThenInclude(a => a.User)
            .AsQueryable();

        if (!includeCompleted)
            query = query.Where(j => j.Status != "Completed" && j.Status != "Canceled");

        if (accessibleCustomerIds != null)
            query = query.Where(j => j.CustomerId.HasValue && accessibleCustomerIds.Contains(j.CustomerId.Value));

        // Review #4 §6: push external Service-only filter into SQL; page in DB.
        if (AuthHelper.IsExternalUser(User))
            query = query.Where(j => j.TaskType == "Service");

        var (p, size) = Pagination.Normalize(page, pageSize);
        var jobs = await query
            .OrderByDescending(j => j.DateRequested)
            .Skip((p - 1) * size)
            .Take(size)
            .ToListAsync();

        var responses = jobs.Select(JobRequestMapper.ToResponse).ToList();
        await JobFormLinkService.EnrichWithFormLinksAsync(_context, responses, User);
        return responses;
    }

    /// <summary>D1: client chooses MOI/MOA workflow vs admin-bypass with a note for Sharon.</summary>
    [HttpPost("{jobId}/workflow-choice")]
    [Authorize(Roles = "Admin,ClientAdmin,ClientSignatory")]
    public async Task<ActionResult<JobRequestResponse>> ChooseWorkflow(int jobId, WorkflowChoiceRequest request)
    {
        var job = await _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees).ThenInclude(a => a.User)
            .FirstOrDefaultAsync(j => j.JobRequestId == jobId);
        if (job == null) return NotFound();

        if (!AuthHelper.IsAdmin(User)
            && !AuthHelper.CanManageClientJob(User, job)
            && !await AuthHelper.CanSignatoryIssueMoiAsync(_context, User, job))
            return Forbid();

        var mode = (request.Mode ?? string.Empty).Trim();
        if (!JobWorkflowModes.IsMoiMoa(mode) && !JobWorkflowModes.IsAdminBypass(mode))
            return BadRequest(new { message = "Mode must be MoiMoa or AdminBypass." });

        await JobRequestUnitService.SyncUnitsForJobAsync(_context, job);
        job = await _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees)
            .FirstAsync(j => j.JobRequestId == jobId);

        var unitNumber = request.UnitNumber ?? (job.TotalQty <= 1 ? 1 : null);
        if (!unitNumber.HasValue)
            return BadRequest(new { message = "Unit number is required for multi-session items." });

        var unit = job.Units.FirstOrDefault(u => u.UnitNumber == unitNumber.Value);
        if (unit == null)
            return BadRequest(new { message = "Session not found." });

        var userId = AuthHelper.CurrentUserId(User);

        if (JobWorkflowModes.IsAdminBypass(mode))
        {
            if (string.IsNullOrWhiteSpace(request.Note) || request.Note.Trim().Length < 8)
                return BadRequest(new { message = "Please describe what Sharon needs to do (at least 8 characters)." });

            var note = request.Note.Trim();
            var at = DateTime.UtcNow;
            unit.WorkflowMode = JobWorkflowModes.AdminBypass;
            unit.AdminBypassNote = note;
            unit.AdminBypassAt = at;
            unit.AdminBypassByUserId = userId;
            unit.InternalHandoffStatus = JobHandoffStatuses.AdminBypass;
            if (unit.Status == "Pending")
                unit.Status = "In Progress";

            if (job.TotalQty <= 1)
            {
                job.WorkflowMode = JobWorkflowModes.AdminBypass;
                job.AdminBypassNote = note;
                job.AdminBypassAt = at;
                job.AdminBypassByUserId = userId;
                JobHandoffService.SetHandoff(job, JobHandoffStatuses.AdminBypass);
            }
            else
            {
                JobHandoffResolver.SyncJobHandoffFromUnits(job);
            }

            if (job.Status == "Pending")
                job.Status = "In Progress";

            await _context.SaveChangesAsync();
            await WorkflowNotificationService.NotifyAdminBypassAsync(_context, job, note);
        }
        else
        {
            // MoiMoa — mark choice; client continues with issue-moi
            var wasAdminBypass = JobWorkflowModes.IsAdminBypass(unit.WorkflowMode)
                || string.Equals(unit.InternalHandoffStatus, JobHandoffStatuses.AdminBypass, StringComparison.OrdinalIgnoreCase)
                || JobWorkflowModes.IsAdminBypass(job.WorkflowMode);

            unit.WorkflowMode = JobWorkflowModes.MoiMoa;
            unit.AdminBypassNote = string.Empty;
            unit.AdminBypassAt = null;
            unit.AdminBypassByUserId = null;
            if (string.Equals(unit.InternalHandoffStatus, JobHandoffStatuses.AdminBypass, StringComparison.OrdinalIgnoreCase))
                unit.InternalHandoffStatus = string.Empty;

            if (job.TotalQty <= 1)
            {
                job.WorkflowMode = JobWorkflowModes.MoiMoa;
                job.AdminBypassNote = string.Empty;
                job.AdminBypassAt = null;
                job.AdminBypassByUserId = null;
                if (string.Equals(job.InternalHandoffStatus, JobHandoffStatuses.AdminBypass, StringComparison.OrdinalIgnoreCase))
                    JobHandoffService.SetHandoff(job, string.Empty);
            }
            else
            {
                JobHandoffResolver.SyncJobHandoffFromUnits(job);
            }

            await _context.SaveChangesAsync();

            // R1: drop stale "client wants bypass" alerts once the unit is back on MOI/MOA
            if (wasAdminBypass)
                await WorkflowNotificationService.MarkAdminBypassNotificationsReadAsync(_context, job.JobRequestId);
        }

        job = await _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees).ThenInclude(a => a.User)
            .FirstAsync(j => j.JobRequestId == jobId);
        var response = JobRequestMapper.ToResponse(job);
        await JobFormLinkService.EnrichWithFormLinksAsync(_context, [response], User);
        return Ok(response);
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
            return BadRequest(new { message = "Customer is required (link user to customer or pass customerId)." });

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

        // D1: AdminBypass tasks must not start MOI; prefer explicit MoiMoa (auto-set if unset)
        var unitMode = string.IsNullOrWhiteSpace(unit.WorkflowMode) ? job.WorkflowMode : unit.WorkflowMode;
        if (JobWorkflowModes.IsAdminBypass(unitMode))
            return BadRequest(new { message = "This task was sent to LGB without MOI/MOA. Contact your LGB admin to change that." });
        if (string.IsNullOrWhiteSpace(unitMode))
        {
            unit.WorkflowMode = JobWorkflowModes.MoiMoa;
            if (job.TotalQty <= 1)
                job.WorkflowMode = JobWorkflowModes.MoiMoa;
        }

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
            await JobRequestUnitService.RemoveAssigneeAsync(_context, unit, assignee.UserId, job);
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
            // D1: tasks that chose MOI/MOA cannot be closed client-side until LGB completes the workflow
            var mode = string.IsNullOrWhiteSpace(unit.WorkflowMode) ? job.WorkflowMode : unit.WorkflowMode;
            if (JobWorkflowModes.IsMoiMoa(mode))
            {
                var handoff = JobHandoffResolver.ResolveEffectiveHandoff(job, unit);
                if (!string.Equals(handoff, JobHandoffStatuses.Completed, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(handoff, JobHandoffStatuses.PendingExecute, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new
                    {
                        message = "This task requires the MOI/MOA workflow. Complete client sign-off (or ask LGB) before marking done.",
                    });
                }
            }

            // AdminBypass: client already submitted instructions — Sharon closes the work
            if (JobWorkflowModes.IsAdminBypass(mode) && !AuthHelper.IsAdmin(User))
            {
                return BadRequest(new
                {
                    message = "This request was sent to LGB. Sharon will mark it complete after the work is done.",
                });
            }

            // R3: AdminBypass has no prep assignees — attribute to the actor when closing
            if (JobWorkflowModes.IsAdminBypass(mode) && string.IsNullOrWhiteSpace(job.JobAssignedTo))
            {
                var actor = AuthHelper.CurrentUserName(User);
                if (!string.IsNullOrWhiteSpace(actor))
                    job.JobAssignedTo = actor.Trim();
            }

            unit.Status = "Completed";
            unit.CompletedAt = DateTime.UtcNow;
            await JobRequestUnitService.SyncUnitToTrackerAsync(_context, unit, job);
            await JobRequestUnitService.RefreshJobAggregateAsync(_context, job);

            if (job.Status == "Completed")
            {
                _context.CompletedServices.Add(new CompletedService
                {
                    JobRequestId = job.JobRequestId,
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
