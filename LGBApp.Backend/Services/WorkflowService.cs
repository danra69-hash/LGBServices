using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public static class WorkflowService
{
    public static async Task<Customer?> ResolveCustomerForCompanyAsync(AppDbContext context, string company)
    {
        if (string.IsNullOrWhiteSpace(company))
            return null;
        return await context.Customers
            .Include(c => c.AccountHolders)
            .FirstOrDefaultAsync(c => c.Company == company);
    }

    /// <summary>Prefer FK, fall back to company name (display string).</summary>
    public static async Task<Customer?> ResolveCustomerForMoiAsync(AppDbContext context, MOIForm form)
    {
        if (form.CustomerId.HasValue)
        {
            return await context.Customers
                .Include(c => c.AccountHolders)
                .FirstOrDefaultAsync(c => c.CustomerId == form.CustomerId.Value);
        }

        var customer = await ResolveCustomerForCompanyAsync(context, form.Company);
        if (customer != null && form.CustomerId == null)
            form.CustomerId = customer.CustomerId;
        return customer;
    }

    public static async Task<Customer?> ResolveCustomerForMoaAsync(AppDbContext context, MOAForm form)
    {
        if (form.CustomerId.HasValue)
        {
            return await context.Customers
                .Include(c => c.AccountHolders)
                .FirstOrDefaultAsync(c => c.CustomerId == form.CustomerId.Value);
        }

        var customer = await ResolveCustomerForCompanyAsync(context, form.Company);
        if (customer != null && form.CustomerId == null)
            form.CustomerId = customer.CustomerId;
        return customer;
    }

    /// <summary>EF2: one query for distinct companies → dictionary (case-insensitive).</summary>
    public static async Task<Dictionary<string, Customer>> ResolveCustomersByCompanyAsync(
        AppDbContext context,
        IEnumerable<string> companies)
    {
        var keys = companies
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (keys.Count == 0)
            return new Dictionary<string, Customer>(StringComparer.OrdinalIgnoreCase);

        var rows = await context.Customers
            .Include(c => c.AccountHolders)
            .Where(c => keys.Contains(c.Company))
            .ToListAsync();

        var map = new Dictionary<string, Customer>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
            map.TryAdd(row.Company, row);
        return map;
    }

    public static string ResolveMoaTemplateCode(Customer? customer, DivisionGroup? divisionGroup)
    {
        if (customer != null && !string.IsNullOrWhiteSpace(customer.MoaFormTemplateCode))
            return customer.MoaFormTemplateCode;

        if (divisionGroup != null && !string.IsNullOrWhiteSpace(divisionGroup.DefaultMoaFormTemplateCode))
            return divisionGroup.DefaultMoaFormTemplateCode;

        return "MOA_DEFAULT";
    }

    public static string ResolveMoiTemplateCode(Customer? customer, DivisionGroup? divisionGroup)
    {
        if (customer != null && !string.IsNullOrWhiteSpace(customer.MoiFormTemplateCode))
            return customer.MoiFormTemplateCode;

        if (divisionGroup != null && !string.IsNullOrWhiteSpace(divisionGroup.DefaultMoiFormTemplateCode))
            return divisionGroup.DefaultMoiFormTemplateCode;

        return "MOI_DEFAULT";
    }

    public static async Task<string> ResolveMoiTemplateCodeAsync(
        AppDbContext context,
        Customer? customer,
        DivisionGroup? divisionGroup,
        string? serviceName)
    {
        if (!string.IsNullOrWhiteSpace(serviceName))
        {
            var normalized = serviceName.Trim();
            var candidates = await context.FormTemplates
                .Where(t => t.IsActive
                    && t.FormType == "MOI"
                    && t.PackageServiceName != null
                    && t.PackageServiceName != "")
                .ToListAsync();

            var match = candidates.FirstOrDefault(t =>
                string.Equals(t.PackageServiceName, normalized, StringComparison.OrdinalIgnoreCase)
                || normalized.Contains(t.PackageServiceName, StringComparison.OrdinalIgnoreCase)
                || t.PackageServiceName.Contains(normalized, StringComparison.OrdinalIgnoreCase));

            if (match != null)
                return match.Code;
        }

        return ResolveMoiTemplateCode(customer, divisionGroup);
    }

    public static async Task<string> ResolveMoaWorkflowTemplateCodeAsync(AppDbContext context, Customer? customer)
    {
        if (customer != null && !string.IsNullOrWhiteSpace(customer.MoaWorkflowTemplateCode))
            return customer.MoaWorkflowTemplateCode.Trim();

        if (customer == null || string.IsNullOrWhiteSpace(customer.DivisionGroupCode))
            return customer?.HasLoa == true ? "MOA_WITH_LOA" : "MOA_NO_LOA";

        var group = await context.DivisionGroups
            .FirstOrDefaultAsync(g => g.Code == customer.DivisionGroupCode);

        if (group != null)
            return group.MoaWorkflowTemplateCode;

        return customer.HasLoa ? "MOA_WITH_LOA" : "MOA_NO_LOA";
    }

    public static WorkflowConditions ParseConditions(bool financeRelated, bool bankSignatory, bool shareMovement = false) =>
        new() { FinanceRelated = financeRelated, BankSignatory = bankSignatory, ShareMovement = shareMovement };

    public static bool StepApplies(WorkflowStepTemplate step, WorkflowConditions conditions, Customer? customer)
    {
        return step.ConditionType switch
        {
            "Always" => true,
            "FinanceRelated" => conditions.FinanceRelated,
            "BankSignatory" => conditions.BankSignatory,
            "Applicable" => true,
            "LoaHolders" => customer?.HasLoa == true || HasLoaHolders(customer),
            "BoardApproval" => customer?.HasLoa != true,
            // Review #7 W5 MS6: Cosec-added steps are inserted at runtime (C3) — skip until then.
            "CosecAdded" => false,
            _ => true,
        };
    }

    private static bool HasLoaHolders(Customer? customer)
    {
        if (customer == null) return false;
        var holders = JsonHelper.Deserialize<List<string>>(customer.LoaHoldersJson);
        return holders.Count > 0;
    }

    public static async Task<WorkflowInstance> InitializeMoaWorkflowAsync(
        AppDbContext context, MOAForm form, Customer? customer)
    {
        var templateCode = await ResolveMoaWorkflowTemplateCodeAsync(context, customer);
        var template = await context.WorkflowTemplates
            .Include(t => t.Steps)
            .FirstOrDefaultAsync(t => t.Code == templateCode && t.IsActive);

        if (template == null)
            throw new InvalidOperationException($"Workflow template '{templateCode}' not found.");

        DivisionGroup? division = null;
        if (customer != null && !string.IsNullOrWhiteSpace(customer.DivisionGroupCode))
        {
            division = await context.DivisionGroups
                .FirstOrDefaultAsync(g => g.Code == customer.DivisionGroupCode);
        }

        var conditions = ParseConditions(form.FinanceRelated, form.BankSignatoryMatter, form.ShareMovement);
        MOIForm? linkedMoi = null;
        if (form.MOIFormId.HasValue)
            linkedMoi = await context.MOIForms.FindAsync(form.MOIFormId.Value);

        // Stamp matrix MOI approver onto MOA formData for sync ResolveAssigneeName.
        if (linkedMoi != null && !string.IsNullOrWhiteSpace(linkedMoi.RequiredApproverName))
        {
            var map = JsonHelper.Deserialize<Dictionary<string, object?>>(form.FormDataJson);
            map["requiredMoiApprover"] = linkedMoi.RequiredApproverName.Trim();
            form.FormDataJson = JsonHelper.Serialize(map);
        }

        var instance = new WorkflowInstance
        {
            WorkflowTemplateId = template.WorkflowTemplateId,
            FormType = "MOA",
            MoaFormId = form.MOAFormId,
            Status = "Active",
            ConditionsJson = JsonHelper.Serialize(conditions),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var applicableSteps = template.Steps
            .OrderBy(s => s.StepOrder)
            .Where(s => StepApplies(s, conditions, customer))
            .ToList();

        var order = 1;
        foreach (var step in applicableSteps)
        {
            var assigneeUserId = step.AssigneeUserId;
            var assigneeName = ResolveAssigneeName(step, customer, form, division);
            if (assigneeUserId.HasValue)
            {
                var linked = await context.Users.FindAsync(assigneeUserId.Value);
                if (linked != null)
                    assigneeName = linked.Name;
            }

            instance.Steps.Add(new WorkflowStepInstance
            {
                StepOrder = order,
                StepKey = step.StepKey,
                DisplayName = step.DisplayName,
                ConditionType = step.ConditionType,
                AssigneeType = step.AssigneeType,
                AssigneeUserId = assigneeUserId,
                AssigneeName = assigneeName,
                Status = order == 1 ? "Active" : "Pending",
                ActivatedAt = order == 1 ? DateTime.UtcNow : null,
            });
            order++;
        }

        instance.CurrentStepOrder = instance.Steps.FirstOrDefault()?.StepOrder ?? 1;
        context.WorkflowInstances.Add(instance);
        await context.SaveChangesAsync();
        return instance;
    }

    public static string ResolveAssigneeName(
        WorkflowStepTemplate step,
        Customer? customer,
        MOAForm? form = null,
        DivisionGroup? division = null)
    {
        if (step.AssigneeType == "ProjectInitiator")
        {
            var fromForm = ReadFormDataString(form?.FormDataJson, "projectInitiator")
                ?? ReadFormDataString(form?.FormDataJson, "requestedBy");
            if (!string.IsNullOrWhiteSpace(fromForm))
                return fromForm.Trim();
            return "MOI requester";
        }

        if (step.AssigneeType == "MoiApprovalHolder" && customer != null)
        {
            var fromOverride = ReadFormDataString(form?.FormDataJson, "requiredMoiApprover");
            if (!string.IsNullOrWhiteSpace(fromOverride))
                return fromOverride.Trim();

            var holders = ClientApprovalService.GetRequiredMoiApprovalHolders(customer);
            return holders.Count > 0
                ? string.Join(", ", holders.Select(h => h.Name))
                : "MOI approver";
        }

        if (step.AssigneeType == "GroupMandatoryApprovers")
        {
            // Priority: Start-MOA Admin override → company Admin list → Group (flowchart MS5).
            var fromForm = JsonHelper.Deserialize<List<string>>(form?.MoaApproversOverrideJson ?? "[]")
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .ToList();
            if (fromForm.Count > 0)
                return string.Join(", ", fromForm);

            var fromCustomer = JsonHelper.Deserialize<List<string>>(customer?.MoaApproversJson ?? "[]")
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .ToList();
            if (fromCustomer.Count > 0)
                return string.Join(", ", fromCustomer);

            var fromGroup = JsonHelper.Deserialize<List<string>>(division?.MandatoryMoaApproversJson ?? "[]")
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .ToList();
            if (fromGroup.Count > 0)
                return string.Join(", ", fromGroup);

            return "MOA approvers (none set)";
        }

        if (!string.IsNullOrWhiteSpace(step.AssigneeDisplayName))
            return step.AssigneeDisplayName;

        if (step.AssigneeType == "LoaHolders" && customer != null)
        {
            var holders = JsonHelper.Deserialize<List<string>>(customer.LoaHoldersJson);
            return holders.Count > 0 ? string.Join(", ", holders) : "LOA Holders";
        }

        if (step.AssigneeType == "BoardMembers")
            return "Board Members";

        return step.AssigneeRole ?? step.DisplayName;
    }

    private static string? ReadFormDataString(string? formDataJson, string key)
    {
        if (string.IsNullOrWhiteSpace(formDataJson)) return null;
        try
        {
            var map = JsonHelper.Deserialize<Dictionary<string, object?>>(formDataJson);
            if (map.TryGetValue(key, out var value) && value != null)
            {
                var s = value.ToString();
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }
        }
        catch
        {
            // ignore malformed form JSON
        }
        return null;
    }

    public static async Task<WorkflowInstanceDto?> GetWorkflowForMoaAsync(AppDbContext context, int moaFormId)
    {
        var instances = await context.WorkflowInstances
            .Include(i => i.Steps)
            .Include(i => i.WorkflowTemplate)
            .Where(i => i.MoaFormId == moaFormId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        // Prefer Active; then Completed. Canceled-only → null so StartWorkflow can re-init (W5 option B).
        var preferred = instances.FirstOrDefault(i => i.Status == "Active")
            ?? instances.FirstOrDefault(i => i.Status == "Completed");
        return preferred == null ? null : ToDto(preferred);
    }

    public static WorkflowInstanceDto ToDto(WorkflowInstance instance) => new()
    {
        Id = instance.WorkflowInstanceId,
        TemplateCode = instance.WorkflowTemplate?.Code ?? string.Empty,
        FormType = instance.FormType,
        Status = instance.Status,
        CurrentStepOrder = instance.CurrentStepOrder,
        Conditions = MapConditions(JsonHelper.Deserialize<WorkflowConditions>(instance.ConditionsJson)),
        Steps = instance.Steps.OrderBy(s => s.StepOrder).Select(s => new WorkflowStepInstanceDto
        {
            Id = s.WorkflowStepInstanceId,
            StepOrder = s.StepOrder,
            StepKey = s.StepKey,
            DisplayName = s.DisplayName,
            AssigneeName = s.AssigneeName,
            AssigneeUserId = s.AssigneeUserId,
            Status = s.Status,
            ApprovedAt = s.ApprovedAt?.ToString("yyyy-MM-dd"),
            Comments = s.Comments,
            AdminOverridden = s.AdminOverridden,
            IsCurrent = s.StepOrder == instance.CurrentStepOrder && s.Status == "Active",
        }).ToList(),
    };

    public static async Task<WorkflowStepInstance?> GetCurrentStepAsync(AppDbContext context, WorkflowInstance instance)
    {
        await context.Entry(instance).Collection(i => i.Steps).LoadAsync();
        return instance.Steps
            .OrderBy(s => s.StepOrder)
            .FirstOrDefault(s => s.Status == "Active");
    }

    public static async Task<bool> CanUserApproveStepAsync(
        AppDbContext context, User user, WorkflowStepInstance step, Customer? customer, bool isAdmin)
    {
        if (isAdmin || user.CanApproveMoa) return true;

        if (step.AssigneeUserId.HasValue && step.AssigneeUserId == user.UserId)
            return true;

        if (step.AssigneeType is "NamedUser" or "InternalSignatory"
            && user.IsInternalSignatory
            && step.AssigneeName.Equals(user.Name, StringComparison.OrdinalIgnoreCase))
            return true;

        if (step.AssigneeType == "ProjectInitiator")
            return AssigneeListContainsName(step.AssigneeName, user.Name);

        if (step.AssigneeType == "MoiApprovalHolder" && customer != null)
        {
            // Prefer matrix-bound MOI on the linked form when assignee was stamped.
            if (AssigneeListContainsName(step.AssigneeName, user.Name)
                || user.Email.Equals(step.AssigneeName, StringComparison.OrdinalIgnoreCase))
                return true;

            if (ClientApprovalService.FindMoiApprovalHolderForUser(customer, user) != null)
                return true;
            return AssigneeListContainsName(step.AssigneeName, user.Name);
        }

        if (step.AssigneeType == "GroupMandatoryApprovers")
            return AssigneeListContainsName(step.AssigneeName, user.Name);

        if (step.AssigneeType == "DivisionRecommender" && customer != null)
        {
            var canRecommend = user.CanRecommendMoi || IsManagerOrAbove(user.JobTitle);
            if (!canRecommend) return false;
            return await context.DivisionGroupRecommenders
                .Include(r => r.DivisionGroup)
                .AnyAsync(r => r.DivisionGroup!.Code == customer.DivisionGroupCode
                    && (r.UserId == user.UserId
                        || r.DisplayName.Equals(user.Name, StringComparison.OrdinalIgnoreCase)));
        }

        if (step.AssigneeType == "JobTitle" && !string.IsNullOrWhiteSpace(step.AssigneeName))
            return user.JobTitle.Contains(step.AssigneeName, StringComparison.OrdinalIgnoreCase)
                || step.AssigneeName.Contains(user.JobTitle, StringComparison.OrdinalIgnoreCase);

        if (step.AssigneeType == "NamedUser")
            return AssigneeListContainsName(step.AssigneeName, user.Name);

        return false;
    }

    private static bool AssigneeListContainsName(string assigneeName, string userName)
    {
        if (string.IsNullOrWhiteSpace(assigneeName) || string.IsNullOrWhiteSpace(userName))
            return false;
        return assigneeName
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(n => n.Equals(userName, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsManagerOrAbove(string jobTitle)
    {
        if (string.IsNullOrWhiteSpace(jobTitle)) return false;
        var t = jobTitle.ToLowerInvariant();
        return t.Contains("manager") || t.Contains("director") || t.Contains("head")
            || t.Contains("cfo") || t.Contains("ceo") || t.Contains("coo") || t.Contains("gm");
    }

    public static async Task AdvanceWorkflowAsync(AppDbContext context, WorkflowInstance instance, WorkflowStepInstance step)
    {
        step.Status = step.AdminOverridden ? "AdminOverridden" : "Approved";
        step.ApprovedAt ??= DateTime.UtcNow;

        var steps = instance.Steps.OrderBy(s => s.StepOrder).ToList();
        var next = steps.FirstOrDefault(s => s.StepOrder > step.StepOrder && s.Status == "Pending");
        if (next != null)
        {
            next.Status = "Active";
            next.ActivatedAt ??= DateTime.UtcNow;
            instance.CurrentStepOrder = next.StepOrder;
        }
        else
        {
            instance.Status = "Completed";
            instance.CurrentStepOrder = step.StepOrder;
        }

        instance.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    private static WorkflowConditionsDto MapConditions(WorkflowConditions c) => new()
    {
        FinanceRelated = c.FinanceRelated,
        BankSignatory = c.BankSignatory,
        ShareMovement = c.ShareMovement,
    };

    public static async Task<bool> CanRecommendMoiAsync(
        AppDbContext context,
        User user,
        Customer customer,
        bool isAdmin,
        JobRequest? job = null)
    {
        if (isAdmin) return true;

        if (job != null)
        {
            await context.Entry(job).Collection(j => j.Units).LoadAsync();
            foreach (var unit in job.Units)
            {
                await context.Entry(unit).Collection(u => u.Assignees).LoadAsync();
                if (JobRequestUnitService.IsUserAssigned(unit, user.UserId))
                    return true;
            }
        }

        if (!user.CanRecommendMoi && !IsManagerOrAbove(user.JobTitle))
            return false;

        if (string.IsNullOrWhiteSpace(customer.DivisionGroupCode))
            return user.CanRecommendMoi || IsManagerOrAbove(user.JobTitle);

        return await context.DivisionGroupRecommenders
            .Include(r => r.DivisionGroup)
            .AnyAsync(r => r.DivisionGroup!.Code == customer.DivisionGroupCode
                && (r.UserId == user.UserId
                    || r.DisplayName.Equals(user.Name, StringComparison.OrdinalIgnoreCase)));
    }
}

public class WorkflowConditions
{
    public bool FinanceRelated { get; set; }
    public bool BankSignatory { get; set; }
    public bool ShareMovement { get; set; }
}
