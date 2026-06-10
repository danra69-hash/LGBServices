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
    public async Task<ActionResult<IEnumerable<FormResponse>>> GetForms([FromQuery] int? jobId)
    {
        var query = _context.MOIForms.AsQueryable();
        if (jobId.HasValue)
            query = query.Where(f => f.JobRequestId == jobId.Value);

        query = await FormAccessHelper.ScopeMoiFormsAsync(_context, User, query);
        var forms = await query.OrderByDescending(f => f.UpdatedAt).ToListAsync();
        return forms.Select(f => FormMapper.ToMoiResponse(f)).ToList();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FormResponse>> GetForm(int id)
    {
        var form = await _context.MOIForms.FindAsync(id);
        if (form == null) return NotFound();
        if (!await FormAccessHelper.CanAccessMoiFormAsync(_context, User, form))
            return Forbid();
        return FormMapper.ToMoiResponse(form);
    }

    [HttpPost]
    public async Task<ActionResult<FormResponse>> CreateForm(FormRequest request)
    {
        var customer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, request.Company);
        DivisionGroup? group = null;
        if (customer != null && !string.IsNullOrWhiteSpace(customer.DivisionGroupCode))
            group = await _context.DivisionGroups.FirstOrDefaultAsync(g => g.Code == customer.DivisionGroupCode);

        var formData = new Dictionary<string, object?>(request.Data);
        if (request.JobId.HasValue)
            formData["jobId"] = request.JobId.Value;

        var templateCode = request.FormTemplateCode
            ?? WorkflowService.ResolveMoiTemplateCode(customer, group);

        var form = new MOIForm
        {
            JobRequestId = request.JobId,
            Company = request.Company,
            FormDataJson = JsonHelper.Serialize(formData),
            FormTemplateCode = templateCode,
            WorkflowState = DetermineInitialState(customer, formData),
            FinanceRelated = request.FinanceRelated,
            BankSignatoryMatter = request.BankSignatoryMatter,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _context.MOIForms.Add(form);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetForm), new { id = form.MOIFormId }, FormMapper.ToMoiResponse(form));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateForm(int id, FormRequest request)
    {
        var form = await _context.MOIForms.FindAsync(id);
        if (form == null) return NotFound();

        var formData = new Dictionary<string, object?>(request.Data);
        if (request.JobId.HasValue)
            formData["jobId"] = request.JobId.Value;

        form.JobRequestId = request.JobId ?? form.JobRequestId;
        form.Company = request.Company;
        form.FormDataJson = JsonHelper.Serialize(formData);
        form.FinanceRelated = request.FinanceRelated;
        form.BankSignatoryMatter = request.BankSignatoryMatter;
        if (!string.IsNullOrWhiteSpace(request.FormTemplateCode))
            form.FormTemplateCode = request.FormTemplateCode;

        if (form.WorkflowState == "PendingPrep"
            && AuthHelper.IsInternalStaff(User)
            && form.JobRequestId.HasValue)
        {
            var linkedJob = await _context.JobRequests.FindAsync(form.JobRequestId.Value);
            if (linkedJob != null && !TaskFormVisibilityHelper.AwaitingIntakeApproval(linkedJob))
                form.WorkflowState = "PendingRecommendation";
        }

        form.UpdatedAt = DateTime.UtcNow;

        if (form.JobRequestId.HasValue)
        {
            var job = await _context.JobRequests.FindAsync(form.JobRequestId.Value);
            if (job != null && job.InternalHandoffStatus == JobHandoffStatuses.ClientSubmitted)
                JobHandoffService.SetHandoff(job, JobHandoffStatuses.PendingPrep);
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/recommend")]
    public async Task<ActionResult<FormResponse>> Recommend(int id, RecommendMoiRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var form = await _context.MOIForms.FindAsync(id);
        if (form == null) return NotFound();

        if (form.WorkflowState is "Recommended" or "Approved")
            return BadRequest("MOI already recommended or approved.");

        var customer = await WorkflowService.ResolveCustomerForCompanyAsync(_context, form.Company);
        if (customer == null) return BadRequest("Customer not found for company.");

        var isAdmin = AuthHelper.IsAdmin(User);
        if (!await WorkflowService.CanRecommendMoiAsync(_context, user, customer, isAdmin))
            return Forbid();

        form.RecommendedByUserId = user.UserId;
        form.RecommendedAt = DateTime.UtcNow;
        form.RecommendationComments = request.Comments;
        form.WorkflowState = "PendingMoiApproval";
        form.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await JobHandoffService.OnMoiRecommendedAsync(_context, form);

        return FormMapper.ToMoiResponse(form);
    }

    [HttpPost("{id}/approve")]
    public async Task<ActionResult<FormResponse>> ApproveMoi(int id, ApproveWorkflowStepRequest request)
    {
        if (!AuthHelper.CanApproveMoiIntake(User))
            return Forbid();

        var form = await _context.MOIForms.FindAsync(id);
        if (form == null) return NotFound();

        if (form.WorkflowState != "PendingMoiApproval" && form.WorkflowState != "Recommended")
            return BadRequest("MOI is not pending approval.");

        form.WorkflowState = "Approved";
        form.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await JobHandoffService.OnMoiApprovedAsync(_context, form);

        return FormMapper.ToMoiResponse(form);
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
