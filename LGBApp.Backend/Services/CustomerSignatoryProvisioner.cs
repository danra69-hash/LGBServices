using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

/// <summary>
/// Creates scoped ClientSignatory logins for account holders flagged for MOI / MOI Approval / MOA.
/// </summary>
public static class CustomerSignatoryProvisioner
{
    public const string DefaultPassword = "password123";

    public static void ApplyHolderFlagsFromCustomerLists(Customer customer)
    {
        var moi = JsonHelper.Deserialize<List<string>>(customer.MoiJson);
        var moiApproval = JsonHelper.Deserialize<List<string>>(customer.MoiApprovalJson);
        var moa = JsonHelper.Deserialize<List<string>>(customer.MoaJson);

        foreach (var holder in customer.AccountHolders)
        {
            holder.NeedsMoi = moi.Contains(holder.Name, StringComparer.OrdinalIgnoreCase);
            holder.NeedsMoiApproval = moiApproval.Contains(holder.Name, StringComparer.OrdinalIgnoreCase);
            holder.NeedsMoa = moa.Contains(holder.Name, StringComparer.OrdinalIgnoreCase);
        }
    }

    public static void SyncCustomerSignerLists(Customer customer)
    {
        customer.MoiJson = JsonHelper.Serialize(
            customer.AccountHolders.Where(h => h.NeedsMoi).Select(h => h.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList());
        customer.MoiApprovalJson = JsonHelper.Serialize(
            customer.AccountHolders.Where(h => h.NeedsMoiApproval).Select(h => h.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList());
        customer.MoaJson = JsonHelper.Serialize(
            customer.AccountHolders.Where(h => h.NeedsMoa).Select(h => h.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList());
    }

    public static async Task SyncSignatoriesAsync(
        AppDbContext context,
        Customer customer,
        int? provisionedByUserId = null,
        bool clientAdded = false)
    {
        ApplyHolderFlagsFromCustomerLists(customer);
        SyncCustomerSignerLists(customer);

        foreach (var holder in customer.AccountHolders)
        {
            if (!holder.NeedsMoi && !holder.NeedsMoiApproval && !holder.NeedsMoa)
                continue;

            if (string.IsNullOrWhiteSpace(holder.Email))
                continue;

            await EnsureSignatoryUserAsync(context, customer, holder, provisionedByUserId, clientAdded);
        }

        await context.SaveChangesAsync();
    }

    public static async Task<User?> EnsureSignatoryUserAsync(
        AppDbContext context,
        Customer customer,
        AccountHolder holder,
        int? provisionedByUserId,
        bool clientAdded)
    {
        if (string.IsNullOrWhiteSpace(holder.Email))
            return null;

        User? user = null;
        if (holder.UserId.HasValue)
            user = await context.Users.FindAsync(holder.UserId.Value);

        user ??= await context.Users.FirstOrDefaultAsync(u =>
            u.Role == UserRoles.ClientSignatory
            && u.Email == holder.Email.Trim());

        if (user != null)
        {
            user.Mobile = holder.Phone ?? user.Mobile;
            user.CustomerId ??= customer.CustomerId;
            holder.UserId = user.UserId;
            await new SignatoryAccessService().EnsureAccessAsync(context, user.UserId, customer.CustomerId);
            return user;
        }

        user = new User
        {
            Email = holder.Email.Trim(),
            PasswordHash = PasswordHasher.Hash(DefaultPassword),
            Name = holder.Name,
            Mobile = holder.Phone ?? string.Empty,
            Role = UserRoles.ClientSignatory,
            CustomerId = customer.CustomerId,
            InvitedByUserId = provisionedByUserId,
            IsVerified = true,
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow,
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        holder.UserId = user.UserId;
        await new SignatoryAccessService().EnsureAccessAsync(context, user.UserId, customer.CustomerId);
        if (clientAdded)
        {
            holder.ClientAdded = true;
            holder.AddedByUserId = provisionedByUserId;
        }

        return user;
    }

    public static async Task LinkHolderForNewSignatoryUserAsync(AppDbContext context, int customerId, User user)
    {
        var customer = await context.Customers
            .Include(c => c.AccountHolders)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (customer == null)
            return;

        var holder = customer.AccountHolders.FirstOrDefault(h =>
            string.Equals(h.Email, user.Email, StringComparison.OrdinalIgnoreCase)
            || string.Equals(h.Name, user.Name, StringComparison.OrdinalIgnoreCase));

        if (holder == null)
        {
            holder = new AccountHolder
            {
                CustomerId = customerId,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Mobile,
                NeedsMoi = true,
            };
            customer.AccountHolders.Add(holder);
        }

        holder.UserId = user.UserId;
        await new SignatoryAccessService().EnsureAccessAsync(context, user.UserId, customerId);
        SyncCustomerSignerLists(customer);
        await context.SaveChangesAsync();
    }

    public static async Task EnsureAllCustomerSignatoriesAsync(AppDbContext context)
    {
        var customers = await context.Customers
            .Include(c => c.AccountHolders)
            .ToListAsync();

        foreach (var customer in customers)
            await SyncSignatoriesAsync(context, customer);
    }
}
