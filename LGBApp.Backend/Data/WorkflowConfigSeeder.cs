using LGBApp.Backend.Models;
using LGBApp.Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Data;

public static class WorkflowConfigSeeder
{
    /// <summary>Flowchart MS1 step key — presence means the chain upgrade already ran.</summary>
    public const string FlowchartHeadStepKey = "HeadOfGroupSecretarial";

    public static void Seed(AppDbContext context)
    {
        SeedReferenceData(context);
        SeedClientUsers(context);
        context.SaveChanges();
    }

    /// <summary>
    /// Idempotent MOI/MOA templates, workflow definitions, and division groups (safe for production).
    /// </summary>
    public static void SeedReferenceData(AppDbContext context)
    {
        SeedFormTemplates(context);
        SeedWorkflowTemplates(context);
        SeedDivisionGroups(context);
        EnsureMoaFlowchartChain(context);
        EnsureGroupMandatoryApprovers(context);
    }

    private static void SeedClientUsers(AppDbContext context)
    {
        CustomerClientAdminProvisioner.EnsureAllCustomersHaveClientAdminAsync(context).GetAwaiter().GetResult();

        var firstCustomer = context.Customers.FirstOrDefault();
        if (firstCustomer == null) return;

        if (!context.Users.Any(u => u.Email == "clientadmin@acme.test"))
        {
            var acmeAdmin = context.Users.FirstOrDefault(u =>
                u.CustomerId == firstCustomer.CustomerId && u.Role == UserRoles.ClientAdmin);
            if (acmeAdmin != null)
            {
                acmeAdmin.Email = "clientadmin@acme.test";
                acmeAdmin.Name = "Acme Client Admin";
            }
        }
    }

