using System.Text;
using System.Text.Json;
using LGBApp.Backend.Models;

namespace LGBApp.Backend.Services;

public static class FormPackExportService
{
    public static byte[] ExportMoiPack(MOIForm form, Customer? customer)
    {
        var approvals = ClientApprovalService.ParseMoi(form);
        var payload = new
        {
            formType = "MOI",
            formId = form.MOIFormId,
            form.Company,
            form.WorkflowState,
            form.UpdatedAt,
            customer = customer?.Company,
            clientApprovals = approvals,
            body = form.FormDataJson,
        };

        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static byte[] ExportMoaPack(MOAForm form, Customer? customer)
    {
        var approvals = ClientApprovalService.ParseMoa(form);
        var payload = new
        {
            formType = "MOA",
            formId = form.MOAFormId,
            form.Company,
            form.UpdatedAt,
            form.SharonApprovedAt,
            customer = customer?.Company,
            clientApprovals = approvals,
            packChecklist = form.PackChecklistJson,
            body = form.FormDataJson,
        };

        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
    }
}
