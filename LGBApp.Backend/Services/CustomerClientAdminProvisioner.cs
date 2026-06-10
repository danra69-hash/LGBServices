using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

/// <summary>
/// Ensures every customer has a base ClientAdmin account (invitation-to-config entry point).
/// </summary>
public static class CustomerClientAdminProvisioner
{
    public const string DefaultPassword = "password123";

    public static async Task<User?> EnsureClientAdminAsync(AppDbContext context, Customer customer)
    {
        var existing = await context.Users
            .FirstOrDefaultAsync(u =>
                u.CustomerId == customer.CustomerId
                && u.Role == UserRoles.ClientAdmin);

        if (existing != null)
            return existing;

        var email = BuildDefaultEmail(customer);
        if (await context.Users.AnyAsync(u => u.Email == email))
        {
            email = $"clientadmin+{customer.CustomerId}+{Guid.NewGuid():N}".Substring(0, 40) + "@lgb.test";
        }

        var admin = new User
        {
            Email = email,
            PasswordHash = PasswordHasher.Hash(DefaultPassword),
            Name = $"{customer.Company} Admin",
            Mobile = customer.Phone ?? string.Empty,
            Role = UserRoles.ClientAdmin,
            CustomerId = customer.CustomerId,
            IsVerified = true,
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow,
        };

        context.Users.Add(admin);
        await context.SaveChangesAsync();
        return admin;
    }

    public static async Task EnsureAllCustomersHaveClientAdminAsync(AppDbContext context)
    {
        var customers = await context.Customers.ToListAsync();
        foreach (var customer in customers)
            await EnsureClientAdminAsync(context, customer);
    }

    public static string BuildDefaultEmail(Customer customer)
    {
        var slug = Slugify(customer.Company);
        if (string.IsNullOrWhiteSpace(slug))
            slug = $"customer-{customer.CustomerId}";
        return $"clientadmin+{slug}@lgb.test";
    }

    private static string Slugify(string value)
    {
        var chars = value.ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c))
            .Take(24)
            .ToArray();
        return new string(chars);
    }
}
