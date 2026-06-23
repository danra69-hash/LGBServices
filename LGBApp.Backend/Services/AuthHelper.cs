using System.Security.Claims;
using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public static class AuthHelper
{
    public static string? CurrentRole(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Role);

    public static bool IsAdmin(ClaimsPrincipal user) =>
        string.Equals(CurrentRole(user), UserRoles.Admin, StringComparison.OrdinalIgnoreCase);

    public static bool IsInternalStaff(ClaimsPrincipal user)
    {
        var role = CurrentRole(user);
        return string.Equals(role, UserRoles.Admin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, UserRoles.User, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsClientAdmin(ClaimsPrincipal user) =>
        string.Equals(CurrentRole(user), UserRoles.ClientAdmin, StringComparison.OrdinalIgnoreCase);

    public static bool IsClientSignatory(ClaimsPrincipal user) =>
        string.Equals(CurrentRole(user), UserRoles.ClientSignatory, StringComparison.OrdinalIgnoreCase);

    public static bool IsExternalUser(ClaimsPrincipal user) =>
        IsClientAdmin(user) || IsClientSignatory(user);

    public static int? CurrentUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }

    public static int? CurrentCustomerId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("customer_id");
        return int.TryParse(raw, out var id) ? id : null;
    }

    public static HashSet<int> GetAccessibleCustomerIds(ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue("accessible_customer_ids");
        if (!string.IsNullOrWhiteSpace(claim))
        {
            return claim
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var id) ? id : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();
        }

        var single = CurrentCustomerId(user);
        return single.HasValue ? [single.Value] : [];
    }

    public static string? CurrentUserName(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Name);

    public static bool CanManageSystem(ClaimsPrincipal user) => IsAdmin(user);

    public static bool CanApproveMoiIntake(ClaimsPrincipal user) =>
        string.Equals(
            user.FindFirstValue("can_approve_moi_intake"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public static bool CanApproveMoi(ClaimsPrincipal user) =>
        IsAdmin(user)
        || string.Equals(
            user.FindFirstValue("can_approve_moi"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public static bool CanApproveMoa(ClaimsPrincipal user) =>
        IsAdmin(user)
        || string.Equals(
            user.FindFirstValue("can_approve_moa"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public static bool HasMoiOversight(ClaimsPrincipal user) =>
        IsAdmin(user) || CanApproveMoiIntake(user) || CanApproveMoi(user);

    public static bool HasMoaOversight(ClaimsPrincipal user) =>
        IsAdmin(user) || CanApproveMoa(user);

    public static bool CanAccessCustomer(ClaimsPrincipal user, int? customerId)
    {
        if (IsAdmin(user) || string.Equals(CurrentRole(user), UserRoles.User, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!customerId.HasValue)
            return false;

        if (IsClientSignatory(user))
            return GetAccessibleCustomerIds(user).Contains(customerId.Value);

        var scopedCustomerId = CurrentCustomerId(user);
        return scopedCustomerId.HasValue && scopedCustomerId.Value == customerId.Value;
    }

    public static bool CanAccessJob(ClaimsPrincipal user, int? assignedUserId, string jobAssignedTo)
    {
        if (IsAdmin(user))
            return true;

        var userId = CurrentUserId(user);
        if (userId.HasValue && assignedUserId.HasValue)
            return userId.Value == assignedUserId.Value;

        var name = CurrentUserName(user);
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(jobAssignedTo))
            return false;

        if (string.Equals(name.Trim(), jobAssignedTo.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;

        return jobAssignedTo
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(part => string.Equals(part, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static bool CanAccessJob(ClaimsPrincipal user, JobRequest job)
    {
        if (IsAdmin(user))
            return true;

        if (IsClientSignatory(user))
            return IsSignatoryForJob(user, job);

        if (IsClientAdmin(user))
            return CanAccessCustomer(user, job.CustomerId);

        if (!IsAdmin(user) && TaskFormVisibilityHelper.AwaitingIntakeApproval(job))
            return CanApproveMoiIntake(user);

        var internalUserId = CurrentUserId(user);
        if (!internalUserId.HasValue)
            return false;

        return IsAssignedToJob(user, job);
    }

    /// <summary>True when the user is tagged on the job unit (or legacy job assignee fields).</summary>
    public static bool IsAssignedToJob(ClaimsPrincipal user, JobRequest job, int? jobRequestUnitId = null)
    {
        if (IsAdmin(user))
            return true;

        var userId = CurrentUserId(user);
        if (!userId.HasValue)
            return false;

        if (jobRequestUnitId.HasValue)
        {
            var scopedUnit = job.Units.FirstOrDefault(u => u.JobRequestUnitId == jobRequestUnitId.Value);
            if (scopedUnit != null && JobRequestUnitService.IsUserAssigned(scopedUnit, userId.Value))
                return true;
        }

        if (job.Units.Any(u => JobRequestUnitService.IsUserAssigned(u, userId.Value)))
            return true;

        return CanAccessJob(user, job.AssignedUserId, job.JobAssignedTo);
    }

    public static bool CanIssueClientJob(ClaimsPrincipal user) =>
        IsAdmin(user) || IsClientAdmin(user);

    public static async Task<bool> CanSignatoryIssueMoiAsync(
        AppDbContext context,
        ClaimsPrincipal user,
        JobRequest job)
    {
        if (!IsClientSignatory(user))
            return false;

        if (job.TaskType is not ("MOI" or "Service"))
            return false;

        if (job.TaskType == "Service" && string.IsNullOrWhiteSpace(job.AccountHolder))
        {
            // Any NeedsMoi signatory may start MOI on an unclaimed package line.
        }
        else if (!IsSignatoryForJob(user, job))
        {
            return false;
        }

        if (!job.CustomerId.HasValue)
            return false;

        var userName = CurrentUserName(user);
        if (string.IsNullOrWhiteSpace(userName))
            return false;

        var customer = await context.Customers
            .Include(c => c.AccountHolders)
            .FirstOrDefaultAsync(c => c.CustomerId == job.CustomerId);

        if (customer == null)
            return false;

        var userId = CurrentUserId(user);
        var holder = customer.AccountHolders.FirstOrDefault(h =>
            userId.HasValue && h.UserId == userId && h.NeedsMoi)
            ?? customer.AccountHolders.FirstOrDefault(h =>
                h.NeedsMoi && h.Name.Equals(userName.Trim(), StringComparison.OrdinalIgnoreCase));

        return holder != null;
    }

    public static bool CanManageUsers(ClaimsPrincipal user) =>
        IsAdmin(user) || IsClientAdmin(user);

    public static bool IsSignatoryForJob(ClaimsPrincipal user, JobRequest job)
    {
        if (!IsClientSignatory(user))
            return false;

        if (!CanAccessCustomer(user, job.CustomerId))
            return false;

        var email = user.FindFirstValue(ClaimTypes.Email);
        if (!string.IsNullOrWhiteSpace(email)
            && !string.IsNullOrWhiteSpace(job.AccountHolderEmail)
            && string.Equals(email.Trim(), job.AccountHolderEmail.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;

        var name = CurrentUserName(user);
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(job.AccountHolder))
            return false;

        return string.Equals(name.Trim(), job.AccountHolder.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> CreatableRoles(ClaimsPrincipal user)
    {
        if (IsAdmin(user))
            return UserRoles.All;
        if (IsClientAdmin(user))
            return [UserRoles.ClientAdmin, UserRoles.ClientSignatory];
        return [];
    }

    public static bool CanManageUser(ClaimsPrincipal actor, User target)
    {
        if (IsAdmin(actor))
            return true;

        if (!IsClientAdmin(actor))
            return false;

        var customerId = CurrentCustomerId(actor);
        if (!customerId.HasValue || target.CustomerId != customerId)
            return false;

        return string.Equals(target.Role, UserRoles.ClientSignatory, StringComparison.OrdinalIgnoreCase)
            || string.Equals(target.Role, UserRoles.ClientAdmin, StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanAssignClientJob(ClaimsPrincipal user, JobRequest job, User assignee)
    {
        if (IsAdmin(user))
            return true;

        if (!IsClientAdmin(user))
            return false;

        var customerId = CurrentCustomerId(user);
        if (!customerId.HasValue || job.CustomerId != customerId)
            return false;

        return assignee.CustomerId == customerId
            && string.Equals(assignee.Role, UserRoles.ClientAdmin, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Client admin can assign, unassign, complete, and revert jobs for their company only.</summary>
    public static bool CanManageClientJob(ClaimsPrincipal user, JobRequest job)
    {
        if (IsAdmin(user))
            return true;

        if (!IsClientAdmin(user))
            return false;

        var customerId = CurrentCustomerId(user);
        return customerId.HasValue && job.CustomerId == customerId;
    }
}
