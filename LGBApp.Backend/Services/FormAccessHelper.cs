using System.Security.Claims;
using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public static class FormAccessHelper
{
    public static async Task<bool> CanAccessMoiFormAsync(AppDbContext context, ClaimsPrincipal user, MOIForm form)
    {
        if (AuthHelper.IsExternalUser(user))
        {
            var job = form.JobRequestId.HasValue
                ? await context.JobRequests
                    .Include(j => j.Units).ThenInclude(u => u.Assignees)
                    .FirstOrDefaultAsync(j => j.JobRequestId == form.JobRequestId)
                : null;

            if (job != null && TaskFormVisibilityHelper.CanViewMoiForm(user, job, form))
                return true;

            if (await CanExternalUserAccessMoiApprovalFormAsync(context, user, form))
                return true;

            var customer = await WorkflowService.ResolveCustomerForCompanyAsync(context, form.Company);
            return AuthHelper.CanAccessCustomer(user, customer?.CustomerId);
        }

        if (!AuthHelper.IsInternalStaff(user))
            return false;

        if (form.JobRequestId.HasValue)
        {
            var job = await context.JobRequests
                .Include(j => j.Units).ThenInclude(u => u.Assignees)
                .FirstOrDefaultAsync(j => j.JobRequestId == form.JobRequestId);
            if (job != null)
                return TaskFormVisibilityHelper.CanViewMoiForm(user, job, form);
        }

        return AuthHelper.HasMoiOversight(user);
    }

    public static async Task<bool> CanAccessMoaFormAsync(AppDbContext context, ClaimsPrincipal user, MOAForm form)
    {
        if (AuthHelper.IsExternalUser(user))
        {
            if (form.JobRequestId.HasValue)
            {
                var job = await context.JobRequests
                    .Include(j => j.Units).ThenInclude(u => u.Assignees)
                    .FirstOrDefaultAsync(j => j.JobRequestId == form.JobRequestId);
                if (job != null)
                    return TaskFormVisibilityHelper.CanViewMoaForm(user, job, form);
            }

            var customer = await WorkflowService.ResolveCustomerForCompanyAsync(context, form.Company);
            return AuthHelper.CanAccessCustomer(user, customer?.CustomerId);
        }

        if (!AuthHelper.IsInternalStaff(user))
            return false;

        if (form.JobRequestId.HasValue)
        {
            var job = await context.JobRequests
                .Include(j => j.Units).ThenInclude(u => u.Assignees)
                .FirstOrDefaultAsync(j => j.JobRequestId == form.JobRequestId);
            if (job != null)
            {
                var moi = await ResolveLinkedMoiAsync(context, form);
                return TaskFormVisibilityHelper.CanViewMoaForm(user, job, form, moi);
            }
        }

        return AuthHelper.HasMoaOversight(user);
    }

    public static async Task<bool> CanAccessServiceJobFormAsync(AppDbContext context, ClaimsPrincipal user, ServiceJobForm form)
    {
        if (AuthHelper.IsAdmin(user))
            return true;

        if (AuthHelper.IsExternalUser(user))
        {
            var job = await context.JobRequests
                .Include(j => j.Units).ThenInclude(u => u.Assignees)
                .FirstOrDefaultAsync(j => j.JobRequestId == form.JobRequestId);

            if (job != null)
                return AuthHelper.CanAccessJob(user, job);

            var customer = await WorkflowService.ResolveCustomerForCompanyAsync(context, form.Company);
            return AuthHelper.CanAccessCustomer(user, customer?.CustomerId);
        }

        if (!AuthHelper.IsInternalStaff(user))
            return false;

        var linkedJob = await context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees)
            .FirstOrDefaultAsync(j => j.JobRequestId == form.JobRequestId);

        return linkedJob != null && AuthHelper.CanAccessJob(user, linkedJob);
    }

    public static async Task<IQueryable<MOIForm>> ScopeMoiFormsAsync(AppDbContext context, ClaimsPrincipal user, IQueryable<MOIForm> query)
    {
        if (AuthHelper.HasMoiOversight(user))
            return query;

        if (AuthHelper.IsInternalStaff(user))
        {
            var userId = AuthHelper.CurrentUserId(user);
            if (!userId.HasValue)
                return query.Where(_ => false);

            var accessibleJobIds = await GetAssignedInternalJobIdsAsync(context, userId.Value);
            return query.Where(f => f.JobRequestId.HasValue && accessibleJobIds.Contains(f.JobRequestId.Value));
        }

        var customerId = AuthHelper.CurrentCustomerId(user);
        if (!customerId.HasValue)
            return query.Where(_ => false);

        var company = await context.Customers
            .Where(c => c.CustomerId == customerId)
            .Select(c => c.Company)
            .FirstOrDefaultAsync();

        var jobIds = await context.JobRequests
            .Where(j => j.CustomerId == customerId)
            .Select(j => j.JobRequestId)
            .ToListAsync();

        if (AuthHelper.IsClientAdmin(user))
        {
            return query.Where(f =>
                (f.JobRequestId.HasValue && jobIds.Contains(f.JobRequestId.Value))
                || f.Company == company);
        }

        if (AuthHelper.IsClientSignatory(user))
        {
            var signatoryName = AuthHelper.CurrentUserName(user) ?? string.Empty;
            var signatoryJobIds = await context.JobRequests
                .Where(j => j.CustomerId == customerId
                    && j.AccountHolder.ToLower() == signatoryName.ToLower())
                .Select(j => j.JobRequestId)
                .ToListAsync();

            var approvalJobIds = await context.JobRequests
                .Where(j => j.CustomerId == customerId
                    && (j.TaskType == "MOI" || j.TaskType == "Service"))
                .Select(j => j.JobRequestId)
                .ToListAsync();

            var approvalFormIds = await context.MOIForms
                .Where(f => f.WorkflowState == MoiWorkflowStates.PendingClientMoiApproval
                    && f.JobRequestId != null
                    && approvalJobIds.Contains(f.JobRequestId.Value))
                .Select(f => f.MOIFormId)
                .ToListAsync();

            return query.Where(f =>
                (f.JobRequestId.HasValue && signatoryJobIds.Contains(f.JobRequestId.Value))
                || approvalFormIds.Contains(f.MOIFormId));
        }

        return query.Where(_ => false);
    }

    public static async Task<List<MOAForm>> FilterMoaFormsAsync(AppDbContext context, ClaimsPrincipal user, List<MOAForm> forms)
    {
        if (AuthHelper.HasMoaOversight(user))
            return forms;

        if (AuthHelper.IsInternalStaff(user))
        {
            var userId = AuthHelper.CurrentUserId(user);
            if (!userId.HasValue)
                return [];

            var accessibleJobIds = await GetAssignedInternalJobIdsAsync(context, userId.Value);
            return forms
                .Where(f => f.JobRequestId.HasValue && accessibleJobIds.Contains(f.JobRequestId.Value))
                .ToList();
        }

        var allowed = new List<MOAForm>();
        foreach (var form in forms)
        {
            if (await CanAccessMoaFormAsync(context, user, form))
                allowed.Add(form);
        }
        return allowed;
    }

    public static async Task<List<ServiceJobForm>> FilterServiceJobFormsAsync(AppDbContext context, ClaimsPrincipal user, List<ServiceJobForm> forms)
    {
        if (AuthHelper.IsAdmin(user))
            return forms;

        var allowed = new List<ServiceJobForm>();
        foreach (var form in forms)
        {
            if (await CanAccessServiceJobFormAsync(context, user, form))
                allowed.Add(form);
        }
        return allowed;
    }

    private static async Task<bool> CanExternalUserAccessMoiApprovalFormAsync(
        AppDbContext context,
        ClaimsPrincipal user,
        MOIForm form)
    {
        if (!AuthHelper.IsClientSignatory(user))
            return false;

        var name = AuthHelper.CurrentUserName(user);
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var customer = await WorkflowService.ResolveCustomerForCompanyAsync(context, form.Company);
        if (customer == null || !AuthHelper.CanAccessCustomer(user, customer.CustomerId))
            return false;

        if (form.WorkflowState != MoiWorkflowStates.PendingClientMoiApproval)
            return false;

        var required = ClientApprovalService.GetRequiredMoiApproverNames(customer);
        return required.Any(n => n.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<MOIForm?> ResolveLinkedMoiAsync(AppDbContext context, MOAForm form)
    {
        if (form.MOIFormId.HasValue)
            return await context.MOIForms.FindAsync(form.MOIFormId.Value);

        if (!form.JobRequestId.HasValue)
            return null;

        return await context.MOIForms
            .Where(f => f.JobRequestId == form.JobRequestId
                && (form.JobRequestUnitId == null
                    ? f.JobRequestUnitId == null
                    : f.JobRequestUnitId == form.JobRequestUnitId))
            .OrderByDescending(f => f.UpdatedAt)
            .FirstOrDefaultAsync();
    }

    private static async Task<List<int>> GetAssignedInternalJobIdsAsync(AppDbContext context, int userId) =>
        await context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees)
            .Where(j => !TaskFormVisibilityHelper.JobHandoffAwaitingIntake(j))
            .Where(j => j.Units.Any(u => JobRequestUnitService.IsUserAssigned(u, userId))
                || j.AssignedUserId == userId)
            .Select(j => j.JobRequestId)
            .ToListAsync();
}
