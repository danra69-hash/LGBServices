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
public class CompletedServicesController : ControllerBase
{
    private readonly AppDbContext _context;

    public CompletedServicesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CompletedServiceResponse>>> GetCompletedServices(
        [FromQuery] string? search,
        [FromQuery] int? year)
    {
        var query = _context.CompletedServices.AsQueryable();

        if (year.HasValue)
            query = query.Where(s => s.DateCompleted.Year == year.Value);

        if (!AuthHelper.IsAdmin(User))
        {
            var userId = AuthHelper.CurrentUserId(User);
            if (!userId.HasValue)
                return Ok(Array.Empty<CompletedServiceResponse>());

            var userName = AuthHelper.CurrentUserName(User) ?? string.Empty;
            query = query.Where(s =>
                s.JobAssignedTo.ToLower() == userName.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(s =>
                s.Customer.ToLower().Contains(term) ||
                s.Service.ToLower().Contains(term) ||
                s.AccountHolder.ToLower().Contains(term) ||
                s.JobAssignedTo.ToLower().Contains(term));
        }

        var services = await query
            .OrderByDescending(s => s.DateCompleted)
            .ToListAsync();

        return services.Select(CompletedServiceMapper.ToResponse).ToList();
    }
}
