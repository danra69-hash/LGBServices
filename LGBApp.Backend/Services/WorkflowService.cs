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
            .FirstOrDefaultAsync(c => c.Company == company);
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

        var conditions = ParseConditions(form.FinanceRelated, form.BankSignatoryMatter, form.ShareMovement);
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
            instance.Steps.Add(new WorkflowStepInstance
            {
                StepOrder = order,
                StepKey = step.StepKey,
                DisplayName = step.DisplayName,
                ConditionType = step.ConditionType,
                AssigneeType = step.AssigneeType,
                AssigneeUserId = step.AssigneeUserId,
                AssigneeName = ResolveAssigneeName(step, customer),
                Status = order == 1 ? "Active" : "Pending",
            });
            order++;
        }

        instance.CurrentStepOrder = instance.Steps.FirstOrDefault()?.StepOrder ?? 1;
        context.WorkflowInstances.Add(instance);
        await context.SaveChangesAsync();
        return instance;
    }

    public static string ResolveAssigneeName(WorkflowStepTemplate step, Customer? customer)
    {
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

    public static async Task<WorkflowInstanceDto?> GetWorkflowForMoaAsync(AppDbContext context, int moaFormId)
    {
        var instance = await context.WorkflowInstances
            .Include(i => i.Steps)
            .Include(i => i.WorkflowTemplate)
            .FirstOrDefaultAsync(i => i.MoaFormId == moaFormId);

        return instance == null ? null : ToDto(instance);
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
        if (isAdmin) return true;

        if (step.AssigneeUserId.HasValue && step.AssigneeUserId == user.UserId)
            return true;

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
            return step.AssigneeName.Equals(user.Name, StringComparison.OrdinalIgnoreCase);

        return false;
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

    public static async Task<bool> CanRecommendMoiAsync(AppDbContext context, User user, Customer customer, bool isAdmin)
    {
        if (isAdmin) return true;
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
