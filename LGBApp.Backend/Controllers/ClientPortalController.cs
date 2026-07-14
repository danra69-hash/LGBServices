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
[Authorize(Roles = "ClientAdmin,ClientSignatory")]
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
            return BadRequest(new { message = "Your account is not linked to a customer company." });

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
            return BadRequest(new { message = "Your account is not linked to a customer company." });

        var companyName = await _context.Customers
            .Where(c => c.CustomerId == customerId)
            .Select(c => c.Company)
            .FirstOrDefaultAsync() ?? string.Empty;

        var packages = await _context.CustomerPackages
            .Where(p => p.CustomerId == customerId && p.Status == "Active")
            .ToListAsync();

        var now = DateTime.UtcNow;
        var activeValue = packages.Sum(p => PackageProration.GetActiveValue(p, now));

        var jobs = await _context.JobRequests
            .Include(j => j.Units)
            .Where(j => j.CustomerId == customerId)
            .ToListAsync();

        var openJobs = jobs.Count(j => j.Status == "Pending" || j.Status == "In Progress");
        var completedJobs = jobs.Count(j => j.Status == "Completed");

        var currentUserId = AuthHelper.CurrentUserId(User);
        var teamCount = await _context.Users.CountAsync(u =>
            u.CustomerId == customerId
            && u.Role == UserRoles.ClientAdmin
            && (!currentUserId.HasValue || u.UserId != currentUserId.Value));

        return new ClientPortalSummaryDto
        {
            CompanyName = companyName,
            ActivePackages = packages.Count,
            ActivePackageValue = activeValue,
            OpenJobs = openJobs,
            CompletedJobs = completedJobs,
            TeamMembers = teamCount,
            CategoryProgress = await ClientPortalSummaryBuilder.BuildCategoryProgressAsync(_context, jobs),
        };
    }

    /// <summary>Client admin: toggle whether one MOI approver is enough or all must sign.</summary>
    [HttpPatch("moi-approval-mode")]
    [Authorize(Roles = "ClientAdmin")]
    public async Task<ActionResult<CustomerResponse>> UpdateMoiApprovalMode(
        [FromBody] UpdateMoiApprovalModeRequest request)
    {
        var customerId = AuthHelper.CurrentCustomerId(User);
        if (!customerId.HasValue)
            return BadRequest(new { message = "Your account is not linked to a customer company." });

        var mode = request.MoiApprovalMode?.Trim() ?? MoiApprovalModes.AllRequired;
        if (mode is not (MoiApprovalModes.AllRequired or MoiApprovalModes.AnyOne))
            return BadRequest(new { message = "moiApprovalMode must be AllRequired or AnyOne." });

        var customer = await _context.Customers
            .Include(c => c.AccountHolders)
            .Include(c => c.Packages)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (customer == null) return NotFound();

        customer.MoiApprovalMode = mode;
        await _context.SaveChangesAsync();
        return CustomerMapper.ToResponse(customer);
    }
}
