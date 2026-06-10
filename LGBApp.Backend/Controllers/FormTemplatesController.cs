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
public class FormTemplatesController : ControllerBase
{
    private readonly AppDbContext _context;

    public FormTemplatesController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FormTemplateDto>>> GetAll([FromQuery] string? formType)
    {
        var query = _context.FormTemplates.Where(t => t.IsActive).AsQueryable();
        if (!string.IsNullOrWhiteSpace(formType))
            query = query.Where(t => t.FormType == formType);
        var templates = await query.OrderBy(t => t.Name).ToListAsync();
        return templates.Select(TemplateMapper.ToDto).ToList();
    }

    [HttpGet("resolve")]
    public async Task<ActionResult<FormTemplateDto>> Resolve(
        [FromQuery] string formType,
        [FromQuery] string? company,
        [FromQuery] string? templateCode)
    {
        if (!string.IsNullOrWhiteSpace(templateCode))
        {
            var explicit_ = await _context.FormTemplates
                .FirstOrDefaultAsync(t => t.Code == templateCode && t.FormType == formType && t.IsActive);
            if (explicit_ != null) return TemplateMapper.ToDto(explicit_);
        }

        Customer? customer = null;
        DivisionGroup? group = null;
        if (!string.IsNullOrWhiteSpace(company))
        {
            customer = await _context.Customers.FirstOrDefaultAsync(c => c.Company == company);
            if (customer != null && !string.IsNullOrWhiteSpace(customer.DivisionGroupCode))
                group = await _context.DivisionGroups
                    .FirstOrDefaultAsync(g => g.Code == customer.DivisionGroupCode);
        }

        var code = formType == "MOI"
            ? WorkflowService.ResolveMoiTemplateCode(customer, group)
            : WorkflowService.ResolveMoaTemplateCode(customer, group);

        var template = await _context.FormTemplates
            .FirstOrDefaultAsync(t => t.Code == code && t.FormType == formType && t.IsActive)
            ?? await _context.FormTemplates
                .FirstOrDefaultAsync(t => t.FormType == formType && t.IsDefault && t.IsActive);

        if (template == null) return NotFound();
        return TemplateMapper.ToDto(template);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FormTemplateDto>> Get(int id)
    {
        var template = await _context.FormTemplates.FindAsync(id);
        if (template == null) return NotFound();
        return TemplateMapper.ToDto(template);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<FormTemplateDto>> Create(FormTemplateDto dto)
    {
        var template = new FormTemplate { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        TemplateMapper.ApplyFormTemplate(template, dto);
        _context.FormTemplates.Add(template);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = template.FormTemplateId }, TemplateMapper.ToDto(template));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, FormTemplateDto dto)
    {
        var template = await _context.FormTemplates.FindAsync(id);
        if (template == null) return NotFound();
        TemplateMapper.ApplyFormTemplate(template, dto);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
