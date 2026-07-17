using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Tests;

/// <summary>Review #7 W5 — flowchart MS1–MS7 MOA chain + cancel in-flight.</summary>
public class MoaFlowchartChainTests
{
    private static readonly string[] ExpectedKeys =
    [
        "HeadOfGroupSecretarial",
        "MoiRequester",
        "MoiApprover",
        "TehSW",
        "GroupMandatory",
        "CosecAdded",
        "FinalApprover",
    ];

    [Fact]
    public void FreshSeed_UsesFlowchartSteps()
    {
        using var db = new TestDbFactory();
        WorkflowConfigSeeder.SeedReferenceData(db.Context);

        foreach (var code in new[] { "MOA_NO_LOA", "MOA_WITH_LOA", "MOA_SWM" })
        {
            var template = db.Context.WorkflowTemplates
                .Include(t => t.Steps)
                .Single(t => t.Code == code);
            var keys = template.Steps.OrderBy(s => s.StepOrder).Select(s => s.StepKey).ToList();
            Assert.Equal(ExpectedKeys, keys);
            Assert.DoesNotContain(template.Steps, s => s.StepKey == "SeniorManagerCoSec");
            Assert.DoesNotContain(template.Steps, s => s.StepKey == "Dlcm");
        }
    }

    [Fact]
    public void Ensure_CancelsActiveAndIsIdempotent()
    {
        using var db = new TestDbFactory();
        WorkflowConfigSeeder.SeedReferenceData(db.Context);

        var template = db.Context.WorkflowTemplates.First(t => t.Code == "MOA_NO_LOA");
        // Simulate a legacy Active instance, then force re-upgrade by renaming the head step.
        var moa = new MOAForm { Company = "Test", FormDataJson = "{}" };
        db.Context.MOAForms.Add(moa);
        db.Context.SaveChanges();

        db.Context.WorkflowInstances.Add(new WorkflowInstance
        {
            WorkflowTemplateId = template.WorkflowTemplateId,
            FormType = "MOA",
            MoaFormId = moa.MOAFormId,
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.Context.SaveChanges();

        // Force upgrade path again: strip flowchart marker
        var head = db.Context.WorkflowStepTemplates
            .First(s => s.WorkflowTemplateId == template.WorkflowTemplateId
                && s.StepKey == WorkflowConfigSeeder.FlowchartHeadStepKey);
        head.StepKey = "LegacyHead";
        db.Context.SaveChanges();

        WorkflowConfigSeeder.EnsureMoaFlowchartChain(db.Context);

        var instance = db.Context.WorkflowInstances.Single(i => i.MoaFormId == moa.MOAFormId);
        Assert.Equal("Canceled", instance.Status);

        var keys = db.Context.WorkflowStepTemplates
            .Where(s => s.WorkflowTemplateId == template.WorkflowTemplateId)
            .OrderBy(s => s.StepOrder)
            .Select(s => s.StepKey)
            .ToList();
        Assert.Equal(ExpectedKeys, keys);

        // Idempotent second call
        WorkflowConfigSeeder.EnsureMoaFlowchartChain(db.Context);
        Assert.Equal(ExpectedKeys.Length,
            db.Context.WorkflowStepTemplates.Count(s => s.WorkflowTemplateId == template.WorkflowTemplateId));
    }

    [Fact]
    public void CosecAdded_DoesNotApply_BankSignatory_Does()
    {
        var cosec = new WorkflowStepTemplate { ConditionType = "CosecAdded" };
        var bank = new WorkflowStepTemplate { ConditionType = "BankSignatory" };
        var conditions = new WorkflowConditions { BankSignatory = true };

        Assert.False(WorkflowService.StepApplies(cosec, conditions, null));
        Assert.True(WorkflowService.StepApplies(bank, conditions, null));
        Assert.False(WorkflowService.StepApplies(bank, new WorkflowConditions(), null));
    }

    [Fact]
    public void ResolveAssignee_UsesFormAndGroupLists()
    {
        var form = new MOAForm
        {
            FormDataJson = JsonHelper.Serialize(new Dictionary<string, string>
            {
                ["projectInitiator"] = "Alice Requester",
            }),
        };
        var customer = new Customer
        {
            AccountHolders =
            [
                new AccountHolder { Name = "Bob Approver", NeedsMoiApproval = true },
            ],
        };
        var division = new DivisionGroup
        {
            MandatoryMoaApproversJson = JsonHelper.Serialize(new[] { "Kevin Kuok" }),
        };

        var requester = new WorkflowStepTemplate { AssigneeType = "ProjectInitiator" };
        Assert.Equal("Alice Requester", WorkflowService.ResolveAssigneeName(requester, customer, form, division));

        var moiApprover = new WorkflowStepTemplate { AssigneeType = "MoiApprovalHolder" };
        Assert.Equal("Bob Approver", WorkflowService.ResolveAssigneeName(moiApprover, customer, form, division));

        var group = new WorkflowStepTemplate { AssigneeType = "GroupMandatoryApprovers" };
        Assert.Equal("Kevin Kuok", WorkflowService.ResolveAssigneeName(group, customer, form, division));

        var teh = new WorkflowStepTemplate
        {
            AssigneeType = "NamedUser",
            AssigneeDisplayName = "Teh SW",
        };
        Assert.Equal("Teh SW", WorkflowService.ResolveAssigneeName(teh, customer, form, division));
    }

    [Fact]
    public async Task GetWorkflow_IgnoresCanceled_AllowsRestart()
    {
        using var db = new TestDbFactory();
        WorkflowConfigSeeder.SeedReferenceData(db.Context);
        var template = db.Context.WorkflowTemplates.First(t => t.Code == "MOA_NO_LOA");
        var moa = new MOAForm { Company = "Test", FormDataJson = "{}" };
        db.Context.MOAForms.Add(moa);
        db.Context.SaveChanges();

        db.Context.WorkflowInstances.Add(new WorkflowInstance
        {
            WorkflowTemplateId = template.WorkflowTemplateId,
            FormType = "MOA",
            MoaFormId = moa.MOAFormId,
            Status = "Canceled",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.Context.SaveChanges();

        Assert.Null(await WorkflowService.GetWorkflowForMoaAsync(db.Context, moa.MOAFormId));
    }
}
