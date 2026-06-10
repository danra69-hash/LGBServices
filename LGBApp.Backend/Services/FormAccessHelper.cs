using System.Security.Claims;
using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public static class FormAccessHelper
{
    public static async Task<bool> CanAccessMoiFormAsync(AppDbContext context, ClaimsPrincipal user, MOIForm form)
    {
        if (AuthHelper.IsAdmin(user) || AuthHelper.IsInternalStaff(user))
            return true;

        if (!AuthHelper.IsExternalUser(user))
            return false;

        var job = form.JobRequestId.HasValue
            ? await context.JobRequests
                .Include(j => j.Units).ThenInclude(u => u.Assignees)
                .FirstOrDefaultAsync(j => j.JobRequestId == form.JobRequestId)
            : null;

        if (job != null)
            return TaskFormVisibilityHelper.CanViewMoiForm(user, job);

        var customer = await WorkflowService.ResolveCustomerForCompanyAsync(context, form.Company);
        return AuthHelper.CanAccessCustomer(user, customer?.CustomerId);
    }

    public static async Task<bool> CanAccessMoaFormAsync(AppDbContext context, ClaimsPrincipal user, MOAForm form)
    {
        if (AuthHelper.IsAdmin(user) || AuthHelper.IsInternalStaff(user))
            return true;

        if (!AuthHelper.IsExternalUser(user))
            return false;

        if (form.JobRequestId.HasValue)
        {
            var job = await context.JobRequests
                .Include(j => j.Units).ThenInclude(u => u.Assignees)
                .FirstOrDefaultAsync(j => j.JobRequestId == form.JobRequestId);
            if (job != null)
                return TaskFormVisibilityHelper.CanViewMoaForm(user, job);
        }

        var customer = await WorkflowService.ResolveCustomerForCompanyAsync(context, form.Company);
        return AuthHelper.CanAccessCustomer(user, customer?.CustomerId);
    }

    public static async Task<bool> CanAccessServiceJobFormAsync(AppDbContext context, ClaimsPrincipal user, ServiceJobForm form)
    {
        if (AuthHelper.IsAdmin(user) || AuthHelper.IsInternalStaff(user))
            return true;

        if (!AuthHelper.IsExternalUser(user))
            return false;

        var job = await context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees)
            .FirstOrDefaultAsync(j => j.JobRequestId == form.JobRequestId);

        if (job != null)
            return AuthHelper.CanAccessJob(user, job);

        var customer = await WorkflowService.ResolveCustomerForCompanyAsync(context, form.Company);
        return AuthHelper.CanAccessCustomer(user, customer?.CustomerId);
    }

    public static async Task<IQueryable<MOIForm>> ScopeMoiFormsAsync(AppDbContext context, ClaimsPrincipal user, IQueryable<MOIForm> query)
    {
        if (AuthHelper.IsAdmin(user) || AuthHelper.IsInternalStaff(user))
            return query;

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

        return query.Where(_ => false);
    }

    public static async Task<List<MOAForm>> FilterMoaFormsAsync(AppDbContext context, ClaimsPrincipal user, List<MOAForm> forms)
    {
        if (AuthHelper.IsAdmin(user) || AuthHelper.IsInternalStaff(user))
            return forms;

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
        if (AuthHelper.IsAdmin(user) || AuthHelper.IsInternalStaff(user))
            return forms;

        var allowed = new List<ServiceJobForm>();
        foreach (var form in forms)
        {
            if (await CanAccessServiceJobFormAsync(context, user, form))
                allowed.Add(form);
        }
        return allowed;
    }
}
