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
public class WorkflowInstancesController : ControllerBase
{
    private readonly AppDbContext _context;

    public WorkflowInstancesController(AppDbContext context) => _context = context;

    [HttpGet("moa/{moaFormId}")]
    public async Task<ActionResult<WorkflowInstanceDto>> GetForMoa(int moaFormId)
    {
        var dto = await WorkflowService.GetWorkflowForMoaAsync(_context, moaFormId);
        if (dto == null) return NotFound();
        return dto;
    }

    [HttpPost("moa/{moaFormId}/approve-step")]
    public async Task<ActionResult<WorkflowInstanceDto>> ApproveMoaStep(int moaFormId, ApproveWorkflowStepRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var instance = await _context.WorkflowInstances
            .Include(i => i.Steps)
            .FirstOrDefaultAsync(i => i.MoaFormId == moaFormId && i.Status == "Active");
        if (instance == null) return NotFound("No active workflow.");

        var step = await WorkflowService.GetCurrentStepAsync(_context, instance);
        if (step == null) return BadRequest("No active step.");

        var form = await _context.MOAForms.FindAsync(moaFormId);
        var customer = form != null
            ? await WorkflowService.ResolveCustomerForCompanyAsync(_context, form.Company)
            : null;
        var isAdmin = AuthHelper.IsAdmin(User);

        if (!await WorkflowService.CanUserApproveStepAsync(_context, user, step, customer, isAdmin))
            return Forbid();

        step.ApprovedByUserId = user.UserId;
        step.Comments = request.Comments;
        await WorkflowService.AdvanceWorkflowAsync(_context, instance, step);

        if (instance.Status == "Completed" && form != null)
        {
            form.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await JobHandoffService.OnMoaWorkflowCompletedAsync(_context, moaFormId);
        }

        return await WorkflowService.GetWorkflowForMoaAsync(_context, moaFormId)
            ?? throw new InvalidOperationException("Workflow missing after approve.");
    }

    [HttpPost("moa/{moaFormId}/admin-override")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<WorkflowInstanceDto>> AdminOverrideMoaStep(int moaFormId, AdminOverrideStepRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var instance = await _context.WorkflowInstances
            .Include(i => i.Steps)
            .FirstOrDefaultAsync(i => i.MoaFormId == moaFormId && i.Status == "Active");
        if (instance == null) return NotFound();

        var step = instance.Steps.FirstOrDefault(s => s.WorkflowStepInstanceId == request.StepInstanceId);
        if (step == null) return NotFound("Step not found.");

        step.AdminOverridden = true;
        step.OverriddenByUserId = user.UserId;
        step.Comments = request.Comments;
        step.ApprovedByUserId = user.UserId;
        await WorkflowService.AdvanceWorkflowAsync(_context, instance, step);

        return await WorkflowService.GetWorkflowForMoaAsync(_context, moaFormId)
            ?? throw new InvalidOperationException("Workflow missing after override.");
    }

    private async Task<User?> GetCurrentUserAsync()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claim, out var id)) return null;
        return await _context.Users.FindAsync(id);
    }
}
