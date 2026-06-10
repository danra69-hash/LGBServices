using LGBApp.Backend.Models;
using LGBApp.Backend.Services;

namespace LGBApp.Backend.Data;

public static class WorkflowConfigSeeder
{
    public static void Seed(AppDbContext context)
    {
        SeedFormTemplates(context);
        SeedWorkflowTemplates(context);
        SeedDivisionGroups(context);
        SeedClientUsers(context);
        context.SaveChanges();
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

        if (!context.Users.Any(u => u.Email == "client@acme.test"))
        {
            context.Users.Add(new User
            {
                Email = "client@acme.test",
                PasswordHash = PasswordHasher.Hash(CustomerClientAdminProvisioner.DefaultPassword),
                Name = "Acme Client Staff",
                Mobile = "",
                Role = UserRoles.Client,
                CustomerId = firstCustomer.CustomerId,
                IsVerified = true,
                MustChangePassword = true,
                CreatedAt = DateTime.UtcNow,
            });
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
            new { key = "projectInitiator", label = "Project Initiator", type = "text", required = true, section = "main" },
            new { key = "preparedByInternal", label = "Prepared by (Internal)", type = "user", required = true, section = "prep" },
            new { key = "vettedByInternal", label = "Vetted by (Internal)", type = "user", required = true, section = "prep" },
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

        // MOI recommendation workflow
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

        // A — Company without LOA
        var noLoa = new WorkflowTemplate
        {
            Code = "MOA_NO_LOA",
            Name = "MOA — No LOA",
            WorkflowType = "MOA",
            Description = "For company without Limit of Authority",
            IsActive = true,
            Steps =
            [
                Step(1, "SeniorManagerCoSec", "Senior Manager, Company Secretarial", "Always", "JobTitle", "Senior Manager, Company Secretarial"),
                Step(2, "ProjectInitiator", "Project Initiator", "Always", "ProjectInitiator"),
                Step(3, "HeadOfFinanceCfo", "Head of Finance or CFO", "FinanceRelated", "JobTitle", "CFO"),
                Step(4, "CeoCooGm", "CEO / COO / GM", "Applicable", "JobTitle", "CEO"),
                Step(5, "MsTeh", "Ms Teh (bank signatories)", "BankSignatory", "NamedUser", displayName: "Ms Teh"),
                Step(6, "BoardMembers", "Full Board (excl. external members)", "BoardApproval", "BoardMembers"),
                Step(7, "Dlcm", "DLCM", "Always", "ExternalName", displayName: "DLCM"),
            ],
        };

        // B — Company with LOA
        var withLoa = new WorkflowTemplate
        {
            Code = "MOA_WITH_LOA",
            Name = "MOA — With LOA",
            WorkflowType = "MOA",
            Description = "For company with Limit of Authority",
            IsActive = true,
            Steps =
            [
                Step(1, "SeniorManagerCoSec", "Senior Manager, Company Secretarial", "Always", "JobTitle", "Senior Manager, Company Secretarial"),
                Step(2, "ProjectInitiator", "Project Initiator", "Always", "ProjectInitiator"),
                Step(3, "HeadOfFinanceCfo", "Head of Finance or CFO", "FinanceRelated", "JobTitle", "CFO"),
                Step(4, "MsTeh", "Ms Teh (bank signatories)", "BankSignatory", "NamedUser", displayName: "Ms Teh"),
                Step(5, "DlcmBank", "DLCM — all bank matters", "Always", "ExternalName", displayName: "DLCM"),
            ],
        };

        // C — SWM Group
        var swm = new WorkflowTemplate
        {
            Code = "MOA_SWM",
            Name = "MOA — SWM Group",
            WorkflowType = "MOA",
            Description = "For SWM Group companies",
            IsActive = true,
            Steps =
            [
                Step(1, "SeniorManagerCoSec", "Senior Manager, Company Secretarial", "Always", "JobTitle", "Senior Manager, Company Secretarial"),
                Step(2, "ProjectInitiator", "Project Initiator", "Always", "ProjectInitiator"),
                Step(3, "HeadOfFinanceCfo", "Head of Finance or CFO", "FinanceRelated", "JobTitle", "CFO"),
                Step(4, "LoaHolders", "Person(s) with applicable LOA / BOD / EXCO", "LoaHolders", "LoaHolders"),
                Step(5, "RegulatorCompliance", "Regulator & Compliance Department", "Always", "NamedUser", displayName: "Shirley Nicholas"),
                Step(6, "CeoCooJanice", "CEO / COO / Janice Lim", "Always", "NamedUser", displayName: "Janice Lim"),
                Step(7, "MsTeh", "Ms Teh (bank signatories)", "BankSignatory", "NamedUser", displayName: "Ms Teh"),
            ],
        };

        context.WorkflowTemplates.AddRange(moiTemplate, noLoa, withLoa, swm);
    }

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

        var groups = new (string Code, string Name, string MoaTemplate, string[] Recommenders)[]
        {
            ("LGB", "LGB", "MOA_NO_LOA", ["Tai", "Sam"]),
            ("LGB_INS", "LGB INS", "MOA_NO_LOA", ["Steven", "Tiew Siong Yi"]),
            ("EXITRA", "Exitra", "MOA_NO_LOA", ["Kevin Teoh", "Tay", "Jonas"]),
            ("BELLWORTH", "Bellworth & London", "MOA_NO_LOA", ["Kevin Kuok", "Jaslyn", "Wong Wai Leng", "Evelyn"]),
            ("UER", "UER", "MOA_NO_LOA", ["Sam Lau"]),
            ("PARKWOOD", "Parkwood", "MOA_NO_LOA", ["Danny Ng", "Casper Wong"]),
            ("SWM", "SWM", "MOA_SWM", ["Shirley", "Bin", "Yvonne", "Thomas"]),
            ("DLAL", "DLAL", "MOA_NO_LOA", []),
            ("DLCM", "DLCM", "MOA_WITH_LOA", []),
            ("DLCM_KK", "DLCM & KK", "MOA_WITH_LOA", []),
            ("DLCM_SEAN", "DLCM & SEAN", "MOA_WITH_LOA", []),
            ("HOLD_ON", "HOLD ON", "MOA_NO_LOA", []),
            ("NOMINEES", "Nominees", "MOA_NO_LOA", []),
            ("TCB", "TCB", "MOA_NO_LOA", []),
            ("KK", "KK", "MOA_WITH_LOA", []),
            ("SEAN", "SEAN", "MOA_WITH_LOA", []),
            ("ISHAK", "ISHAK", "MOA_NO_LOA", []),
            ("JANICE", "JANICE", "MOA_NO_LOA", []),
        };

        foreach (var (code, name, moaTemplate, recommenders) in groups)
        {
            var group = new DivisionGroup
            {
                Code = code,
                Name = name,
                MoaWorkflowTemplateCode = moaTemplate,
                DefaultMoiFormTemplateCode = "MOI_DEFAULT",
                DefaultMoaFormTemplateCode = moaTemplate == "MOA_SWM" ? "MOA_SWM" : "MOA_DEFAULT",
                IsActive = true,
                Recommenders = recommenders.Select(r => new DivisionGroupRecommender { DisplayName = r }).ToList(),
            };
            context.DivisionGroups.Add(group);
        }
    }
}
