using System.Security.Claims;
using LGBApp.Backend.Models;

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

    public static bool IsExternalUser(ClaimsPrincipal user) => IsClientAdmin(user);

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

    public static string? CurrentUserName(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Name);

    public static bool CanManageSystem(ClaimsPrincipal user) => IsAdmin(user);

    public static bool CanApproveMoiIntake(ClaimsPrincipal user) =>
        string.Equals(
            user.FindFirstValue("can_approve_moi_intake"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public static bool CanAccessCustomer(ClaimsPrincipal user, int? customerId)
    {
        if (IsAdmin(user) || string.Equals(CurrentRole(user), UserRoles.User, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!customerId.HasValue)
            return false;

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

        if (IsExternalUser(user))
        {
            if (!CanAccessCustomer(user, job.CustomerId))
                return false;

            return true;
        }

        if (!IsAdmin(user) && TaskFormVisibilityHelper.AwaitingIntakeApproval(job))
            return false;

        var internalUserId = CurrentUserId(user);
        if (!internalUserId.HasValue)
            return false;

        if (job.Units.Any(u => JobRequestUnitService.IsUserAssigned(u, internalUserId.Value)))
            return true;

        return CanAccessJob(user, job.AssignedUserId, job.JobAssignedTo);
    }

    public static bool CanIssueClientJob(ClaimsPrincipal user) =>
        IsAdmin(user) || IsClientAdmin(user);

    public static bool CanManageUsers(ClaimsPrincipal user) =>
        IsAdmin(user) || IsClientAdmin(user);

    public static IReadOnlyList<string> CreatableRoles(ClaimsPrincipal user)
    {
        if (IsAdmin(user))
            return UserRoles.All;
        if (IsClientAdmin(user))
            return [UserRoles.ClientAdmin];
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

        return string.Equals(target.Role, UserRoles.ClientAdmin, StringComparison.OrdinalIgnoreCase);
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
