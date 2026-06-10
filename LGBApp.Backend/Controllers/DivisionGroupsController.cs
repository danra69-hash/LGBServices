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
public class DivisionGroupsController : ControllerBase
{
    private readonly AppDbContext _context;

    public DivisionGroupsController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DivisionGroupDto>>> GetAll()
    {
        var groups = await _context.DivisionGroups
            .Include(g => g.Recommenders)
            .Where(g => g.IsActive)
            .OrderBy(g => g.Name)
            .ToListAsync();
        return groups.Select(TemplateMapper.ToDto).ToList();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DivisionGroupDto>> Get(int id)
    {
        var group = await _context.DivisionGroups
            .Include(g => g.Recommenders)
            .FirstOrDefaultAsync(g => g.DivisionGroupId == id);
        if (group == null) return NotFound();
        return TemplateMapper.ToDto(group);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DivisionGroupDto>> Create(DivisionGroupDto dto)
    {
        var group = new DivisionGroup
        {
            Code = dto.Code,
            Name = dto.Name,
            MoaWorkflowTemplateCode = dto.MoaWorkflowTemplateCode,
            DefaultMoiFormTemplateCode = dto.DefaultMoiFormTemplateCode,
            DefaultMoaFormTemplateCode = dto.DefaultMoaFormTemplateCode,
            IsActive = dto.IsActive,
            Recommenders = dto.Recommenders.Select(r => new DivisionGroupRecommender
            {
                UserId = r.UserId,
                DisplayName = r.DisplayName,
            }).ToList(),
        };
        _context.DivisionGroups.Add(group);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = group.DivisionGroupId }, TemplateMapper.ToDto(group));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, DivisionGroupDto dto)
    {
        var group = await _context.DivisionGroups
            .Include(g => g.Recommenders)
            .FirstOrDefaultAsync(g => g.DivisionGroupId == id);
        if (group == null) return NotFound();

        group.Code = dto.Code;
        group.Name = dto.Name;
        group.MoaWorkflowTemplateCode = dto.MoaWorkflowTemplateCode;
        group.DefaultMoiFormTemplateCode = dto.DefaultMoiFormTemplateCode;
        group.DefaultMoaFormTemplateCode = dto.DefaultMoaFormTemplateCode;
        group.IsActive = dto.IsActive;

        _context.DivisionGroupRecommenders.RemoveRange(group.Recommenders);
        group.Recommenders = dto.Recommenders.Select(r => new DivisionGroupRecommender
        {
            DivisionGroupId = id,
            UserId = r.UserId,
            DisplayName = r.DisplayName,
        }).ToList();

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("import")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DivisionGroupImportResult>> Import([FromBody] List<DivisionGroupImportRow> rows)
    {
        var result = new DivisionGroupImportResult();
        var customers = await _context.Customers.ToListAsync();
        var groups = await _context.DivisionGroups.ToListAsync();

        foreach (var row in rows)
        {
            var customer = customers.FirstOrDefault(c =>
                c.Company.Equals(row.Company, StringComparison.OrdinalIgnoreCase));
            if (customer == null)
            {
                result.Unmatched.Add(row.Company);
                continue;
            }

            var code = row.DivisionGroup.Trim().ToUpperInvariant().Replace(" ", "_").Replace("&", "AND");
            var group = groups.FirstOrDefault(g =>
                g.Code.Equals(code, StringComparison.OrdinalIgnoreCase)
                || g.Name.Equals(row.DivisionGroup, StringComparison.OrdinalIgnoreCase));

            customer.DivisionGroupCode = group?.Code ?? code;
            customer.HasLoa = row.HasLoa;
            result.Updated++;
        }

        await _context.SaveChangesAsync();
        return result;
    }
}
