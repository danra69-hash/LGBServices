using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

/// <summary>
/// Resolution / secretarial staff (e.g. Ng Poh Li, Nita) — internal User role, assigned per job.
/// </summary>
public static class SecretarialStaffService
{
    public static async Task<List<User>> GetSecretarialStaffAsync(AppDbContext context) =>
        await context.Users
            .Where(u => u.Role == UserRoles.User
                && u.CustomerId == null
                && !u.CanApproveMoiIntake
                && !u.CanApproveMoi
                && !u.CanApproveMoa)
            .OrderBy(u => u.Name)
            .ToListAsync();

    /// <summary>Internal resolution staff (User role, no approval hats) plus internal Admins.</summary>
    public static bool IsAssignableInternalStaff(User user) =>
        user.CustomerId == null
        && (user.Role == UserRoles.Admin || IsAssignableSecretarialUser(user));

    private static bool IsAssignableSecretarialUser(User user) =>
        user.Role == UserRoles.User
        && user.CustomerId == null
        && !user.CanApproveMoiIntake
        && !user.CanApproveMoi
        && !user.CanApproveMoa;

    public static bool IsReadyForSecretarialAssignment(JobRequest job, MOIForm? moiForm = null)
    {
        if (TaskFormVisibilityHelper.AwaitingIntakeApproval(job))
            return false;

        if (moiForm != null
            && moiForm.WorkflowState is MoiWorkflowStates.Approved or MoiWorkflowStates.PendingPrep or MoiWorkflowStates.PendingRecommendation)
            return true;

        if (job.TotalQty > 1
            && job.Units.Any(u => u.InternalHandoffStatus is JobHandoffStatuses.AwaitingSecAssignment
                or JobHandoffStatuses.PendingPrep
                or JobHandoffStatuses.ResoInProgress))
            return moiForm == null
                || moiForm.WorkflowState is MoiWorkflowStates.Approved
                    or MoiWorkflowStates.PendingPrep
                    or MoiWorkflowStates.PendingRecommendation;

        if (moiForm != null && moiForm.WorkflowState is
            MoiWorkflowStates.PendingPrep
            or MoiWorkflowStates.PendingRecommendation)
            return true;

        return job.InternalHandoffStatus is
            JobHandoffStatuses.PendingPrep
            or JobHandoffStatuses.ResoInProgress
            or JobHandoffStatuses.AdminReview
            or JobHandoffStatuses.MoaSharonApproved
            or JobHandoffStatuses.ReadyForMoa
            or JobHandoffStatuses.MoaCirculation;
    }
}
