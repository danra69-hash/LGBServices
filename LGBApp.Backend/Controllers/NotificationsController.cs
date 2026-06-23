using LGBApp.Backend.Data;
using LGBApp.Backend.Models.DTOs;
using LGBApp.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _context;

    public NotificationsController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationDto>>> GetMine([FromQuery] bool unreadOnly = false)
    {
        var userId = AuthHelper.CurrentUserId(User);
        if (!userId.HasValue) return Unauthorized();

        var query = _context.AppNotifications
            .AsNoTracking()
            .Where(n => n.UserId == userId.Value);

        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(100)
            .Select(n => new NotificationDto
            {
                Id = n.AppNotificationId,
                EventType = n.EventType,
                Title = n.Title,
                Message = n.Message,
                JobRequestId = n.JobRequestId,
                CustomerId = n.CustomerId,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userId = AuthHelper.CurrentUserId(User);
        if (!userId.HasValue) return Unauthorized();

        var notification = await _context.AppNotifications
            .FirstOrDefaultAsync(n => n.AppNotificationId == id && n.UserId == userId.Value);
        if (notification == null) return NotFound();

        notification.IsRead = true;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
