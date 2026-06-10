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
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("stats")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DashboardStatsResponse>> GetStats()
    {
        var now = DateTime.UtcNow;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = thisMonthStart.AddMonths(-1);

        var activeCustomers = await _context.Customers.CountAsync(c => c.Status == "Active");

        var activeCustomersLastMonth = await _context.Customers.CountAsync(c =>
            c.Status == "Active" && c.PurchasedDate < thisMonthStart);

        // SQLite does not support Sum on decimal — aggregate prorated active values in memory
        var activePackages = await _context.CustomerPackages.ToListAsync();
        var packageRevenue = activePackages
            .Sum(p => PackageProration.GetActiveValue(p, now));

        var totalRevenue = packageRevenue > 0
            ? packageRevenue
            : (decimal)(await _context.Customers
                .Select(c => (double)c.Value)
                .ToListAsync()).Sum();

        var revenueThisMonth = activePackages
            .Where(p => p.PurchasedDate >= thisMonthStart)
            .Sum(p => PackageProration.GetActiveValue(p, now));

        var revenueLastMonth = activePackages
            .Where(p => p.PurchasedDate >= lastMonthStart && p.PurchasedDate < thisMonthStart)
            .Sum(p => PackageProration.GetActiveValue(p, lastMonthStart.AddDays(15)));

        var outstandingServices = await _context.JobRequests
            .CountAsync(j => j.Status == "Pending" || j.Status == "In Progress");

        var outstandingLastMonth = await _context.JobRequests
            .CountAsync(j =>
                (j.Status == "Pending" || j.Status == "In Progress")
                && j.DateRequested < thisMonthStart);

        var totalCompleted = await _context.CompletedServices
            .CountAsync(s => s.Status == "Completed");

        var completedThisMonth = await _context.CompletedServices
            .CountAsync(s => s.Status == "Completed" && s.DateCompleted >= thisMonthStart);

        var completedLastMonth = await _context.CompletedServices
            .CountAsync(s => s.Status == "Completed"
                && s.DateCompleted >= lastMonthStart
                && s.DateCompleted < thisMonthStart);

        var adHocCount = await _context.CompletedServices.CountAsync();
        var adHocRevenue = (decimal)(await _context.Products
            .Select(p => (double)p.AddOnPrice)
            .ToListAsync()).Sum();

        var adHocThisMonth = await _context.CompletedServices
            .CountAsync(s => s.DateCompleted >= thisMonthStart);

        var adHocLastMonth = await _context.CompletedServices
            .CountAsync(s => s.DateCompleted >= lastMonthStart && s.DateCompleted < thisMonthStart);

        return new DashboardStatsResponse
        {
            ActiveCustomers = activeCustomers,
            TotalRevenue = totalRevenue,
            OutstandingServices = outstandingServices,
            TotalServicesCompleted = totalCompleted,
            AdHocServicesCount = adHocCount,
            AdHocRevenue = adHocRevenue,
            ActiveCustomersChange = FormatChange(activeCustomers, activeCustomersLastMonth),
            TotalRevenueChange = FormatChange(revenueThisMonth, revenueLastMonth),
            OutstandingServicesChange = FormatChange(outstandingServices, outstandingLastMonth),
            TotalServicesCompletedChange = FormatChange(completedThisMonth, completedLastMonth),
            AdHocRevenueChange = FormatChange(adHocThisMonth, adHocLastMonth),
        };
    }

    private static string FormatChange(decimal current, decimal previous)
    {
        if (previous == 0)
            return current > 0 ? "+100%" : "+0%";

        var pct = (current - previous) / previous * 100;
        return $"{(pct >= 0 ? "+" : "")}{pct:F0}%";
    }

    private static string FormatChange(int current, int previous)
    {
        if (previous == 0)
            return current > 0 ? "+100%" : "+0%";

        var pct = (current - previous) * 100.0 / previous;
        return $"{(pct >= 0 ? "+" : "")}{pct:F0}%";
    }
}
