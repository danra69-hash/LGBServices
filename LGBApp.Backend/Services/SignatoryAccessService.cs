using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public class SignatoryAccessService
{
    public static string NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();

    public async Task<IReadOnlyList<int>> GetAccessibleCustomerIdsAsync(AppDbContext context, User user)
    {
        if (!string.Equals(user.Role, UserRoles.ClientSignatory, StringComparison.OrdinalIgnoreCase))
            return user.CustomerId.HasValue ? [user.CustomerId.Value] : [];

        var fromAccess = await context.SignatoryCustomerAccess
            .Where(a => a.UserId == user.UserId)
            .Select(a => a.CustomerId)
            .ToListAsync();

        var fromHolders = await context.AccountHolders
            .Where(h => h.UserId == user.UserId)
            .Select(h => h.CustomerId)
            .ToListAsync();

        var ids = fromAccess
            .Concat(fromHolders)
            .Concat(user.CustomerId.HasValue ? [user.CustomerId.Value] : Array.Empty<int>())
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        return ids;
    }

    public async Task<List<SignatoryCompanyAccessDto>> GetAccessibleCompaniesAsync(AppDbContext context, User user)
    {
        var ids = await GetAccessibleCustomerIdsAsync(context, user);
        if (ids.Count == 0)
            return [];

        return await context.Customers
            .Where(c => ids.Contains(c.CustomerId))
            .OrderBy(c => c.Company)
            .Select(c => new SignatoryCompanyAccessDto
            {
                CustomerId = c.CustomerId,
                Company = c.Company,
            })
            .ToListAsync();
    }

    public async Task EnsureAccessAsync(AppDbContext context, int userId, int customerId)
    {
        var exists = await context.SignatoryCustomerAccess
            .AnyAsync(a => a.UserId == userId && a.CustomerId == customerId);
        if (exists)
            return;

        context.SignatoryCustomerAccess.Add(new SignatoryCustomerAccess
        {
            UserId = userId,
            CustomerId = customerId,
            CreatedAt = DateTime.UtcNow,
        });
    }

    public async Task BackfillFromHoldersAsync(AppDbContext context)
    {
        var links = await context.AccountHolders
            .Where(h => h.UserId.HasValue)
            .Select(h => new { h.UserId, h.CustomerId })
            .Distinct()
            .ToListAsync();

        foreach (var link in links)
        {
            if (!link.UserId.HasValue)
                continue;

            await EnsureAccessAsync(context, link.UserId.Value, link.CustomerId);
        }

        await context.SaveChangesAsync();
    }
}
