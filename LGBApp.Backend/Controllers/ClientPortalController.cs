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
[Authorize(Roles = "ClientAdmin,Client")]
public class ClientPortalController : ControllerBase
{
    private readonly AppDbContext _context;

    public ClientPortalController(AppDbContext context) => _context = context;

    /// <summary>Company profile and purchased packages — no LGB-wide metrics.</summary>
    [HttpGet("my-company")]
    public async Task<ActionResult<CustomerResponse>> GetMyCompany()
    {
        var customerId = AuthHelper.CurrentCustomerId(User);
        if (!customerId.HasValue)
            return BadRequest("Your account is not linked to a customer company.");

        var customer = await _context.Customers
            .Include(c => c.AccountHolders)
            .Include(c => c.Packages)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer == null) return NotFound("Company not found.");

        return CustomerMapper.ToResponse(customer);
    }

    /// <summary>Scoped summary for the client's own packages and jobs.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<ClientPortalSummaryDto>> GetSummary()
    {
        var customerId = AuthHelper.CurrentCustomerId(User);
        if (!customerId.HasValue)
            return BadRequest("Your account is not linked to a customer company.");

        var companyName = await _context.Customers
            .Where(c => c.CustomerId == customerId)
            .Select(c => c.Company)
            .FirstOrDefaultAsync() ?? string.Empty;

        var packages = await _context.CustomerPackages
            .Where(p => p.CustomerId == customerId && p.Status == "Active")
            .ToListAsync();

        var now = DateTime.UtcNow;
        var activeValue = packages.Sum(p => PackageProration.GetActiveValue(p, now));

        var jobsQuery = _context.JobRequests.Where(j => j.CustomerId == customerId);
        var openJobs = await jobsQuery.CountAsync(j => j.Status == "Pending" || j.Status == "In Progress");
        var completedJobs = await jobsQuery.CountAsync(j => j.Status == "Completed");

        var teamCount = 0;
        if (AuthHelper.IsClientAdmin(User))
        {
            teamCount = await _context.Users.CountAsync(u =>
                u.CustomerId == customerId && u.Role == UserRoles.Client);
        }

        return new ClientPortalSummaryDto
        {
            CompanyName = companyName,
            ActivePackages = packages.Count,
            ActivePackageValue = activeValue,
            OpenJobs = openJobs,
            CompletedJobs = completedJobs,
            TeamMembers = teamCount,
        };
    }
}
