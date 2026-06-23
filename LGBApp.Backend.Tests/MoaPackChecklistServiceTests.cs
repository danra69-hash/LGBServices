using LGBApp.Backend.Models;
using LGBApp.Backend.Services;

namespace LGBApp.Backend.Tests;

public class MoaPackChecklistServiceTests
{
    [Fact]
    public void Validate_CompleteChecklist_Passes()
    {
        var form = new MOAForm
        {
            PackChecklistJson = JsonHelper.Serialize(new MoaPackChecklist
            {
                InternalChecklistA = true,
                InternalChecklistB = true,
                CleanAgreementAttached = true,
                SsmRegistrationNo = "123456-A",
                SsmEntityType = "Sdn Bhd",
                SsmStatus = "Active",
                SsmAsAtDate = "2026-06-01",
            }),
        };

        var (valid, errors) = MoaPackChecklistService.Validate(form);

        Assert.True(valid);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(false, true, true, "123", "Sdn Bhd", "Active", "2026-01-01")]
    [InlineData(true, false, true, "123", "Sdn Bhd", "Active", "2026-01-01")]
    [InlineData(true, true, false, "123", "Sdn Bhd", "Active", "2026-01-01")]
    [InlineData(true, true, true, "", "Sdn Bhd", "Active", "2026-01-01")]
    [InlineData(true, true, true, "123", "", "Active", "2026-01-01")]
    [InlineData(true, true, true, "123", "Sdn Bhd", "", "2026-01-01")]
    [InlineData(true, true, true, "123", "Sdn Bhd", "Active", "")]
    public void Validate_MissingFields_Fails(
        bool checklistA,
        bool checklistB,
        bool cleanAgreement,
        string regNo,
        string entityType,
        string status,
        string asAtDate)
    {
        var form = new MOAForm
        {
            PackChecklistJson = JsonHelper.Serialize(new MoaPackChecklist
            {
                InternalChecklistA = checklistA,
                InternalChecklistB = checklistB,
                CleanAgreementAttached = cleanAgreement,
                SsmRegistrationNo = regNo,
                SsmEntityType = entityType,
                SsmStatus = status,
                SsmAsAtDate = asAtDate,
            }),
        };

        var (valid, errors) = MoaPackChecklistService.Validate(form);

        Assert.False(valid);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Validate_ShareMovement_RequiresShareholdingTable()
    {
        var form = new MOAForm
        {
            ShareMovement = true,
            PackChecklistJson = JsonHelper.Serialize(new MoaPackChecklist
            {
                InternalChecklistA = true,
                InternalChecklistB = true,
                CleanAgreementAttached = true,
                ShareholdingTableAttached = false,
                SsmRegistrationNo = "123",
                SsmEntityType = "Sdn Bhd",
                SsmStatus = "Active",
                SsmAsAtDate = "2026-01-01",
            }),
        };

        var (valid, errors) = MoaPackChecklistService.Validate(form);

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("Shareholding", StringComparison.OrdinalIgnoreCase));
    }
}
