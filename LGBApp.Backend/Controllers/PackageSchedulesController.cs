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
public class PackageSchedulesController : ControllerBase
{
    private readonly AppDbContext _context;

    public PackageSchedulesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PackageScheduleItemDto>>> GetSchedules(
        [FromQuery] int? customerId,
        [FromQuery] int? customerPackageId,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] bool mineOnly = false)
    {
        var query = _context.PackageScheduleItems
            .Include(s => s.Customer)
            .Include(s => s.CustomerPackage)
            .AsQueryable();

        if (customerId.HasValue)
            query = query.Where(s => s.CustomerId == customerId.Value);

        if (customerPackageId.HasValue)
            query = query.Where(s => s.CustomerPackageId == customerPackageId.Value);

        if (DateTime.TryParse(from, out var fromDate))
            query = query.Where(s => s.ScheduledAt >= fromDate);

        if (DateTime.TryParse(to, out var toDate))
            query = query.Where(s => s.ScheduledAt <= toDate);

        if (!AuthHelper.IsAdmin(User) || mineOnly)
        {
            var userId = AuthHelper.CurrentUserId(User);
            if (!userId.HasValue)
                return Ok(Array.Empty<PackageScheduleItemDto>());

            query = query.Where(s => s.AssignedUserId == userId.Value);
        }

        var items = await query.OrderBy(s => s.ScheduledAt).ToListAsync();

        if (!AuthHelper.IsExternalUser(User))
        {
            var workUnitIds = items
                .Where(i => i.ItemType == "work" && i.JobRequestUnitId.HasValue)
                .Select(i => i.JobRequestUnitId!.Value)
                .Distinct()
                .ToList();

            if (workUnitIds.Count > 0)
            {
                var units = await _context.JobRequestUnits
                    .AsNoTracking()
                    .Include(u => u.JobRequest)
                    .Where(u => workUnitIds.Contains(u.JobRequestUnitId))
                    .ToListAsync();

                var jobIds = units.Select(u => u.JobRequestId).Distinct().ToList();
                var mois = await _context.MOIForms
                    .AsNoTracking()
                    .Where(f => f.JobRequestId != null && jobIds.Contains(f.JobRequestId.Value))
                    .ToListAsync();

                var releasedUnitIds = units
                    .Where(u => InternalWorkVisibilityHelper.IsUnitReleasedToInternal(
                        u.JobRequest,
                        u,
                        InternalWorkVisibilityHelper.ResolveMoiForUnit(
                            mois.Where(f => f.JobRequestId == u.JobRequestId),
                            u,
                            u.JobRequest)))
                    .Select(u => u.JobRequestUnitId)
                    .ToHashSet();

                items = items
                    .Where(i => i.ItemType != "work"
                        || (i.JobRequestUnitId.HasValue && releasedUnitIds.Contains(i.JobRequestUnitId.Value)))
                    .ToList();
            }
        }

        return items.Select(ToDto).ToList();
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PackageScheduleItemDto>> CreateSchedule(PackageScheduleItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { message = "Title is required." });

        if (!DateTime.TryParse(request.ScheduledAt, out var scheduledAt))
            return BadRequest(new { message = "Valid scheduled date/time is required." });

        var package = await _context.CustomerPackages
            .Include(p => p.Customer)
            .FirstOrDefaultAsync(p => p.CustomerPackageId == request.CustomerPackageId);

        if (package == null)
            return BadRequest(new { message = "Package not found." });

        if (package.CustomerId != request.CustomerId)
            return BadRequest(new { message = "Package does not belong to this customer." });

        var item = new PackageScheduleItem
        {
            CustomerId = request.CustomerId,
            CustomerPackageId = request.CustomerPackageId,
            ItemType = string.IsNullOrWhiteSpace(request.ItemType) ? "call" : request.ItemType,
            Title = request.Title.Trim(),
            ScheduledAt = scheduledAt,
            DurationMinutes = request.DurationMinutes,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "scheduled" : request.Status,
            Notes = request.Notes,
            BookingUrl = request.BookingUrl,
            SequenceNumber = request.SequenceNumber
        };

        _context.PackageScheduleItems.Add(item);
        await _context.SaveChangesAsync();

        await _context.Entry(item).Reference(s => s.Customer).LoadAsync();
        await _context.Entry(item).Reference(s => s.CustomerPackage).LoadAsync();

        return CreatedAtAction(nameof(GetSchedules), new { customerId = item.CustomerId }, ToDto(item));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateSchedule(int id, PackageScheduleItemRequest request)
    {
        var item = await _context.PackageScheduleItems.FindAsync(id);
        if (item == null)
            return NotFound();

        if (!DateTime.TryParse(request.ScheduledAt, out var scheduledAt))
            return BadRequest(new { message = "Valid scheduled date/time is required." });

        item.ItemType = string.IsNullOrWhiteSpace(request.ItemType) ? item.ItemType : request.ItemType;
        item.Title = string.IsNullOrWhiteSpace(request.Title) ? item.Title : request.Title.Trim();
        item.ScheduledAt = scheduledAt;
        item.DurationMinutes = request.DurationMinutes;
        item.Status = string.IsNullOrWhiteSpace(request.Status) ? item.Status : request.Status;
        item.Notes = request.Notes;
        item.BookingUrl = request.BookingUrl;
        item.SequenceNumber = request.SequenceNumber;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSchedule(int id)
    {
        var item = await _context.PackageScheduleItems.FindAsync(id);
        if (item == null)
            return NotFound();

        _context.PackageScheduleItems.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static PackageScheduleItemDto ToDto(PackageScheduleItem item) => new()
    {
        Id = item.PackageScheduleItemId,
        CustomerId = item.CustomerId,
        CustomerPackageId = item.CustomerPackageId,
        PackageName = item.CustomerPackage?.PackageName ?? string.Empty,
        CustomerName = item.Customer?.Company ?? string.Empty,
        ItemType = item.ItemType,
        Title = item.Title,
        ScheduledAt = item.ScheduledAt.ToString("yyyy-MM-ddTHH:mm"),
        DurationMinutes = item.DurationMinutes,
        Status = item.Status,
        Notes = item.Notes,
        BookingUrl = item.BookingUrl,
        SequenceNumber = item.SequenceNumber,
        JobRequestUnitId = item.JobRequestUnitId,
        AssignedUserId = item.AssignedUserId,
        AssignedUserName = item.Notes,
    };
}
