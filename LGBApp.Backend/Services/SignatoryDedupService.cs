using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public class SignatoryDedupService
{
    private readonly SignatoryAccessService _accessService;

    public SignatoryDedupService(SignatoryAccessService accessService)
    {
        _accessService = accessService;
    }

    public async Task<List<SignatoryOverlapDto>> FindOverlapsAsync(AppDbContext context)
    {
        var holders = await context.AccountHolders
            .Include(h => h.Customer)
            .Where(h => h.Email != null && h.Email != "")
            .Where(h => h.NeedsMoi || h.NeedsMoiApproval || h.NeedsMoa)
            .ToListAsync();

        var groups = holders
            .GroupBy(h => SignatoryAccessService.NormalizeEmail(h.Email))
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .Select(g => BuildOverlap(g.Key, g.ToList()))
            .Where(o => o.CompanyCount > 1 || !o.IsLinked)
            .OrderBy(o => o.Email)
            .ToList();

        return groups;
    }

    public async Task<SignatoryLinkResultDto> LinkByEmailAsync(AppDbContext context, string email)
    {
        var normalized = SignatoryAccessService.NormalizeEmail(email);
        if (string.IsNullOrEmpty(normalized))
            throw new InvalidOperationException("Email is required.");

        var holders = await context.AccountHolders
            .Include(h => h.Customer)
            .Where(h => h.Email != null && h.Email != "")
            .Where(h => h.NeedsMoi || h.NeedsMoiApproval || h.NeedsMoa)
            .ToListAsync();

        var matched = holders
            .Where(h => SignatoryAccessService.NormalizeEmail(h.Email) == normalized)
            .ToList();

        if (matched.Count == 0)
            throw new InvalidOperationException("No signatory account holders found for that email.");

        var user = await FindOrCreateSignatoryUserAsync(context, matched);
        var linked = 0;

        foreach (var holder in matched)
        {
            holder.UserId = user.UserId;
            await _accessService.EnsureAccessAsync(context, user.UserId, holder.CustomerId);
            linked++;
        }

        if (!user.CustomerId.HasValue)
            user.CustomerId = matched[0].CustomerId;

        await context.SaveChangesAsync();

        var customerIds = matched.Select(h => h.CustomerId).Distinct().OrderBy(id => id).ToList();
        return new SignatoryLinkResultDto
        {
            Email = normalized,
            UserId = user.UserId,
            HoldersLinked = linked,
            CustomerIds = customerIds,
            Message = $"Linked {linked} account holder(s) across {customerIds.Count} companies to one signatory login.",
        };
    }

    private static SignatoryOverlapDto BuildOverlap(string email, List<AccountHolder> holders)
    {
        var userIds = holders
            .Where(h => h.UserId.HasValue)
            .Select(h => h.UserId!.Value)
            .Distinct()
            .ToList();

        var isLinked = userIds.Count == 1 && holders.All(h => h.UserId == userIds[0]);

        return new SignatoryOverlapDto
        {
            Email = email,
            PrimaryName = holders[0].Name,
            CompanyCount = holders.Select(h => h.CustomerId).Distinct().Count(),
            IsLinked = isLinked,
            LinkedUserId = userIds.Count == 1 ? userIds[0] : null,
            Companies = holders
                .OrderBy(h => h.Customer.Company)
                .Select(h => new SignatoryOverlapCompanyDto
                {
                    CustomerId = h.CustomerId,
                    Company = h.Customer.Company,
                    AccountHolderId = h.AccountHolderId,
                    HolderName = h.Name,
                    NeedsMoi = h.NeedsMoi,
                    NeedsMoiApproval = h.NeedsMoiApproval,
                    NeedsMoa = h.NeedsMoa,
                    UserId = h.UserId,
                })
                .ToList(),
        };
    }

    private static async Task<User> FindOrCreateSignatoryUserAsync(AppDbContext context, List<AccountHolder> holders)
    {
        var existingUserId = holders
            .Where(h => h.UserId.HasValue)
            .Select(h => h.UserId!.Value)
            .Distinct()
            .ToList();

        if (existingUserId.Count == 1)
        {
            var existing = await context.Users.FindAsync(existingUserId[0]);
            if (existing != null)
                return existing;
        }

        if (existingUserId.Count > 1)
        {
            var users = await context.Users
                .Where(u => existingUserId.Contains(u.UserId))
                .OrderBy(u => u.UserId)
                .ToListAsync();

            var keeper = users.FirstOrDefault(u =>
                string.Equals(u.Role, UserRoles.ClientSignatory, StringComparison.OrdinalIgnoreCase))
                ?? users[0];

            return keeper;
        }

        var primary = holders[0];
        var email = primary.Email.Trim();
        var byEmail = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (byEmail != null)
        {
            if (!string.Equals(byEmail.Role, UserRoles.ClientSignatory, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Email {email} is already used by a non-signatory user.");

            return byEmail;
        }

        var user = new User
        {
            Email = email,
            PasswordHash = PasswordHasher.Hash(CustomerSignatoryProvisioner.DefaultPassword),
            Name = primary.Name,
            Mobile = primary.Phone ?? string.Empty,
            Role = UserRoles.ClientSignatory,
            CustomerId = primary.CustomerId,
            IsVerified = true,
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow,
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
