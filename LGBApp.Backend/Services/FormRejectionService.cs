using LGBApp.Backend.Models;

namespace LGBApp.Backend.Services;

public static class FormRejectionStages
{
    public const string MoiClientApproval = "moi_client_approval";
    public const string MoiIntake = "moi_intake";
    public const string MoaSharonReview = "moa_sharon_review";
    public const string MoaClientApproval = "moa_client_approval";
}

public static class FormRejectionService
{
    public static List<FormRejectionRecord> ParseMoi(MOIForm form) =>
        JsonHelper.Deserialize<List<FormRejectionRecord>>(form.RejectionsJson);

    public static List<FormRejectionRecord> ParseMoa(MOAForm form) =>
        JsonHelper.Deserialize<List<FormRejectionRecord>>(form.RejectionsJson);

    public static void AddMoiRejection(MOIForm form, FormRejectionRecord record)
    {
        var list = ParseMoi(form);
        list.Add(record);
        form.RejectionsJson = JsonHelper.Serialize(list);
    }

    public static void AddMoaRejection(MOAForm form, FormRejectionRecord record)
    {
        var list = ParseMoa(form);
        list.Add(record);
        form.RejectionsJson = JsonHelper.Serialize(list);
    }
}
