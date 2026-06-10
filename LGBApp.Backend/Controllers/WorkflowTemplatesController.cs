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
public class WorkflowTemplatesController : ControllerBase
{
    private readonly AppDbContext _context;

    public WorkflowTemplatesController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkflowTemplateDto>>> GetAll([FromQuery] string? workflowType)
    {
        var query = _context.WorkflowTemplates.Include(t => t.Steps).AsQueryable();
        if (!string.IsNullOrWhiteSpace(workflowType))
            query = query.Where(t => t.WorkflowType == workflowType);
        var templates = await query.OrderBy(t => t.Name).ToListAsync();
        return templates.Select(TemplateMapper.ToDto).ToList();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WorkflowTemplateDto>> Get(int id)
    {
        var template = await _context.WorkflowTemplates
            .Include(t => t.Steps)
            .FirstOrDefaultAsync(t => t.WorkflowTemplateId == id);
        if (template == null) return NotFound();
        return TemplateMapper.ToDto(template);
    }

    [HttpGet("by-code/{code}")]
    public async Task<ActionResult<WorkflowTemplateDto>> GetByCode(string code)
    {
        var template = await _context.WorkflowTemplates
            .Include(t => t.Steps)
            .FirstOrDefaultAsync(t => t.Code == code);
        if (template == null) return NotFound();
        return TemplateMapper.ToDto(template);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, WorkflowTemplateDto dto)
    {
        var template = await _context.WorkflowTemplates
            .Include(t => t.Steps)
            .FirstOrDefaultAsync(t => t.WorkflowTemplateId == id);
        if (template == null) return NotFound();

        template.Name = dto.Name;
        template.Description = dto.Description;
        template.IsActive = dto.IsActive;

        _context.WorkflowStepTemplates.RemoveRange(template.Steps);
        template.Steps = dto.Steps.Select(s => new WorkflowStepTemplate
        {
            WorkflowTemplateId = id,
            StepOrder = s.StepOrder,
            StepKey = s.StepKey,
            DisplayName = s.DisplayName,
            ConditionType = s.ConditionType,
            AssigneeType = s.AssigneeType,
            AssigneeRole = s.AssigneeRole,
            AssigneeUserId = s.AssigneeUserId,
            AssigneeDisplayName = s.AssigneeDisplayName,
            AllowAdminOverride = s.AllowAdminOverride,
        }).ToList();

        await _context.SaveChangesAsync();
        return NoContent();
    }
}
