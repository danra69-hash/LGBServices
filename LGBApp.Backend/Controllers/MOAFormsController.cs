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
public class MOAFormsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly WorkflowNotifier _notifier;

    public MOAFormsController(AppDbContext context, WorkflowNotifier notifier)
    {
        _context = context;
        _notifier = notifier;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FormResponse>>> GetForms([FromQuery] int? moiFormId)
    {
        var query = _context.MOAForms.AsQueryable();
        if (moiFormId.HasValue)
            query = query.Where(f => f.MOIFormId == moiFormId.Value);

        var forms = await query.OrderByDescending(f => f.UpdatedAt).ToListAsync();
        forms = await FormAccessHelper.FilterMoaFormsAsync(_context, User, forms);
        var results = new List<FormResponse>();
        foreach (var form in forms)
        {
            var workflow = await WorkflowService.GetWorkflowForMoaAsync(_context, form.MOAFormId);
            var customer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, form.Company);
            results.Add(FormMapper.ToMoaResponse(form, workflow, customer));
        }
        return results;
    }

    [HttpGet("for-job/{jobId:int}")]
    public async Task<ActionResult<FormResponse>> GetFormForJob(int jobId, [FromQuery] int? unitNumber = null)
    {
        var job = await _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees)
            .FirstOrDefaultAsync(j => j.JobRequestId == jobId);
        if (job == null)
            return NotFound();

        if (!AuthHelper.CanAccessJob(User, job))
            return Forbid();

        var moa = await JobFormProvisioner.EnsureMoaForJobAsync(_context, job, unitNumber)
            ?? await JobFormProvisioner.FindMoaForJobAsync(_context, job, unitNumber);
        if (moa == null)
            return NotFound("No MOA form is available for this job yet.");

        if (!await FormAccessHelper.CanAccessMoaFormAsync(_context, User, moa))
            return Forbid();

        var workflow = await WorkflowService.GetWorkflowForMoaAsync(_context, moa.MOAFormId);
        var customer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, moa.Company);
        return FormMapper.ToMoaResponse(moa, workflow, customer);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FormResponse>> GetForm(int id)
    {
        var form = await _context.MOAForms.FindAsync(id);
        if (form == null) return NotFound();
        if (!await FormAccessHelper.CanAccessMoaFormAsync(_context, User, form))
            return Forbid();
        var workflow = await WorkflowService.GetWorkflowForMoaAsync(_context, id);
        var customer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, form.Company);
        return FormMapper.ToMoaResponse(form, workflow, customer);
    }

    [HttpPost]
    public async Task<ActionResult<FormResponse>> CreateForm(FormRequest request)
    {
        var customer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, request.Company);
        DivisionGroup? group = null;
        if (customer != null && !string.IsNullOrWhiteSpace(customer.DivisionGroupCode))
            group = await _context.DivisionGroups.FirstOrDefaultAsync(g => g.Code == customer.DivisionGroupCode);

        if (request.MoiFormId.HasValue)
        {
            var moiForm = await _context.MOIForms.FindAsync(request.MoiFormId.Value);
            if (moiForm != null && moiForm.WorkflowState != "Approved")
                return BadRequest(new { message = "MOI must be approved before creating MOA." });
        }

        var templateCode = request.FormTemplateCode
            ?? WorkflowService.ResolveMoaTemplateCode(customer, group);

        MOIForm? linkedMoi = null;
        if (request.MoiFormId.HasValue)
            linkedMoi = await _context.MOIForms.FindAsync(request.MoiFormId.Value);

        if (linkedMoi != null && request.JobId.HasValue)
        {
            var duplicate = await _context.MOAForms.FirstOrDefaultAsync(f =>
                f.JobRequestId == request.JobId
                && f.JobRequestUnitId == linkedMoi.JobRequestUnitId
                && (linkedMoi.JobRequestUnitId != null || f.MOIFormId == linkedMoi.MOIFormId));
            if (duplicate != null)
                return Ok(FormMapper.ToMoaResponse(duplicate, null, customer));
        }

        var moaData = new Dictionary<string, object?>(request.Data);
        if (linkedMoi != null)
        {
            moaData["moiFormId"] = linkedMoi.MOIFormId;
            if (linkedMoi.JobRequestUnitId.HasValue
                && !moaData.ContainsKey("unitNumber"))
            {
                var unitNum = await _context.JobRequestUnits
                    .Where(u => u.JobRequestUnitId == linkedMoi.JobRequestUnitId)
                    .Select(u => u.UnitNumber)
                    .FirstOrDefaultAsync();
                if (unitNum > 0)
                    moaData["unitNumber"] = unitNum;
            }
        }

        var form = new MOAForm
        {
            JobRequestId = request.JobId,
            JobRequestUnitId = linkedMoi?.JobRequestUnitId,
            MOIFormId = request.MoiFormId,
            Company = request.Company,
            FormDataJson = JsonHelper.Serialize(moaData),
            FormTemplateCode = templateCode,
            FinanceRelated = request.FinanceRelated,
            BankSignatoryMatter = request.BankSignatoryMatter,
            ShareMovement = request.ShareMovement,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        if (linkedMoi != null)
        {
            form.FinanceRelated = linkedMoi.FinanceRelated;
            form.BankSignatoryMatter = linkedMoi.BankSignatoryMatter;
        }

        _context.MOAForms.Add(form);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetForm), new { id = form.MOAFormId }, FormMapper.ToMoaResponse(form, null));
    }

    [HttpPut("{id}/pack")]
    public async Task<ActionResult<FormResponse>> UpdatePack(int id, UpdateMoaPackRequest request)
    {
        var form = await _context.MOAForms.FindAsync(id);
        if (form == null) return NotFound();

        var deny = await AuthorizeMoaEditAsync(form);
        if (deny != null) return deny;

        var conflict = FormConcurrencyHelper.CheckExpectedUpdatedAt(form.UpdatedAt, request.ExpectedUpdatedAt);
        if (conflict != null) return conflict;

        var previousUpdatedAt = form.UpdatedAt;
        form.FinanceRelated = request.FinanceRelated;
        form.BankSignatoryMatter = request.BankSignatoryMatter;
        form.ShareMovement = request.ShareMovement;
        MoaPackChecklistService.Apply(form, new MoaPackChecklist
        {
            InternalChecklistA = request.Checklist.InternalChecklistA,
            InternalChecklistB = request.Checklist.InternalChecklistB,
            CleanAgreementAttached = request.Checklist.CleanAgreementAttached,
            ShareholdingTableAttached = request.Checklist.ShareholdingTableAttached,
            SsmRegistrationNo = request.Checklist.SsmRegistrationNo,
            SsmNewRegistrationNo = request.Checklist.SsmNewRegistrationNo,
            SsmEntityType = request.Checklist.SsmEntityType,
            SsmStatus = request.Checklist.SsmStatus,
            SsmAsAtDate = request.Checklist.SsmAsAtDate,
        });
        form.UpdatedAt = DateTime.UtcNow;
        var saveConflict = await FormConcurrencyHelper.SaveWithConcurrencyAsync(_context, previousUpdatedAt);
        if (saveConflict != null) return saveConflict;

        var workflow = await WorkflowService.GetWorkflowForMoaAsync(_context, id);
        return FormMapper.ToMoaResponse(form, workflow);
    }

    [HttpPost("{id}/client-approve")]
    public async Task<ActionResult<FormResponse>> ClientApprove(int id, ClientApproveRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var isExternal = AuthHelper.IsExternalUser(User);
        var isInternalSigner = AuthHelper.IsInternalStaff(User)
            && (user.IsInternalSignatory || user.CanApproveMoa);
        if (!isExternal && !isInternalSigner)
            return Forbid();

        var form = await _context.MOAForms.FindAsync(id);
        if (form == null) return NotFound();

        // N4: tenant check before state oracle
        var customer = await WorkflowService.ResolveCustomerForMoaAsync(_context, form);
        if (customer == null) return NotFound();
        if (isExternal && !AuthHelper.CanAccessCustomer(User, customer.CustomerId))
            return NotFound();

        if (!form.JobRequestId.HasValue)
            return BadRequest(new { message = "MOA must be linked to a job." });

        var job = await _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees)
            .FirstOrDefaultAsync(j => j.JobRequestId == form.JobRequestId.Value);
        if (job == null) return NotFound();

        var unit = JobHandoffResolver.ResolveUnit(job, null, form);
        var handoff = JobHandoffResolver.ResolveEffectiveHandoff(job, unit, form);
        if (!JobHandoffResolver.IsMoaClientSignoffHandoff(handoff))
            return BadRequest(new { message = "MOA is not available for client sign-off." });

        var holder = ClientApprovalService.FindMoaHolderForUser(customer, user);
        var holderName = ClientApprovalService.ResolveMoaSignerName(customer, user, isInternalSigner);
        if (holderName == null)
            return Forbid();

        var signatureError = SignaturePolicy.ValidateRequired(request.SignatureDataUrl, request.SignatureFileName);
        if (signatureError != null) return signatureError;

        var records = ClientApprovalService.ParseMoa(form);
        // D4: client holders match by UserId; internal countersign by UserId only
        if (holder != null && ClientApprovalService.HasSigned(records, holder))
            return BadRequest(new { message = "You have already signed off on this MOA." });
        if (holder == null && records.Any(r => r.UserId == user.UserId))
            return BadRequest(new { message = "You have already signed off on this MOA." });

        records.Add(new ClientApprovalRecord
        {
            UserId = user.UserId,
            AccountHolderName = holderName,
            Comments = request.Comments ?? string.Empty,
            SignatureFileName = request.SignatureFileName,
            SignatureDataUrl = request.SignatureDataUrl,
            SignedAt = DateTime.UtcNow,
        });
        ClientApprovalService.SaveMoa(form, records);

        await JobHandoffService.OnClientMoaApprovalRecordedAsync(_context, job, form, customer, unit, _notifier);

        var workflow = await WorkflowService.GetWorkflowForMoaAsync(_context, id);
        return FormMapper.ToMoaResponse(form, workflow, customer);
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

        var form = await _context.MOAForms.FindAsync(id);
        if (form == null) return NotFound();

        // N4: tenant check before state oracle
        var customer = await WorkflowService.ResolveCustomerForMoaAsync(_context, form);
        if (customer == null || !AuthHelper.CanAccessCustomer(User, customer.CustomerId))
            return NotFound();

        if (!form.JobRequestId.HasValue)
            return BadRequest(new { message = "MOA must be linked to a job." });

        var job = await _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees)
            .FirstOrDefaultAsync(j => j.JobRequestId == form.JobRequestId.Value);
        if (job == null) return NotFound();

        var unit = JobHandoffResolver.ResolveUnit(job, null, form);
        var handoff = JobHandoffResolver.ResolveEffectiveHandoff(job, unit, form);
        if (!JobHandoffResolver.IsMoaClientSignoffHandoff(handoff))
            return BadRequest(new { message = "MOA is not available for client sign-off." });

        if (ClientApprovalService.FindMoaHolderForUser(customer, user) == null)
            return Forbid();

        await JobHandoffService.OnMoaClientRejectedAsync(_context, job, form, user, request.Reason, unit);

        var workflow = await WorkflowService.GetWorkflowForMoaAsync(_context, id);
        return FormMapper.ToMoaResponse(form, workflow, customer);
    }

    [HttpPost("{id}/start-workflow")]
    public async Task<ActionResult<FormResponse>> StartWorkflow(int id)
    {
        if (!AuthHelper.IsInternalStaff(User))
            return Forbid();

        var form = await _context.MOAForms.FindAsync(id);
        if (form == null) return NotFound();

        if (!await FormAccessHelper.CanAccessMoaFormAsync(_context, User, form))
            return Forbid();

        var existing = await WorkflowService.GetWorkflowForMoaAsync(_context, id);
        if (existing != null)
            return BadRequest(new { message = "Workflow already started." });

        MOIForm? moi = null;
        if (form.JobRequestId.HasValue)
        {
            var job = await _context.JobRequests
                .Include(j => j.Units).ThenInclude(u => u.Assignees)
                .FirstOrDefaultAsync(j => j.JobRequestId == form.JobRequestId);
            if (job != null)
            {
                moi = await ResolveLinkedMoiAsync(form);
                var unit = JobHandoffResolver.ResolveUnit(job, null, form);
                var handoff = JobHandoffResolver.ResolveEffectiveHandoff(job, unit, form);
                if (!IsMoaRoutingStartAllowed(handoff, moi))
                    return BadRequest(new { message = "Internal MOA routing can only start while the MOA is being prepared." });

                if (moi != null && moi.WorkflowState is
                    MoiWorkflowStates.Draft
                    or MoiWorkflowStates.PendingClientMoiApproval
                    or MoiWorkflowStates.PendingAdminIntake
                    or MoiWorkflowStates.MoiRejected)
                    return BadRequest(new { message = "MOI must pass client intake before starting MOA internal routing." });
            }
        }

        var (packValid, packErrors) = MoaPackChecklistService.Validate(form);
        if (!packValid)
            return BadRequest(new { message = "MOA pack checklist incomplete.", errors = packErrors });

        var customer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, form.Company);
        await WorkflowService.InitializeMoaWorkflowAsync(_context, form, customer);
        await JobHandoffService.OnMoaWorkflowStartedAsync(_context, form);
        var workflow = await WorkflowService.GetWorkflowForMoaAsync(_context, id);
        return FormMapper.ToMoaResponse(form, workflow);
    }

    private async Task<MOIForm?> ResolveLinkedMoiAsync(MOAForm form)
    {
        if (form.MOIFormId.HasValue)
            return await _context.MOIForms.FindAsync(form.MOIFormId.Value);

        if (!form.JobRequestId.HasValue)
            return null;

        return await _context.MOIForms
            .Where(f => f.JobRequestId == form.JobRequestId
                && (form.JobRequestUnitId == null
                    ? f.JobRequestUnitId == null
                    : f.JobRequestUnitId == form.JobRequestUnitId))
            .OrderByDescending(f => f.UpdatedAt)
            .FirstOrDefaultAsync();
    }

    private static bool IsMoaRoutingStartAllowed(string handoff, MOIForm? moi) =>
        handoff is JobHandoffStatuses.PendingPrep
            or JobHandoffStatuses.ResoInProgress
            or JobHandoffStatuses.AdminReview
        || (string.IsNullOrWhiteSpace(handoff)
            && moi?.WorkflowState is MoiWorkflowStates.PendingPrep
                or MoiWorkflowStates.PendingRecommendation
                or MoiWorkflowStates.Approved);

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateForm(int id, FormRequest request)
    {
        var form = await _context.MOAForms.FindAsync(id);
        if (form == null) return NotFound();

        var deny = await AuthorizeMoaEditAsync(form);
        if (deny != null) return deny;

        var conflict = FormConcurrencyHelper.CheckExpectedUpdatedAt(form.UpdatedAt, request.ExpectedUpdatedAt);
        if (conflict != null) return conflict;

        var previousUpdatedAt = form.UpdatedAt;
        form.MOIFormId = request.MoiFormId ?? form.MOIFormId;
        form.Company = request.Company;
        form.FormDataJson = JsonHelper.Serialize(request.Data);
        form.FinanceRelated = request.FinanceRelated;
        form.BankSignatoryMatter = request.BankSignatoryMatter;
        form.ShareMovement = request.ShareMovement;
        if (!string.IsNullOrWhiteSpace(request.FormTemplateCode))
            form.FormTemplateCode = request.FormTemplateCode;
        form.UpdatedAt = DateTime.UtcNow;

        var saveConflict = await FormConcurrencyHelper.SaveWithConcurrencyAsync(_context, previousUpdatedAt);
        if (saveConflict != null) return saveConflict;
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteForm(int id)
    {
        var form = await _context.MOAForms.FindAsync(id);
        if (form == null) return NotFound();
        _context.MOAForms.Remove(form);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<ActionResult?> AuthorizeMoaEditAsync(MOAForm form)
    {
        if (!await FormAccessHelper.CanAccessMoaFormAsync(_context, User, form))
            return Forbid();

        if (!form.JobRequestId.HasValue)
            return null;

        var job = await _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees)
            .FirstOrDefaultAsync(j => j.JobRequestId == form.JobRequestId);
        if (job == null)
            return NotFound();

        MOIForm? moi = null;
        if (form.MOIFormId.HasValue)
            moi = await _context.MOIForms.FindAsync(form.MOIFormId.Value);
        else
        {
            moi = await _context.MOIForms
                .Where(f => f.JobRequestId == form.JobRequestId
                    && (form.JobRequestUnitId == null
                        ? f.JobRequestUnitId == null
                        : f.JobRequestUnitId == form.JobRequestUnitId))
                .OrderByDescending(f => f.UpdatedAt)
                .FirstOrDefaultAsync();
        }

        var workflow = await WorkflowService.GetWorkflowForMoaAsync(_context, form.MOAFormId);
        if (!TaskFormVisibilityHelper.CanEditMoaForm(User, job, form, moi, workflow != null))
            return Forbid();

        return null;
    }

    [HttpGet("{id}/export-pack")]
    public async Task<IActionResult> ExportPack(int id)
    {
        var form = await _context.MOAForms.FindAsync(id);
        if (form == null) return NotFound();
        if (!await FormAccessHelper.CanAccessMoaFormAsync(_context, User, form))
            return Forbid();

        var customer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, form.Company);
        var bytes = FormPackExportService.ExportMoaPack(form, customer);
        return File(bytes, "application/json", $"moa-{id}-pack.json");
    }

    private async Task<User?> GetCurrentUserAsync()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claim, out var id)) return null;
        return await _context.Users.FindAsync(id);
    }
}