    private static void SeedFormTemplates(AppDbContext context)
    {
        if (context.FormTemplates.Any())
            return;

        var now = DateTime.UtcNow;
        var defaultMoiFields = JsonHelper.Serialize(new[]
        {
            new { key = "company", label = "Company", type = "select", required = true, section = "main" },
            new { key = "typeOfDocument", label = "Type of Document", type = "select", required = true, section = "main" },
            new { key = "documentTitle", label = "Document Title", type = "text", required = true, section = "main" },
            new { key = "backgroundInfo", label = "Background Info", type = "textarea", required = true, section = "main" },
            new { key = "withLOA", label = "With Limits of Authority (LOA)", type = "boolean", required = false, section = "loa" },
            new { key = "financeRelated", label = "Involves financing/payments/compliance affecting AFS", type = "boolean", required = false, section = "conditions" },
            new { key = "bankSignatoryMatter", label = "Involves opening/closing/changing bank signatories", type = "boolean", required = false, section = "conditions" },
            new { key = "requestedBy", label = "Requested By", type = "select", required = true, section = "request" },
        });

        var defaultMoaFields = JsonHelper.Serialize(new[]
        {
            new { key = "typeOfDocument", label = "Type of Document", type = "text", required = true, section = "main" },
            new { key = "projectInitiator", label = "Project Initiator", type = "text", required = true, section = "main" },
            new { key = "preparedByInternal", label = "Prepared by (Internal)", type = "user", required = true, section = "prep" },
            new { key = "vettedByInternal", label = "Vetted by (Internal)", type = "user", required = true, section = "prep" },
            new { key = "preparedByExternal", label = "Prepared by (External)", type = "text", required = false, section = "prep" },
            new { key = "vettedByExternal", label = "Vetted by (External)", type = "text", required = false, section = "prep" },
            new { key = "seniorManagerApproval", label = "Senior Manager approval", type = "approval", required = false, section = "approvals" },
            new { key = "managerRegulatoryApproval", label = "Manager Regulatory approval", type = "approval", required = false, section = "approvals" },
            new { key = "shareMovement", label = "Movement of shares", type = "boolean", required = false, section = "pack" },
        });

        context.FormTemplates.AddRange(
            new FormTemplate
            {
                FormType = "MOI",
                Code = "MOI_DEFAULT",
                Name = "Default MOI",
                Description = "Memorandum of Instruction",
                AddressedTo = "Head of Legal & Secretarial Department",
                DivisionLabel = "Secretarial Division",
                IssuerEntity = "LGB Group",
                FieldsJson = defaultMoiFields,
                IsDefault = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new FormTemplate
            {
                FormType = "MOA",
                Code = "MOA_DEFAULT",
                Name = "Default MOA",
                Description = "Memorandum of Approval",
                AddressedTo = "Head of Legal & Secretarial Department",
                DivisionLabel = "Secretarial Division",
                IssuerEntity = "LGB Group",
                FieldsJson = defaultMoaFields,
                IsDefault = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new FormTemplate
            {
                FormType = "MOA",
                Code = "MOA_SWM",
                Name = "SWM MOA",
                Description = "Memorandum of Approval",
                AddressedTo = "Head of Legal & Secretarial Department",
                DivisionLabel = "SWM Group",
                IssuerEntity = "SWM Group",
                FieldsJson = defaultMoaFields,
                IsDefault = false,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
    }

    private static void SeedWorkflowTemplates(AppDbContext context)
    {
        if (context.WorkflowTemplates.Any())
            return;

        var moiTemplate = new WorkflowTemplate
        {
            Code = "MOI_RECOMMEND",
            Name = "MOI Recommendation",
            WorkflowType = "MOI",
            Description = "Draft → Recommend (manager+) → MOI Approval",
            IsActive = true,
            Steps =
            [
                new WorkflowStepTemplate { StepOrder = 1, StepKey = "Draft", DisplayName = "Draft", ConditionType = "Always", AssigneeType = "ProjectInitiator" },
                new WorkflowStepTemplate { StepOrder = 2, StepKey = "Recommend", DisplayName = "Recommend (Manager & Above)", ConditionType = "Always", AssigneeType = "DivisionRecommender" },
                new WorkflowStepTemplate { StepOrder = 3, StepKey = "MoiApproval", DisplayName = "MOI Approval", ConditionType = "Always", AssigneeType = "MoiApprovalHolder" },
            ],
        };

        context.WorkflowTemplates.AddRange(
            moiTemplate,
            BuildMoaTemplate("MOA_NO_LOA", "MOA — No LOA", "For company without Limit of Authority"),
            BuildMoaTemplate("MOA_WITH_LOA", "MOA — With LOA", "For company with Limit of Authority"),
            BuildMoaTemplate("MOA_SWM", "MOA — SWM Group", "For SWM Group companies"));
    }

    /// <summary>
    /// Review #7 W5: replace legacy MOA chains with flowchart MS1–MS7; cancel in-flight (option B).
    /// Idempotent — skips when HeadOfGroupSecretarial already present on MOA_NO_LOA.
    /// </summary>
    public static void EnsureMoaFlowchartChain(AppDbContext context)
    {
        var noLoa = context.WorkflowTemplates
            .Include(t => t.Steps)
            .FirstOrDefault(t => t.Code == "MOA_NO_LOA");
        if (noLoa == null)
            return;

        if (noLoa.Steps.Any(s => s.StepKey == FlowchartHeadStepKey))
            return;

        Console.WriteLine("[Startup] W5: reseeding MOA chains to flowchart MS1–MS7 (cancel in-flight Active instances)…");

        var active = context.WorkflowInstances
            .Where(i => i.FormType == "MOA" && i.Status == "Active")
            .ToList();
        var now = DateTime.UtcNow;
        foreach (var instance in active)
        {
            instance.Status = "Canceled";
            instance.UpdatedAt = now;
        }

        foreach (var code in new[] { "MOA_NO_LOA", "MOA_WITH_LOA", "MOA_SWM" })
        {
            var template = context.WorkflowTemplates
                .Include(t => t.Steps)
                .FirstOrDefault(t => t.Code == code);
            if (template == null)
                continue;

            context.WorkflowStepTemplates.RemoveRange(template.Steps);
            template.Steps.Clear();
            foreach (var step in BuildFlowchartSteps())
                template.Steps.Add(step);

            template.Description = code switch
            {
                "MOA_WITH_LOA" => "Flowchart MS1–MS7 — company with LOA",
                "MOA_SWM" => "Flowchart MS1–MS7 — SWM Group",
                _ => "Flowchart MS1–MS7 — company without LOA",
            };
        }

        context.SaveChanges();
        Console.WriteLine($"[Startup] W5: canceled {active.Count} Active MOA workflow(s); templates updated.");
    }

    /// <summary>CubeV Approval Matrix — display names only (no invented emails).</summary>
    public static void EnsureGroupMandatoryApprovers(AppDbContext context)
    {
        var presets = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["BELLWORTH"] = ["Kevin Kuok"],
            ["SWM"] = ["Janice Lim", "Ho De Leong", "Shirley Nicholas"],
            ["LGB"] = [],
        };

        foreach (var group in context.DivisionGroups.ToList())
        {
            if (!presets.TryGetValue(group.Code, out var names))
                continue;
            var json = JsonHelper.Serialize(names);
            if (string.Equals(group.MandatoryMoaApproversJson, json, StringComparison.Ordinal))
                continue;
            // Only fill when still default empty — don't overwrite manual edits after first seed
            if (string.IsNullOrWhiteSpace(group.MandatoryMoaApproversJson)
                || group.MandatoryMoaApproversJson == "[]"
                || group.MandatoryMoaApproversJson == "null")
            {
                group.MandatoryMoaApproversJson = json;
            }
        }

        context.SaveChanges();
    }

    private static WorkflowTemplate BuildMoaTemplate(string code, string name, string description) => new()
    {
        Code = code,
        Name = name,
        WorkflowType = "MOA",
        Description = description,
        IsActive = true,
        Steps = BuildFlowchartSteps(),
    };

    private static List<WorkflowStepTemplate> BuildFlowchartSteps() =>
    [
        Step(1, FlowchartHeadStepKey, "Head of group secretarial (Sharon)", "Always", "JobTitle", "Senior Manager, Company Secretarial"),
        Step(2, "MoiRequester", "MOI requester", "Always", "ProjectInitiator"),
        Step(3, "MoiApprover", "MOI approver", "Always", "MoiApprovalHolder"),
        Step(4, "TehSW", "Teh SW (banking)", "BankSignatory", "NamedUser", displayName: "Teh SW"),
        Step(5, "GroupMandatory", "Mandatory MOA approvers (group)", "Always", "GroupMandatoryApprovers"),
        Step(6, "CosecAdded", "Additional approvers (Cosec)", "CosecAdded", "NamedUser", displayName: "Cosec-added"),
        Step(7, "FinalApprover", "Dato' Lim (final)", "Always", "NamedUser", displayName: "Dato' Lim"),
    ];

    private static WorkflowStepTemplate Step(
        int order, string key, string display, string condition, string assigneeType,
        string? role = null, string? displayName = null) => new()
    {
        StepOrder = order,
        StepKey = key,
        DisplayName = display,
        ConditionType = condition,
        AssigneeType = assigneeType,
        AssigneeRole = role,
        AssigneeDisplayName = displayName,
        AllowAdminOverride = true,
    };

    private static void SeedDivisionGroups(AppDbContext context)
    {
        if (context.DivisionGroups.Any())
            return;

        var groups = new (string Code, string Name, string MoaTemplate, string[] Recommenders, string[] Mandatory)[]
        {
            ("LGB", "LGB", "MOA_NO_LOA", ["Tai", "Sam"], []),
            ("LGB_INS", "LGB INS", "MOA_NO_LOA", ["Steven", "Tiew Siong Yi"], []),
            ("EXITRA", "Exitra", "MOA_NO_LOA", ["Kevin Teoh", "Tay", "Jonas"], []),
            ("BELLWORTH", "Bellworth & London", "MOA_NO_LOA", ["Kevin Kuok", "Jaslyn", "Wong Wai Leng", "Evelyn"], ["Kevin Kuok"]),
            ("UER", "UER", "MOA_NO_LOA", ["Sam Lau"], []),
            ("PARKWOOD", "Parkwood", "MOA_NO_LOA", ["Danny Ng", "Casper Wong"], []),
            ("SWM", "SWM", "MOA_SWM", ["Shirley", "Bin", "Yvonne", "Thomas"], ["Janice Lim", "Ho De Leong", "Shirley Nicholas"]),
            ("DLAL", "DLAL", "MOA_NO_LOA", [], []),
            ("DLCM", "DLCM", "MOA_WITH_LOA", [], []),
            ("DLCM_KK", "DLCM & KK", "MOA_WITH_LOA", [], []),
            ("DLCM_SEAN", "DLCM & SEAN", "MOA_WITH_LOA", [], []),
            ("HOLD_ON", "HOLD ON", "MOA_NO_LOA", [], []),
            ("NOMINEES", "Nominees", "MOA_NO_LOA", [], []),
            ("TCB", "TCB", "MOA_NO_LOA", [], []),
            ("KK", "KK", "MOA_WITH_LOA", [], []),
            ("SEAN", "SEAN", "MOA_WITH_LOA", [], []),
            ("ISHAK", "ISHAK", "MOA_NO_LOA", [], []),
            ("JANICE", "JANICE", "MOA_NO_LOA", [], []),
        };

        foreach (var (code, name, moaTemplate, recommenders, mandatory) in groups)
        {
            var group = new DivisionGroup
            {
                Code = code,
                Name = name,
                MoaWorkflowTemplateCode = moaTemplate,
                DefaultMoiFormTemplateCode = "MOI_DEFAULT",
                DefaultMoaFormTemplateCode = moaTemplate == "MOA_SWM" ? "MOA_SWM" : "MOA_DEFAULT",
                MandatoryMoaApproversJson = JsonHelper.Serialize(mandatory),
                IsActive = true,
                Recommenders = recommenders.Select(r => new DivisionGroupRecommender { DisplayName = r }).ToList(),
            };
            context.DivisionGroups.Add(group);
        }
    }
}
