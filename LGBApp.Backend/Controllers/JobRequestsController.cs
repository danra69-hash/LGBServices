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
    private readonly WorkflowNotifier _notifier;

    public JobRequestsController(AppDbContext context, WorkflowNotifier notifier)
    {
        _context = context;
        _notifier = notifier;
    }

    private IQueryable<JobRequest> JobQuery() =>
        _context.JobRequests
            .Include(j => j.Units)
            .ThenInclude(u => u.Assignees)
            .ThenInclude(a => a.User);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<JobRequestResponse>>> GetJobRequests(
        [FromQuery] int? customerPackageId,
        [FromQuery] bool includeCompleted = false,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        var query = JobQuery();

        if (!includeCompleted)
            query = query.Where(j => j.Status != "Completed" && j.Status != "Canceled");

        if (customerPackageId.HasValue)
        {
            var package = await _context.CustomerPackages.FindAsync(customerPackageId.Value);
            if (package == null)
                return Ok(Array.Empty<JobRequestResponse>());

            query = query.Where(j => j.CustomerPackageId == customerPackageId.Value);
        }

        // C6 / EF1: filter accessible companies in SQL for multi-company signatories
        if (AuthHelper.IsExternalUser(User))
        {
            var ids = AuthHelper.GetAccessibleCustomerIds(User);
            if (ids.Count == 0)
                return Ok(Array.Empty<JobRequestResponse>());
            query = query.Where(j => j.CustomerId.HasValue && ids.Contains(j.CustomerId.Value));
        }

        // External users: visibility is fully expressible in SQL — page in the database.
        if (AuthHelper.IsExternalUser(User))
        {
            var externalJobs = await Pagination.Apply(
                    query
                        .OrderBy(j => j.TaskType)
                        .ThenBy(j => j.Service)
                        .ThenBy(j => j.AccountHolder),
                    page,
                    pageSize)
                .ToListAsync();
            var externalResponses = externalJobs.Select(JobRequestMapper.ToResponse).ToList();
            await JobFormLinkService.EnrichWithFormLinksAsync(_context, externalResponses, User);
            return externalResponses;
        }

        // Internal: release/assignee visibility still needs in-memory helpers; page after filter.
        var jobs = await query
            .OrderBy(j => j.TaskType)
            .ThenBy(j => j.Service)
            .ThenBy(j => j.AccountHolder)
            .ToListAsync();

        var moisByJobId = await LoadMoisByJobIdAsync(jobs.Select(j => j.JobRequestId));

        // Package workboard (GET ?customerPackageId=…) is the full deliverables catalog —
        // admins must see seeded lines before the client issues MOI. The release gate applies
        // to the operational queue (dashboard / tracker), not this scoped package view.
        if (!(customerPackageId.HasValue && AuthHelper.IsAdmin(User)))
            jobs = InternalWorkVisibilityHelper.FilterJobsForInternal(jobs, moisByJobId);

        if (!AuthHelper.IsAdmin(User))
        {
            jobs = jobs
                .Where(j => TaskFormVisibilityHelper.CanInternalUserSeeJob(User, j))
                .ToList();
        }

        jobs = Pagination.ApplyInMemory(jobs, page, pageSize);
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

        var user = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId.Value);

        var units = await _context.JobRequestUnits
            .AsNoTracking()
            .Include(u => u.Assignees)
            .ThenInclude(a => a.User)
            .Include(u => u.JobRequest)
            .Where(u => u.Status != "Completed"
                && (u.AssignedUserId == userId.Value
                    || u.Assignees.Any(a => a.UserId == userId.Value)))
            .OrderBy(u => u.ScheduledDate ?? DateTime.MaxValue)
            .ThenBy(u => u.JobRequest.Customer)
            .ToListAsync();

        if (units.Count == 0)
            return Ok(Array.Empty<WorkTrackerItemDto>());

        var jobIds = units.Select(u => u.JobRequestId).Distinct().ToList();
        var moiForms = await _context.MOIForms
            .AsNoTracking()
            .Where(f => f.JobRequestId != null && jobIds.Contains(f.JobRequestId.Value))
            .ToListAsync();

        units = units
            .Where(u => InternalWorkVisibilityHelper.IsUnitReleasedToInternal(
                u.JobRequest,
                u,
                InternalWorkVisibilityHelper.ResolveMoiForUnit(
                    moiForms.Where(f => f.JobRequestId == u.JobRequestId),
                    u,
                    u.JobRequest)))
            .ToList();

        if (units.Count == 0)
            return Ok(Array.Empty<WorkTrackerItemDto>());

        jobIds = units.Select(u => u.JobRequestId).Distinct().ToList();
        if (jobIds.Count == 0)
            return Ok(Array.Empty<WorkTrackerItemDto>());

        var jobs = await _context.JobRequests
            .AsNoTracking()
            .Include(j => j.Units)
            .ThenInclude(u => u.Assignees)
            .Where(j => jobIds.Contains(j.JobRequestId))
            .ToDictionaryAsync(j => j.JobRequestId);

        var moaForms = await _context.MOAForms
            .AsNoTracking()
            .Where(f => f.JobRequestId != null && jobIds.Contains(f.JobRequestId.Value))
            .ToListAsync();

        var items = new List<WorkTrackerItemDto>(units.Count);
        foreach (var unit in units)
        {
            if (!jobs.TryGetValue(unit.JobRequestId, out var job))
                continue;

            var moi = ResolveMoiForUnit(moiForms, unit, job.TotalQty);
            var moa = ResolveMoaForUnit(moaForms, unit, job.TotalQty);

            var item = JobRequestUnitService.ToTrackerDto(unit, job);
            item.HasMoiForm = moi != null;
            item.HasMoaForm = moa != null;
            item.MoiFormId = moi?.MOIFormId;
            item.MoiWorkflowState = moi?.WorkflowState;
            item.RequiredExecutionDate = MoiFormMetadataHelper.ReadRequiredExecutionDate(moi);
            item.DocumentTitle = MoiFormMetadataHelper.ReadDocumentTitle(moi);

            var display = PackageItemStatusResolver.ResolveForUnit(job, unit, moi);
            item.DisplayStatus = display.Label;
            item.DisplayStatusKey = display.Key;

            if (moa != null && TaskFormVisibilityHelper.CanViewMoaForm(User, job, moa, moi))
            {
                item.LinkedFormKind = "MOA";
                item.LinkedFormId = moa.MOAFormId;
            }
            else if (!TaskFormVisibilityHelper.ShouldPreferMoaOverMoi(job, moi, unit.InternalHandoffStatus)
                && moi != null
                && TaskFormVisibilityHelper.CanViewMoiForm(User, job, moi))
            {
                item.LinkedFormKind = "MOI";
                item.LinkedFormId = moi.MOIFormId;
            }

            items.Add(item);
        }

        return items;
    }

    private static MOIForm? ResolveMoiForUnit(List<MOIForm> mois, JobRequestUnit unit, int totalQty)
    {
        var byUnit = mois.FirstOrDefault(f => f.JobRequestUnitId == unit.JobRequestUnitId);
        if (byUnit != null)
            return byUnit;

        return totalQty <= 1
            ? mois.FirstOrDefault(f => f.JobRequestUnitId == null)
            : null;
    }

    private static MOAForm? ResolveMoaForUnit(List<MOAForm> moas, JobRequestUnit unit, int totalQty)
    {
        var byUnit = moas.FirstOrDefault(f => f.JobRequestUnitId == unit.JobRequestUnitId);
        if (byUnit != null)
            return byUnit;

        return totalQty <= 1
            ? moas.FirstOrDefault(f => f.JobRequestUnitId == null)
            : null;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<JobRequestResponse>> GetJobRequest(int id)
    {
        var job = await JobQuery().FirstOrDefaultAsync(j => j.JobRequestId == id);
        if (job == null)
            return NotFound();

        if (!AuthHelper.CanAccessJob(User, job))
            return Forbid();

        var response = JobRequestMapper.ToResponse(job);
        await JobFormLinkService.EnrichWithFormLinksAsync(_context, [response], User);
        return response;
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

    [HttpPost("{id}/assign-secretarial-team")]
    public async Task<ActionResult<JobRequestResponse>> AssignSecretarialTeam(int id)
    {
        if (!AuthHelper.IsAdmin(User) && !AuthHelper.CanApproveMoi(User))
            return Forbid();

        try
        {
            var job = await JobRequestAssignmentService.AssignSecretarialTeamAsync(_context, id);
            var response = JobRequestMapper.ToResponse(job);
            await JobFormLinkService.EnrichWithFormLinksAsync(_context, [response], User);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/approve-intake")]
    public async Task<ActionResult<JobRequestResponse>> ApproveMoiIntake(int id, [FromQuery] int? unitNumber = null)
    {
        if (!AuthHelper.CanApproveMoiIntake(User) && !AuthHelper.CanApproveMoi(User))
            return Forbid();

        var job = await JobQuery().FirstOrDefaultAsync(j => j.JobRequestId == id);
        if (job == null) return NotFound();

        if (!await IsAwaitingIntakeAsync(job, unitNumber))
            return BadRequest(new { message = "This task is not awaiting MOI intake approval." });

        await JobHandoffService.OnMoiIntakeApprovedAsync(_context, job, unitNumber);
        job = await JobQuery().FirstAsync(j => j.JobRequestId == id);
        var response = JobRequestMapper.ToResponse(job);
        await JobFormLinkService.EnrichWithFormLinksAsync(_context, [response], User);
        return Ok(response);
    }

    [HttpPost("{id}/reject-intake")]
    public async Task<ActionResult<JobRequestResponse>> RejectMoiIntake(
        int id,
        RejectFormRequest request,
        [FromQuery] int? unitNumber = null)
    {
        if (!AuthHelper.CanApproveMoiIntake(User))
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { message = "A rejection reason is required." });

        var job = await JobQuery().FirstOrDefaultAsync(j => j.JobRequestId == id);
        if (job == null) return NotFound();

        if (!await IsAwaitingIntakeAsync(job, unitNumber))
            return BadRequest(new { message = "This task is not awaiting MOI intake approval." });

        var moiForm = await ResolveMoiFormForIntakeAsync(job, unitNumber);
        if (moiForm == null)
            return BadRequest(new { message = "No MOI form found for this task." });

        var user = await _context.Users.FindAsync(AuthHelper.CurrentUserId(User) ?? 0);
        if (user == null) return Unauthorized();

        await JobHandoffService.OnMoiIntakeRejectedAsync(_context, job, moiForm, user, request.Reason);
        job = await JobQuery().FirstAsync(j => j.JobRequestId == id);
        var response = JobRequestMapper.ToResponse(job);
        await JobFormLinkService.EnrichWithFormLinksAsync(_context, [response], User);
        return Ok(response);
    }

    [HttpPost("{id}/assign")]
    public async Task<IActionResult> AssignJob(int id, AssignJobRequest request)
    {
        if (!AuthHelper.IsAdmin(User) && !AuthHelper.CanApproveMoi(User))
            return Forbid();

        var job = await JobQuery().FirstOrDefaultAsync(j => j.JobRequestId == id);
        if (job == null)
            return NotFound();

        var user = await _context.Users.FindAsync(request.UserId);
        if (user == null)
            return BadRequest(new { message = "Selected user was not found." });

        if (!SecretarialStaffService.IsAssignableInternalStaff(user))
            return BadRequest(new { message = "Only internal secretarial staff or internal admins can be assigned to jobs." });

        return await TransactionHelper.ExecuteInTransactionAsync(_context, async () =>
        {
            await JobRequestUnitService.SyncUnitsForJobAsync(_context, job);
            job = await JobQuery().FirstAsync(j => j.JobRequestId == id);

            var unit = JobRequestUnitService.ResolveUnit(job, request.UnitNumber);
            if (unit == null)
                return (false, (IActionResult)BadRequest(new { message = "No unit available to assign." }));

            var moiForms = await _context.MOIForms
                .Where(f => f.JobRequestId == id)
                .ToListAsync();
            var moi = ResolveMoiForUnit(moiForms, unit, job.TotalQty);
            var previousUnit = job.Units.FirstOrDefault(u => u.UnitNumber == unit.UnitNumber - 1);
            var previousMoi = previousUnit != null
                ? ResolveMoiForUnit(moiForms, previousUnit, job.TotalQty)
                : null;
            if (!UnitAssignmentGate.CanAssignUnit(job, unit, job.Units.ToList(), moi, previousMoi))
            {
                return (false, BadRequest(new
                {
                    message = "This session is not ready for team assignment yet. Complete the prior session's MOI first.",
                }));
            }

            if (request.Remove)
                await JobRequestUnitService.RemoveAssigneeAsync(_context, unit, user.UserId, job);
            else
            {
                await JobRequestUnitService.AddAssigneeAsync(_context, unit, user);
                await JobHandoffService.OnSecretarialStaffAssignedToUnitAsync(_context, job, unit, moi);
            }

            if (!string.IsNullOrWhiteSpace(request.Comments))
                job.AssignmentComments = request.Comments;

            if (DateOnlyHelper.Parse(request.AcceptedDate) is { } accepted)
                job.DateRequested = accepted;

            await JobRequestUnitService.RefreshJobAggregateAsync(_context, job);
            await JobRequestUnitService.SyncUnitToTrackerAsync(_context, unit, job);
            await JobHandoffService.OnJobAcceptedAsync(_context, job);
            await _context.SaveChangesAsync();

            job = await JobQuery().FirstAsync(j => j.JobRequestId == id);
            return (true, (IActionResult)Ok(JobRequestMapper.ToResponse(job)));
        });
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
            {
                if (!isAdmin && !AuthHelper.CanAccessJob(User, job))
                    return Forbid();

                var submitUnitNumber = await ResolveMoaHandoffUnitNumberAsync(job, request.UnitNumber);
                if (job.TotalQty > 1 && !submitUnitNumber.HasValue)
                    return BadRequest(new { message = "unitNumber is required for multi-session MOA submission." });

                var moaForSubmit = await ResolveMoaFormForHandoffAsync(job, submitUnitNumber);
                if (moaForSubmit == null)
                    return BadRequest(new { message = "Save the MOA draft before submitting for admin approval." });

                var submitUnit = JobHandoffResolver.ResolveUnit(job, submitUnitNumber, moaForSubmit);
                MOIForm? linkedMoi = moaForSubmit.MOIFormId.HasValue
                    ? await _context.MOIForms.FindAsync(moaForSubmit.MOIFormId.Value)
                    : null;
                if (!JobHandoffResolver.IsMoaDraftSubmittable(job, submitUnit, moaForSubmit, linkedMoi))
                    return BadRequest(new { message = "MOA draft can only be submitted while preparation is in progress." });

                var (submitValid, submitErrors) = MoaPackChecklistService.Validate(moaForSubmit);
                if (!submitValid)
                    return BadRequest(new { message = "Complete the MOA pack checklist before submitting.", errors = submitErrors });

                await JobHandoffService.OnMoaSubmittedForAdminReviewAsync(_context, job, moaForSubmit, submitUnit);
                break;
            }
            case "sharon-approve-moa":
            {
                if (!AuthHelper.CanApproveMoa(User)) return Forbid();
                var approveUnitNumber = await ResolveMoaHandoffUnitNumberAsync(job, request.UnitNumber);
                if (job.TotalQty > 1 && !approveUnitNumber.HasValue)
                    return BadRequest(new { message = "unitNumber is required for multi-session MOA approval." });

                var moaForm = await ResolveMoaFormForHandoffAsync(job, approveUnitNumber);
                if (moaForm == null)
                    return BadRequest(new { message = "No MOA form found for this task." });

                var (packValid, packErrors) = MoaPackChecklistService.Validate(moaForm);
                if (!packValid)
                    return BadRequest(new { message = "MOA pack checklist incomplete.", errors = packErrors });

                var approveUnit = JobHandoffResolver.ResolveUnit(job, approveUnitNumber, moaForm);
                await JobHandoffService.OnSharonMoaApprovedAsync(_context, job, moaForm, approveUnit);
                break;
            }
            case "approve-for-moa":
            {
                if (!isAdmin && !AuthHelper.CanApproveMoa(User))
                    return Forbid();
                var releaseUnitNumber = await ResolveMoaHandoffUnitNumberAsync(job, request.UnitNumber);
                if (job.TotalQty > 1 && !releaseUnitNumber.HasValue)
                    return BadRequest(new { message = "unitNumber is required for multi-session MOA release." });

                var moaForRelease = await ResolveMoaFormForHandoffAsync(job, releaseUnitNumber);
                var releaseUnit = JobHandoffResolver.ResolveUnit(job, releaseUnitNumber, moaForRelease);
                await JobHandoffService.AdvanceToReadyForMoaAsync(_context, job, releaseUnit, moaForRelease, _notifier);
                job = await JobQuery().FirstAsync(j => j.JobRequestId == id);
                return Ok(JobRequestMapper.ToResponse(job));
            }
            case "reject-moa":
            {
                if (!AuthHelper.CanApproveMoa(User) && !isAdmin)
                    return Forbid();
                var rejectUnitNumber = await ResolveMoaHandoffUnitNumberAsync(job, request.UnitNumber);
                if (job.TotalQty > 1 && !rejectUnitNumber.HasValue)
                    return BadRequest(new { message = "unitNumber is required for multi-session MOA rejection." });

                var moaToReject = await ResolveMoaFormForHandoffAsync(job, rejectUnitNumber);
                if (moaToReject == null)
                    return BadRequest(new { message = "No MOA form found for this task." });

                var rejectUnit = JobHandoffResolver.ResolveUnit(job, rejectUnitNumber, moaToReject);
                var rejectHandoff = JobHandoffResolver.ResolveEffectiveHandoff(job, rejectUnit, moaToReject);
                if (rejectHandoff != JobHandoffStatuses.AdminReview)
                    return BadRequest(new { message = "MOA can only be rejected by Sharon while awaiting head secretary review." });

                var rejectUser = await _context.Users.FindAsync(AuthHelper.CurrentUserId(User) ?? 0);
                if (rejectUser == null) return Unauthorized();
                if (string.IsNullOrWhiteSpace(request.Comments))
                    return BadRequest(new { message = "A rejection reason is required." });
                await JobHandoffService.OnMoaSharonRejectedAsync(
                    _context, job, moaToReject, rejectUser, request.Comments!, rejectUnit);
                break;
            }
            default:
                return BadRequest(new { message = "Unknown handoff action." });
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

        return await TransactionHelper.ExecuteInTransactionAsync(_context, async () =>
        {
            await JobRequestUnitService.SyncUnitsForJobAsync(_context, job);
            job = await JobQuery().FirstAsync(j => j.JobRequestId == id);

            var unitNumber = request.UnitNumber ?? (job.TotalQty == 1 ? 1 : null);
            if (!unitNumber.HasValue)
            {
                return (false, (IActionResult)BadRequest(new
                {
                    message = "Unit number is required for multi-quantity items.",
                }));
            }

            var unit = job.Units.FirstOrDefault(u => u.UnitNumber == unitNumber.Value);
            if (unit == null)
                return (false, BadRequest(new { message = "Unit not found." }));

            var isAdmin = AuthHelper.IsAdmin(User);
            var actorId = AuthHelper.CurrentUserId(User);

            if (!isAdmin)
            {
                if (!actorId.HasValue || !JobRequestUnitService.IsUserAssigned(unit, actorId.Value))
                    return (false, (IActionResult)Forbid());
            }

            if (request.UserId.HasValue && isAdmin)
            {
                var assignUser = await _context.Users.FindAsync(request.UserId.Value);
                if (assignUser == null)
                    return (false, BadRequest(new { message = "Selected user was not found." }));
                await JobRequestUnitService.AddAssigneeAsync(_context, unit, assignUser);
            }

            if (request.ScheduledDate is not null)
            {
                if (!AuthHelper.IsClientAdmin(User))
                {
                    return (false, BadRequest(new
                    {
                        message = "Scheduled dates are set by the client company.",
                    }));
                }

                unit.ScheduledDate = string.IsNullOrWhiteSpace(request.ScheduledDate)
                    ? null
                    : DateOnlyHelper.Parse(request.ScheduledDate);
                await JobRequestUnitService.SyncUnitToTrackerAsync(_context, unit, job);
            }

            if (request.MarkUnitComplete && request.MarkUnitIncomplete)
            {
                return (false, BadRequest(new
                {
                    message = "Cannot mark a unit complete and incomplete in the same request.",
                }));
            }

            var handoff = JobHandoffResolver.ResolveEffectiveHandoff(job, unit);
            if (request.MarkUnitComplete || request.MarkUnitIncomplete)
            {
                if (handoff is JobHandoffStatuses.ReadyForMoa or JobHandoffStatuses.MoaCirculation)
                {
                    return (false, BadRequest(new
                    {
                        message = "This item must be completed via MOA sign-off, not manual progress.",
                    }));
                }
            }

            if (request.MarkUnitComplete)
            {
                if (handoff is JobHandoffStatuses.PendingExecute or JobHandoffStatuses.ExecutionSecComplete)
                {
                    if (!isAdmin && !AuthHelper.CanApproveMoa(User))
                        return (false, (IActionResult)Forbid());

                    var moaForm = await ResolveMoaFormForHandoffAsync(job, unitNumber);
                    await JobHandoffService.OnExecutionCompletedAsync(_context, job, unit, moaForm);
                }
                else
                {
                    // R3: AdminBypass never had prep assignees — attribute completion to the acting admin
                    var unitMode = !string.IsNullOrWhiteSpace(unit.WorkflowMode)
                        ? unit.WorkflowMode
                        : job.WorkflowMode;
                    if (JobWorkflowModes.IsAdminBypass(unitMode)
                        && string.IsNullOrWhiteSpace(job.JobAssignedTo))
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
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }
            else if (request.MarkUnitIncomplete)
            {
                if (unit.Status != "Completed")
                {
                    return (false, BadRequest(new
                    {
                        message = "Only completed units can be reverted.",
                    }));
                }

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
            var progressResponse = JobRequestMapper.ToResponse(job);
            await JobFormLinkService.EnrichWithFormLinksAsync(_context, [progressResponse], User);
            return (true, (IActionResult)Ok(progressResponse));
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateJobRequest(int id, JobRequestRequest request)
    {
        var job = await JobQuery().FirstOrDefaultAsync(j => j.JobRequestId == id);
        if (job == null)
            return NotFound();

        // Full-field job updates are admin-only (C1) — clients/staff must use workflow endpoints.
        if (!AuthHelper.IsAdmin(User))
            return Forbid();

        var allowedStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Pending", "In Progress", "Completed", "Canceled",
        };
        if (string.IsNullOrWhiteSpace(request.Status) || !allowedStatuses.Contains(request.Status))
            return BadRequest(new { message = "Status must be Pending, In Progress, Completed, or Canceled." });

        if (request.TotalQty < 1)
            return BadRequest(new { message = "TotalQty must be at least 1." });
        if (request.TotalQty < request.UsedQty)
            return BadRequest(new { message = "TotalQty cannot be less than UsedQty." });

        var previousStatus = job.Status;
        JobRequestMapper.ApplyRequest(job, request, isAdmin: true);

        if (request.Status is "Completed" or "Canceled" && previousStatus != request.Status)
        {
            job.DateCompleted = DateTime.UtcNow;
            if (request.Status == "Completed" && job.UsedQty < job.TotalQty)
                job.UsedQty = job.TotalQty;

            var alreadyRecorded = request.Status == "Completed"
                && await _context.CompletedServices.AnyAsync(c =>
                    c.JobRequestId == job.JobRequestId && c.Status == "Completed");
            if (!alreadyRecorded)
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
                    Status = request.Status,
                    CreatedAt = DateTime.UtcNow
                });
            }
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

    private async Task<bool> IsAwaitingIntakeAsync(JobRequest job, int? unitNumber)
    {
        if (unitNumber.HasValue && job.TotalQty > 1)
        {
            var unit = job.Units.FirstOrDefault(u => u.UnitNumber == unitNumber.Value);
            if (unit == null)
                return false;

            var moi = await ResolveMoiFormForIntakeAsync(job, unitNumber);
            return TaskFormVisibilityHelper.UnitAwaitingIntakeApproval(unit, moi);
        }

        var moiForm = await ResolveMoiFormForIntakeAsync(job, unitNumber);
        return TaskFormVisibilityHelper.AwaitingIntakeApproval(job, moiForm);
    }

    private async Task<MOIForm?> ResolveMoiFormForIntakeAsync(JobRequest job, int? unitNumber)
    {
        if (unitNumber.HasValue && job.TotalQty > 1)
        {
            var unit = job.Units.FirstOrDefault(u => u.UnitNumber == unitNumber.Value);
            if (unit == null)
                return null;

            return await _context.MOIForms
                .Where(f => f.JobRequestId == job.JobRequestId && f.JobRequestUnitId == unit.JobRequestUnitId)
                .OrderByDescending(f => f.UpdatedAt)
                .FirstOrDefaultAsync();
        }

        return await _context.MOIForms
            .Where(f => f.JobRequestId == job.JobRequestId)
            .OrderByDescending(f => f.UpdatedAt)
            .FirstOrDefaultAsync();
    }

    private async Task<int?> ResolveMoaHandoffUnitNumberAsync(JobRequest job, int? requestedUnitNumber)
    {
        if (requestedUnitNumber.HasValue)
            return requestedUnitNumber;
        if (job.TotalQty <= 1)
            return 1;

        var moa = await _context.MOAForms
            .Where(f => f.JobRequestId == job.JobRequestId && f.JobRequestUnitId != null)
            .OrderByDescending(f => f.UpdatedAt)
            .FirstOrDefaultAsync();
        if (moa?.JobRequestUnitId is int unitId)
            return job.Units.FirstOrDefault(u => u.JobRequestUnitId == unitId)?.UnitNumber;

        return null;
    }

    private async Task<MOAForm?> ResolveMoaFormForHandoffAsync(JobRequest job, int? unitNumber = null)
    {
        // S5: never fall through to another session's MOA when a unitNumber was requested
        if (unitNumber.HasValue && job.TotalQty > 1)
        {
            var unit = job.Units.FirstOrDefault(u => u.UnitNumber == unitNumber.Value);
            if (unit == null)
                return null;

            return await _context.MOAForms
                .Where(f => f.JobRequestId == job.JobRequestId && f.JobRequestUnitId == unit.JobRequestUnitId)
                .OrderByDescending(f => f.UpdatedAt)
                .FirstOrDefaultAsync();
        }

        return await _context.MOAForms
            .Where(f => f.JobRequestId == job.JobRequestId)
            .OrderByDescending(f => f.UpdatedAt)
            .FirstOrDefaultAsync();
    }

    private async Task<Dictionary<int, List<MOIForm>>> LoadMoisByJobIdAsync(IEnumerable<int> jobIds)
    {
        var ids = jobIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var mois = await _context.MOIForms
            .AsNoTracking()
            .Where(f => f.JobRequestId != null && ids.Contains(f.JobRequestId.Value))
            .ToListAsync();

        return mois
            .GroupBy(f => f.JobRequestId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}
