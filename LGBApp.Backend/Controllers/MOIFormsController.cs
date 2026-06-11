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

    public MOIFormsController(AppDbContext context) => _context = context;

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
        var results = new List<FormResponse>();
        foreach (var form in forms)
        {
            var customer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, form.Company);
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
            if (AuthHelper.IsExternalUser(User) && !AuthHelper.CanManageClientJob(User, job) && !AuthHelper.IsSignatoryForJob(User, job))
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
                await ApplyFormRequestAsync(existing, request, job, unit);
                var linkedCustomer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, existing.Company);
                return Ok(FormMapper.ToMoiResponse(existing, customer: linkedCustomer));
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
            Company = request.Company,
            FormDataJson = JsonHelper.Serialize(formData),
            FormTemplateCode = templateCode,
            WorkflowState = workflowState,
            FinanceRelated = request.FinanceRelated,
            BankSignatoryMatter = request.BankSignatoryMatter,
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

        if (AuthHelper.IsExternalUser(User))
        {
            if (form.WorkflowState is "PendingRecommendation" or "Recommended" or "PendingMoiApproval")
                form.WorkflowState = MoiWorkflowStates.Draft;

            if (form.WorkflowState is not (MoiWorkflowStates.Draft or MoiWorkflowStates.MoiRejected))
                return BadRequest("MOI can only be edited while in Draft or after rejection.");

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

        await ApplyFormRequestAsync(form, request, job, unit);
        return NoContent();
    }

    private async Task ApplyFormRequestAsync(
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

        form.UpdatedAt = DateTime.UtcNow;

        if (job != null)
        {
            if (unit != null && job.TotalQty > 1
                && unit.InternalHandoffStatus == JobHandoffStatuses.ClientSubmitted)
                unit.InternalHandoffStatus = JobHandoffStatuses.PendingPrep;
            else if (job.InternalHandoffStatus == JobHandoffStatuses.ClientSubmitted)
                JobHandoffService.SetHandoff(job, JobHandoffStatuses.PendingPrep);
        }

        await _context.SaveChangesAsync();
    }

    [HttpPost("{id}/submit-for-approval")]
    public async Task<ActionResult<FormResponse>> SubmitForApproval(int id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var form = await _context.MOIForms.FindAsync(id);
        if (form == null) return NotFound();

        if (form.WorkflowState is not (MoiWorkflowStates.Draft or MoiWorkflowStates.MoiRejected))
            return BadRequest("MOI can only be submitted for approval from Draft or after rejection.");

        if (!form.JobRequestId.HasValue)
            return BadRequest("MOI must be linked to a job.");

        var job = await _context.JobRequests.FindAsync(form.JobRequestId.Value);
        if (job == null) return NotFound();

        if (!AuthHelper.CanManageClientJob(User, job) && !AuthHelper.IsSignatoryForJob(User, job))
            return Forbid();

        var customer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, form.Company);
        if (customer == null) return BadRequest("Customer not found.");

        await JobHandoffService.OnMoiSubmittedForApprovalAsync(_context, job, form, customer);
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

        if (form.WorkflowState != MoiWorkflowStates.PendingClientMoiApproval)
            return BadRequest("MOI is not awaiting client approval.");

        var customer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, form.Company);
        if (customer == null) return BadRequest("Customer not found.");

        var required = ClientApprovalService.GetRequiredMoiApproverNames(customer);
        var holder = customer.AccountHolders.FirstOrDefault(h =>
            h.UserId == user.UserId && h.NeedsMoiApproval)
            ?? customer.AccountHolders.FirstOrDefault(h =>
                h.NeedsMoiApproval
                && h.Name.Equals(user.Name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (holder == null)
            return Forbid();

        var holderName = holder.Name.Trim();

        var records = ClientApprovalService.ParseMoi(form);
        if (ClientApprovalService.HasSigned(records, holderName))
            return BadRequest("You have already approved this MOI.");

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
            return BadRequest("MOI must be linked to a job.");

        var job = await _context.JobRequests.FindAsync(form.JobRequestId.Value);
        if (job == null) return NotFound();

        await JobHandoffService.OnClientMoiApprovalRecordedAsync(_context, job, form, customer);
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
            return BadRequest("A rejection reason is required.");

        var form = await _context.MOIForms.FindAsync(id);
        if (form == null) return NotFound();

        if (form.WorkflowState != MoiWorkflowStates.PendingClientMoiApproval)
            return BadRequest("MOI is not awaiting client approval.");

        var customer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, form.Company);
        if (customer == null) return BadRequest("Customer not found.");

        var holder = customer.AccountHolders.FirstOrDefault(h =>
            h.UserId == user.UserId && h.NeedsMoiApproval)
            ?? customer.AccountHolders.FirstOrDefault(h =>
                h.NeedsMoiApproval
                && h.Name.Equals(user.Name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (holder == null)
            return Forbid();

        if (!form.JobRequestId.HasValue)
            return BadRequest("MOI must be linked to a job.");

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

        if (form.WorkflowState == MoiWorkflowStates.Approved)
            return BadRequest("MOI already approved.");

        var customer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, form.Company);
        if (customer == null) return BadRequest("Customer not found for company.");

        var isAdmin = AuthHelper.IsAdmin(User);
        if (!await WorkflowService.CanRecommendMoiAsync(_context, user, customer, isAdmin))
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

    private async Task<User?> GetCurrentUserAsync()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claim, out var id)) return null;
        return await _context.Users.FindAsync(id);
    }
}
