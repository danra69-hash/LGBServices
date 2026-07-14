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
public class BillingPartiesController : ControllerBase
{
    private readonly AppDbContext _context;

    public BillingPartiesController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BillingPartyDto>>> GetAll(
        [FromQuery] string? category,
        [FromQuery] bool activeOnly = true)
    {
        var query = _context.BillingParties.AsQueryable();
        if (activeOnly)
            query = query.Where(b => b.IsActive);
        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim();
            query = query.Where(b =>
                b.Category == cat
                || b.Category == "Both"
                || cat == "Both");
        }

        var items = await query
            .OrderBy(b => b.SortOrder)
            .ThenBy(b => b.Name)
            .ToListAsync();

        return items.Select(ToDto).ToList();
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BillingPartyDto>> Create(BillingPartyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required." });

        var entity = new BillingParty
        {
            Name = request.Name.Trim(),
            Category = NormalizeCategory(request.Category),
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow,
        };
        _context.BillingParties.Add(entity);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), ToDto(entity));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BillingPartyDto>> Update(int id, BillingPartyRequest request)
    {
        var entity = await _context.BillingParties.FindAsync(id);
        if (entity == null) return NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required." });

        entity.Name = request.Name.Trim();
        entity.Category = NormalizeCategory(request.Category);
        entity.IsActive = request.IsActive;
        entity.SortOrder = request.SortOrder;
        await _context.SaveChangesAsync();
        return ToDto(entity);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.BillingParties.FindAsync(id);
        if (entity == null) return NotFound();

        await BillingPartyService.RemovePartyFromAllCustomersAsync(_context, id);
        _context.BillingParties.Remove(entity);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static string NormalizeCategory(string? category) =>
        category switch
        {
            "InvoiceBy" or "ChargeTo" or "Both" => category,
            _ => "Both",
        };

    private static BillingPartyDto ToDto(BillingParty b) => new()
    {
        Id = b.BillingPartyId,
        Name = b.Name,
        Category = b.Category,
        IsActive = b.IsActive,
        SortOrder = b.SortOrder,
    };
}
