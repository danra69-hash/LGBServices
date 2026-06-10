using LGBApp.Backend.Models;

namespace LGBApp.Backend.Services;

public class MoaPackChecklist
{
    public bool InternalChecklistA { get; set; }
    public bool InternalChecklistB { get; set; }
    public bool CleanAgreementAttached { get; set; }
    public bool ShareholdingTableAttached { get; set; }
    public string SsmRegistrationNo { get; set; } = string.Empty;
    public string SsmNewRegistrationNo { get; set; } = string.Empty;
    public string SsmEntityType { get; set; } = string.Empty;
    public string SsmStatus { get; set; } = string.Empty;
    public string SsmAsAtDate { get; set; } = string.Empty;
}

public static class MoaPackChecklistService
{
    public static MoaPackChecklist Parse(MOAForm form) =>
        JsonHelper.Deserialize<MoaPackChecklist>(form.PackChecklistJson);

    public static void Apply(MOAForm form, MoaPackChecklist checklist) =>
        form.PackChecklistJson = JsonHelper.Serialize(checklist);

    public static (bool IsValid, List<string> Errors) Validate(MOAForm form)
    {
        var pack = Parse(form);
        var errors = new List<string>();

        if (!pack.InternalChecklistA)
            errors.Add("Internal checklist A must be completed.");
        if (!pack.InternalChecklistB)
            errors.Add("Internal checklist B must be completed.");
        if (!pack.CleanAgreementAttached)
            errors.Add("Clean agreement / appointment letter attachment is required.");
        if (string.IsNullOrWhiteSpace(pack.SsmRegistrationNo))
            errors.Add("SSM registration number is required.");
        if (string.IsNullOrWhiteSpace(pack.SsmEntityType))
            errors.Add("SSM entity type is required.");
        if (string.IsNullOrWhiteSpace(pack.SsmStatus))
            errors.Add("SSM status is required.");
        if (string.IsNullOrWhiteSpace(pack.SsmAsAtDate))
            errors.Add("SSM as-at date is required.");
        if (form.ShareMovement && !pack.ShareholdingTableAttached)
            errors.Add("Shareholding table is required when share movement is flagged.");

        return (errors.Count == 0, errors);
    }
}
