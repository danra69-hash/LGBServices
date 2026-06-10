using LGBApp.Backend.Data;
using LGBApp.Backend.Models.DTOs;
using LGBApp.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ServiceJobFormsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ServiceJobFormsController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FormResponse>>> GetForms([FromQuery] int? jobId)
    {
        var query = _context.ServiceJobForms.AsQueryable();
        if (jobId.HasValue)
            query = query.Where(f => f.JobRequestId == jobId.Value);

        var forms = await query.OrderByDescending(f => f.UpdatedAt).ToListAsync();
        forms = await FormAccessHelper.FilterServiceJobFormsAsync(_context, User, forms);
        return forms.Select(f => new FormResponse
        {
            Id = f.ServiceJobFormId,
            JobId = f.JobRequestId,
            Company = f.Company,
            Data = JsonHelper.Deserialize<Dictionary<string, object?>>(f.FormDataJson),
            WorkflowState = f.Status,
            CreatedAt = f.CreatedAt.ToString("yyyy-MM-dd"),
            UpdatedAt = f.UpdatedAt.ToString("yyyy-MM-dd"),
        }).ToList();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateForm(int id, FormRequest request)
    {
        var form = await _context.ServiceJobForms.FindAsync(id);
        if (form == null) return NotFound();
        if (!await FormAccessHelper.CanAccessServiceJobFormAsync(_context, User, form))
            return Forbid();

        form.FormDataJson = JsonHelper.Serialize(request.Data);
        form.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
