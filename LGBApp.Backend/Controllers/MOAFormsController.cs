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

    public MOAFormsController(AppDbContext context) => _context = context;

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
            results.Add(FormMapper.ToMoaResponse(form, workflow));
        }
        return results;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FormResponse>> GetForm(int id)
    {
        var form = await _context.MOAForms.FindAsync(id);
        if (form == null) return NotFound();
        if (!await FormAccessHelper.CanAccessMoaFormAsync(_context, User, form))
            return Forbid();
        var workflow = await WorkflowService.GetWorkflowForMoaAsync(_context, id);
        return FormMapper.ToMoaResponse(form, workflow);
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
                return BadRequest("MOI must be approved before creating MOA.");
        }

        var templateCode = request.FormTemplateCode
            ?? WorkflowService.ResolveMoaTemplateCode(customer, group);

        var form = new MOAForm
        {
            JobRequestId = request.JobId,
            MOIFormId = request.MoiFormId,
            Company = request.Company,
            FormDataJson = JsonHelper.Serialize(request.Data),
            FormTemplateCode = templateCode,
            FinanceRelated = request.FinanceRelated,
            BankSignatoryMatter = request.BankSignatoryMatter,
            ShareMovement = request.ShareMovement,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        if (request.MoiFormId.HasValue)
        {
            var moi = await _context.MOIForms.FindAsync(request.MoiFormId.Value);
            if (moi != null)
            {
                form.FinanceRelated = moi.FinanceRelated;
                form.BankSignatoryMatter = moi.BankSignatoryMatter;
            }
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
        await _context.SaveChangesAsync();

        var workflow = await WorkflowService.GetWorkflowForMoaAsync(_context, id);
        return FormMapper.ToMoaResponse(form, workflow);
    }

    [HttpPost("{id}/start-workflow")]
    public async Task<ActionResult<FormResponse>> StartWorkflow(int id)
    {
        var form = await _context.MOAForms.FindAsync(id);
        if (form == null) return NotFound();

        var existing = await WorkflowService.GetWorkflowForMoaAsync(_context, id);
        if (existing != null)
            return BadRequest("Workflow already started.");

        if (form.MOIFormId.HasValue)
        {
            var moi = await _context.MOIForms.FindAsync(form.MOIFormId.Value);
            if (moi != null && moi.WorkflowState != "Approved")
                return BadRequest("MOI must be approved before starting MOA workflow.");
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

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateForm(int id, FormRequest request)
    {
        var form = await _context.MOAForms.FindAsync(id);
        if (form == null) return NotFound();

        form.MOIFormId = request.MoiFormId ?? form.MOIFormId;
        form.Company = request.Company;
        form.FormDataJson = JsonHelper.Serialize(request.Data);
        form.FinanceRelated = request.FinanceRelated;
        form.BankSignatoryMatter = request.BankSignatoryMatter;
        form.ShareMovement = request.ShareMovement;
        if (!string.IsNullOrWhiteSpace(request.FormTemplateCode))
            form.FormTemplateCode = request.FormTemplateCode;
        form.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
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
}
