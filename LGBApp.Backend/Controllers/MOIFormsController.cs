using System.Security.Claims;
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
public class MOIFormsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly WorkflowNotifier _notifier;

    public MOIFormsController(AppDbContext context, WorkflowNotifier notifier)
    {
        _context = context;
        _notifier = notifier;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FormResponse>>> GetForms(
        [FromQuery] int? jobId,
        [FromQuery] int? unitNumber)
    {
        var query = _context.MOIForms.AsQueryable();
        if (jobId.HasValue)
            query = query.Where(f => f.JobRequestId == jobId.Value);

        if (jobId.HasValue && unitNumber.HasValue)
        {
            var jobMeta = await _context.JobRequests
                .Where(j => j.JobRequestId == jobId)
                .Select(j => new { j.TotalQty })
                .FirstOrDefaultAsync();

            var unitId = await _context.JobRequestUnits
                .Where(u => u.JobRequestId == jobId && u.UnitNumber == unitNumber)
                .Select(u => (int?)u.JobRequestUnitId)
                .FirstOrDefaultAsync();

            if (unitId.HasValue)
            {
                query = jobMeta is { TotalQty: > 1 }
                    ? query.Where(f => f.JobRequestUnitId == unitId)
                    : query.Where(f => f.JobRequestUnitId == unitId || f.JobRequestUnitId == null);
            }
            else
            {
                query = query.Where(f => false);
            }
        }

        query = await FormAccessHelper.ScopeMoiFormsAsync(_context, User, query);
        var forms = await query.OrderByDescending(f => f.UpdatedAt).ToListAsync();
        var byCompany = await WorkflowService.ResolveCustomersByCompanyAsync(
            _context, forms.Select(f => f.Company));
        var byId = forms
            .Where(f => f.CustomerId.HasValue)
            .Select(f => f.CustomerId!.Value)
            .Distinct()
            .ToList();
        var customersById = byId.Count == 0
            ? new Dictionary<int, Customer>()
            : await _context.Customers.Include(c => c.AccountHolders)
                .Where(c => byId.Contains(c.CustomerId))
                .ToDictionaryAsync(c => c.CustomerId);

        var results = new List<FormResponse>();
        foreach (var form in forms)
        {
            Customer? customer = null;
            if (form.CustomerId.HasValue)
                customersById.TryGetValue(form.CustomerId.Value, out customer);
            customer ??= byCompany.GetValueOrDefault(form.Company);
            if (customer != null && form.CustomerId == null)
                form.CustomerId = customer.CustomerId;
            results.Add(FormMapper.ToMoiResponse(form, customer: customer));
        }
        return results;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FormResponse>> GetForm(int id)
    {
        var form = await _context.MOIForms.FindAsync(id);
        if (form == null) return NotFound();
        if (!await FormAccessHelper.CanAccessMoiFormAsync(_context, User, form))
            return Forbid();
        var customer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, form.Company);
        return FormMapper.ToMoiResponse(form, customer: customer);
    }

    [HttpPost]
    public async Task<ActionResult<FormResponse>> CreateForm(FormRequest request)
    {
        JobRequest? job = null;
        JobRequestUnit? unit = null;

        if (request.JobId.HasValue)
        {
            job = await _context.JobRequests
                .Include(j => j.Units)
                .FirstOrDefaultAsync(j => j.JobRequestId == request.JobId.Value);
            if (job == null) return NotFound();
            if (AuthHelper.IsExternalUser(User)
                && !AuthHelper.CanManageClientJob(User, job)
                && !AuthHelper.IsSignatoryForJob(User, job)
                && !await AuthHelper.CanSignatoryIssueMoiAsync(_context, User, job))
                return Forbid();

            await JobRequestUnitService.SyncUnitsForJobAsync(_context, job);
            await _context.Entry(job).Collection(j => j.Units).LoadAsync();

            var unitNumber = MoiFormService.ResolveUnitNumber(request);
            unit = MoiFormService.ResolveUnit(job, unitNumber ?? (job.TotalQty <= 1 ? 1 : null));
            if (job.TotalQty > 1 && unit == null)
                return BadRequest(new { message = "unitNumber is required — each session needs its own MOI." });

            var existing = await MoiFormService.FindForUnitAsync(
                _context, job.JobRequestId, unit?.JobRequestUnitId, job.TotalQty <= 1);
            if (existing != null)
            {
                var applyConflict = await ApplyFormRequestAsync(existing, request, job, unit);
                if (applyConflict != null) return applyConflict;
                var linkedCustomer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, existing.Company);
                return Ok(FormMapper.ToMoiResponse(existing, customer: linkedCustomer));
            }
        }
        else
        {
            // S6: floating MOI (no job) requires staff, or external access to that company
            if (!AuthHelper.IsInternalStaff(User))
            {
                var companyCustomer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, request.Company);
                if (companyCustomer == null
                    || !AuthHelper.CanAccessCustomer(User, companyCustomer.CustomerId))
                    return Forbid();
            }
        }

        var customer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, request.Company);
        DivisionGroup? group = null;
        if (customer != null && !string.IsNullOrWhiteSpace(customer.DivisionGroupCode))
            group = await _context.DivisionGroups.FirstOrDefaultAsync(g => g.Code == customer.DivisionGroupCode);

        var formData = new Dictionary<string, object?>(request.Data);
        if (request.JobId.HasValue)
            formData["jobId"] = request.JobId.Value;
        if (job != null)
            MoiFormService.StampUnitMetadata(formData, job, unit);

        var serviceName = request.Data.GetValueOrDefault("service")?.ToString()
            ?? request.Data.GetValueOrDefault("typeOfDocument")?.ToString()
            ?? job?.Service;
        var templateCode = request.FormTemplateCode
            ?? await WorkflowService.ResolveMoiTemplateCodeAsync(_context, customer, group, serviceName);

        var workflowState = AuthHelper.IsExternalUser(User)
            ? MoiWorkflowStates.Draft
            : DetermineInitialState(customer, formData);

        var form = new MOIForm
        {
            JobRequestId = request.JobId,
            JobRequestUnitId = unit?.JobRequestUnitId,
            CustomerId = customer?.CustomerId ?? job?.CustomerId,
            Company = request.Company,
            FormDataJson = JsonHelper.Serialize(formData),
            FormTemplateCode = templateCode,
            WorkflowState = workflowState,
            FinanceRelated = request.FinanceRelated,
            BankSignatoryMatter = request.BankSignatoryMatter,
            SchemaVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _context.MOIForms.Add(form);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetForm), new { id = form.MOIFormId }, FormMapper.ToMoiResponse(form, customer: customer));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateForm(int id, FormRequest request)
    {
        var form = await _context.MOIForms.FindAsync(id);
        if (form == null) return NotFound();

        if (!await FormAccessHelper.CanAccessMoiFormAsync(_context, User, form))
            return Forbid();

        var conflict = FormConcurrencyHelper.CheckExpectedUpdatedAt(form.UpdatedAt, request.ExpectedUpdatedAt);
        if (conflict != null) return conflict;

        if (AuthHelper.IsExternalUser(User))
        {
            if (form.WorkflowState is "PendingRecommendation" or "Recommended" or "PendingMoiApproval")
                form.WorkflowState = MoiWorkflowStates.Draft;

            if (form.WorkflowState is not (MoiWorkflowStates.Draft or MoiWorkflowStates.MoiRejected))
                return BadRequest(new { message = "MOI can only be edited while in Draft or after rejection." });

            if (form.JobRequestId.HasValue)
            {
                var linkedJob = await _context.JobRequests.FindAsync(form.JobRequestId.Value);
                if (linkedJob != null
                    && !TaskFormVisibilityHelper.CanEditMoiForm(User, linkedJob, form.WorkflowState))
                    return Forbid();
            }
            else
            {
                var linkedCustomer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, form.Company);
                if (!AuthHelper.CanAccessCustomer(User, linkedCustomer?.CustomerId))
                    return Forbid();
            }
        }

        var job = form.JobRequestId.HasValue
            ? await _context.JobRequests.Include(j => j.Units).FirstOrDefaultAsync(j => j.JobRequestId == form.JobRequestId)
            : null;
        JobRequestUnit? unit = null;
        if (job != null)
        {
            var unitNumber = MoiFormService.ResolveUnitNumber(request);
            unit = form.JobRequestUnitId.HasValue
                ? job.Units.FirstOrDefault(u => u.JobRequestUnitId == form.JobRequestUnitId)
                : MoiFormService.ResolveUnit(job, unitNumber ?? (job.TotalQty <= 1 ? 1 : null));
            if (unit != null && !form.JobRequestUnitId.HasValue)
                form.JobRequestUnitId = unit.JobRequestUnitId;
        }

        var applyConflict = await ApplyFormRequestAsync(form, request, job, unit);
        if (applyConflict != null) return applyConflict;
        return NoContent();
    }

    private async Task<ActionResult?> ApplyFormRequestAsync(
        MOIForm form,
        FormRequest request,
        JobRequest? job,
        JobRequestUnit? unit)
    {
        var formData = new Dictionary<string, object?>(request.Data);
        if (request.JobId.HasValue)
            formData["jobId"] = request.JobId.Value;
        if (job != null)
            MoiFormService.StampUnitMetadata(formData, job, unit);

        form.JobRequestId = request.JobId ?? form.JobRequestId;
        if (unit != null)
            form.JobRequestUnitId = unit.JobRequestUnitId;
        form.Company = request.Company;
        form.FormDataJson = JsonHelper.Serialize(formData);
        form.FinanceRelated = request.FinanceRelated;
        form.BankSignatoryMatter = request.BankSignatoryMatter;
        if (!string.IsNullOrWhiteSpace(request.FormTemplateCode))
            form.FormTemplateCode = request.FormTemplateCode;

        if (form.WorkflowState == MoiWorkflowStates.PendingPrep
            && AuthHelper.IsInternalStaff(User)
            && form.JobRequestId.HasValue
            && job != null
            && !TaskFormVisibilityHelper.AwaitingIntakeApproval(job, form))
            form.WorkflowState = MoiWorkflowStates.PendingRecommendation;

        var previousUpdatedAt = form.UpdatedAt;
        form.UpdatedAt = DateTime.UtcNow;

        if (job != null)
        {
            if (unit != null && job.TotalQty > 1
                && unit.InternalHandoffStatus == JobHandoffStatuses.ClientSubmitted)
                unit.InternalHandoffStatus = JobHandoffStatuses.PendingPrep;
            else if (job.InternalHandoffStatus == JobHandoffStatuses.ClientSubmitted)
                JobHandoffService.SetHandoff(job, JobHandoffStatuses.PendingPrep);
        }

        return await FormConcurrencyHelper.SaveWithConcurrencyAsync(_context, previousUpdatedAt);
    }

    [HttpPost("{id}/submit-for-approval")]
    public async Task<ActionResult<FormResponse>> SubmitForApproval(int id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var form = await _context.MOIForms.FindAsync(id);
        if (form == null) return NotFound();

        // N4: authz before state (hide existence from other tenants)
        if (!await FormAccessHelper.CanAccessMoiFormAsync(_context, User, form))
            return NotFound();

        if (form.WorkflowState is not (MoiWorkflowStates.Draft or MoiWorkflowStates.MoiRejected))
            return BadRequest(new { message = "MOI can only be submitted for approval from Draft or after rejection." });

        if (!form.JobRequestId.HasValue)
            return BadRequest(new { message = "MOI must be linked to a job." });

        var job = await _context.JobRequests.FindAsync(form.JobRequestId.Value);
        if (job == null) return NotFound();

        if (!AuthHelper.CanManageClientJob(User, job)
            && !AuthHelper.IsSignatoryForJob(User, job)
            && !await AuthHelper.CanSignatoryIssueMoiAsync(_context, User, job))
            return Forbid();

        var formData = JsonHelper.Deserialize<Dictionary<string, object?>>(form.FormDataJson);
        if (FormDataHelper.IsTruthy(formData.GetValueOrDefault("supportingDocument")))
        {
            var docQuery = _context.JobItemDocuments
                .Where(d => d.JobRequestId == job.JobRequestId
                    && d.Folder == "supporting");
            if (form.JobRequestUnitId.HasValue && job.TotalQty > 1)
                docQuery = docQuery.Where(d => d.JobRequestUnitId == form.JobRequestUnitId);
            else if (form.JobRequestUnitId.HasValue)
                docQuery = docQuery.Where(d =>
                    d.JobRequestUnitId == form.JobRequestUnitId || d.JobRequestUnitId == null);

            if (!await docQuery.AnyAsync())
                return BadRequest(new { message = "Attach at least one supporting document before submitting for approval." });
        }

        var customer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, form.Company);
        if (customer == null) return BadRequest(new { message = "Customer not found." });

        await JobHandoffService.OnMoiSubmittedForApprovalAsync(_context, job, form, customer, _notifier);
        return FormMapper.ToMoiResponse(form, customer: customer);
    }

    [HttpPost("{id}/client-approve")]
    public async Task<ActionResult<FormResponse>> ClientApprove(int id, ClientApproveRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        if (!AuthHelper.IsExternalUser(User))
            return Forbid();

        var form = await _context.MOIForms.FindAsync(id);
        if (form == null) return NotFound();

        // N4: tenant/access before state oracle
        var customer = await WorkflowService.ResolveCustomerForMoiAsync(_context, form);
        if (customer == null || !AuthHelper.CanAccessCustomer(User, customer.CustomerId))
            return NotFound();

        if (form.WorkflowState != MoiWorkflowStates.PendingClientMoiApproval)
            return BadRequest(new { message = "MOI is not awaiting client approval." });

        var holder = ClientApprovalService.FindMoiApprovalHolderForUser(customer, user);
        if (holder == null)
            return Forbid();

        var signatureError = SignaturePolicy.ValidateRequired(request.SignatureDataUrl, request.SignatureFileName);
        if (signatureError != null) return signatureError;

        var holderName = holder.Name.Trim();

        var records = ClientApprovalService.ParseMoi(form);
        if (ClientApprovalService.HasSigned(records, holder))
            return BadRequest(new { message = "You have already approved this MOI." });

        records.Add(new ClientApprovalRecord
        {
            UserId = user.UserId,
            AccountHolderName = holderName,
            Comments = request.Comments ?? string.Empty,
            SignatureFileName = request.SignatureFileName,
            SignatureDataUrl = request.SignatureDataUrl,
            SignedAt = DateTime.UtcNow,
        });
        ClientApprovalService.SaveMoi(form, records);

        if (!form.JobRequestId.HasValue)
            return BadRequest(new { message = "MOI must be linked to a job." });

        var job = await _context.JobRequests.FindAsync(form.JobRequestId.Value);
        if (job == null) return NotFound();

        await JobHandoffService.OnClientMoiApprovalRecordedAsync(_context, job, form, customer, _notifier);
        return FormMapper.ToMoiResponse(form, customer: customer);
    }

    [HttpPost("{id}/client-reject")]
    public async Task<ActionResult<FormResponse>> ClientReject(int id, RejectFormRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        if (!AuthHelper.IsExternalUser(User))
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { message = "A rejection reason is required." });

        var form = await _context.MOIForms.FindAsync(id);
        if (form == null) return NotFound();

        // N4: tenant/access before state oracle
        var customer = await WorkflowService.ResolveCustomerForMoiAsync(_context, form);
        if (customer == null || !AuthHelper.CanAccessCustomer(User, customer.CustomerId))
            return NotFound();

        if (form.WorkflowState != MoiWorkflowStates.PendingClientMoiApproval)
            return BadRequest(new { message = "MOI is not awaiting client approval." });

        var holder = ClientApprovalService.FindMoiApprovalHolderForUser(customer, user);
        if (holder == null)
            return Forbid();

        if (!form.JobRequestId.HasValue)
            return BadRequest(new { message = "MOI must be linked to a job." });

        var job = await _context.JobRequests.FindAsync(form.JobRequestId.Value);
        if (job == null) return NotFound();

        await JobHandoffService.OnMoiClientRejectedAsync(_context, job, form, user, request.Reason);
        return FormMapper.ToMoiResponse(form, customer: customer);
    }

    [HttpPost("{id}/recommend")]
    public async Task<ActionResult<FormResponse>> Recommend(int id, RecommendMoiRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var form = await _context.MOIForms.FindAsync(id);
        if (form == null) return NotFound();

        // N4: access before state (external users get 404, not a workflow oracle)
        if (!await FormAccessHelper.CanAccessMoiFormAsync(_context, User, form))
            return NotFound();

        // S7: recommend only from prep (backtest) or already-recommended (idempotent update)
        if (form.WorkflowState is not (MoiWorkflowStates.PendingPrep or MoiWorkflowStates.PendingRecommendation))
        {
            return BadRequest(new
            {
                message = $"MOI can only be recommended from PendingPrep or PendingRecommendation (current: '{form.WorkflowState}').",
            });
        }

        var customer = await WorkflowService.ResolveCustomerForMoiAsync(_context, form);
        if (customer == null) return BadRequest(new { message = "Customer not found for company." });

        var isAdmin = AuthHelper.IsAdmin(User);
        JobRequest? job = null;
        if (form.JobRequestId.HasValue)
        {
            job = await _context.JobRequests
                .Include(j => j.Units)
                .ThenInclude(u => u.Assignees)
                .FirstOrDefaultAsync(j => j.JobRequestId == form.JobRequestId.Value);
        }

        if (!await WorkflowService.CanRecommendMoiAsync(_context, user, customer, isAdmin, job))
            return Forbid();

        form.RecommendedByUserId = user.UserId;
        form.RecommendedAt = DateTime.UtcNow;
        form.RecommendationComments = request.Comments;
        form.UpdatedAt = DateTime.UtcNow;
        await JobHandoffService.OnMoiRecommendedAsync(_context, form);

        return FormMapper.ToMoiResponse(form, customer: customer);
    }

    [HttpPost("{id}/approve")]
    public async Task<ActionResult<FormResponse>> ApproveMoi(int id, ApproveWorkflowStepRequest request)
    {
        if (!AuthHelper.CanApproveMoi(User))
            return Forbid();

        var form = await _context.MOIForms.FindAsync(id);
        if (form == null) return NotFound();

        // C4: Sharon MOI sign-off only from states that await intake/sign-off.
        // Use admin-override for deliberate escapes from Draft/other states.
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            MoiWorkflowStates.PendingAdminIntake,
            MoiWorkflowStates.PendingRecommendation,
        };
        if (!allowed.Contains(form.WorkflowState ?? string.Empty))
        {
            return BadRequest(new
            {
                message = $"MOI cannot be approved from state '{form.WorkflowState}'. "
                    + "It must be PendingAdminIntake or PendingRecommendation (use admin-override if needed).",
            });
        }

        await JobHandoffService.OnMoiApprovedAsync(_context, form);
        var customer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, form.Company);
        return FormMapper.ToMoiResponse(form, customer: customer);
    }

    [HttpPost("{id}/admin-override")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<FormResponse>> AdminOverride(int id, RecommendMoiRequest request)
    {
        var user = await GetCurrentUserAsync();
        var form = await _context.MOIForms.FindAsync(id);
        if (form == null) return NotFound();

        form.WorkflowState = "Approved";
        form.RecommendedByUserId = user?.UserId;
        form.RecommendedAt = DateTime.UtcNow;
        form.RecommendationComments = $"[Admin override] {request.Comments}";
        form.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await JobHandoffService.OnMoiApprovedAsync(_context, form);

        return FormMapper.ToMoiResponse(form);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteForm(int id)
    {
        var form = await _context.MOIForms.FindAsync(id);
        if (form == null) return NotFound();
        _context.MOIForms.Remove(form);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static string DetermineInitialState(Customer? customer, Dictionary<string, object?> formData)
    {
        var requestedBy = formData.GetValueOrDefault("requestedBy")?.ToString() ?? string.Empty;
        if (customer == null || string.IsNullOrWhiteSpace(requestedBy))
            return "Draft";

        var moiHolders = JsonHelper.Deserialize<List<string>>(customer.MoiJson);
        if (moiHolders.Any(h => h.Equals(requestedBy, StringComparison.OrdinalIgnoreCase)))
            return "Draft";

        return "PendingRecommendation";
    }

    [HttpGet("{id}/export-pack")]
    public async Task<IActionResult> ExportPack(int id)
    {
        var form = await _context.MOIForms.FindAsync(id);
        if (form == null) return NotFound();
        if (!await FormAccessHelper.CanAccessMoiFormAsync(_context, User, form))
            return Forbid();

        var customer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, form.Company);
        var bytes = FormPackExportService.ExportMoiPack(form, customer);
        return File(bytes, "application/json", $"moi-{id}-pack.json");
    }

    private async Task<User?> GetCurrentUserAsync()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claim, out var id)) return null;
        return await _context.Users.FindAsync(id);
    }
}
