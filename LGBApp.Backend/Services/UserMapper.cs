using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public static class UserMapper
{
    public static UserResponse ToResponse(User user, Customer? customer = null) =>
        ToResponse(user, customer, null, null);

    public static async Task<UserResponse> ToResponseAsync(
        AppDbContext context,
        User user,
        Customer? customer = null)
    {
        List<AccountHolder>? linkedHolders = null;
        List<SignatoryCompanyAccessDto>? accessibleCompanies = null;

        if (string.Equals(user.Role, UserRoles.ClientSignatory, StringComparison.OrdinalIgnoreCase))
        {
            linkedHolders = await context.AccountHolders
                .Where(h => h.UserId == user.UserId)
                .ToListAsync();
            accessibleCompanies = await new SignatoryAccessService()
                .GetAccessibleCompaniesAsync(context, user);
        }

        return ToResponse(user, customer, linkedHolders, accessibleCompanies);
    }

    private static UserResponse ToResponse(
        User user,
        Customer? customer,
        List<AccountHolder>? linkedHolders,
        List<SignatoryCompanyAccessDto>? accessibleCompanies)
    {
        var response = new UserResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            Name = user.Name,
            Mobile = user.Mobile,
            Role = user.Role,
            JobTitle = user.JobTitle,
            CanRecommendMoi = user.CanRecommendMoi,
            CanApproveMoiIntake = user.CanApproveMoiIntake,
            CanApproveMoi = user.CanApproveMoi,
            CanApproveMoa = user.CanApproveMoa,
            IsInternalSignatory = user.IsInternalSignatory,
            CustomerId = user.CustomerId,
            CustomerName = user.Customer?.Company ?? customer?.Company,
            IsVerified = user.IsVerified,
            MustChangePassword = user.MustChangePassword,
            CreatedAt = user.CreatedAt,
            AccessibleCompanies = accessibleCompanies,
        };

        ApplySignatoryFlags(response, user, customer ?? user.Customer, linkedHolders);
        return response;
    }

    private static void ApplySignatoryFlags(
        UserResponse response,
        User user,
        Customer? customer,
        List<AccountHolder>? linkedHolders)
    {
        if (!string.Equals(user.Role, UserRoles.ClientSignatory, StringComparison.OrdinalIgnoreCase))
            return;

        var holders = linkedHolders?
            .Where(h => h.UserId == user.UserId)
            .ToList();

        if (holders is { Count: > 0 })
        {
            response.NeedsMoi = holders.Any(h => h.NeedsMoi);
            response.NeedsMoiApproval = holders.Any(h => h.NeedsMoiApproval);
            response.NeedsMoa = holders.Any(h => h.NeedsMoa);
            return;
        }

        if (customer?.AccountHolders == null)
            return;

        var holder = customer.AccountHolders.FirstOrDefault(h =>
            h.Name.Equals(user.Name, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(h.Email)
                && h.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase)));

        if (holder == null)
            return;

        response.NeedsMoi = holder.NeedsMoi;
        response.NeedsMoiApproval = holder.NeedsMoiApproval;
        response.NeedsMoa = holder.NeedsMoa;
    }
}
