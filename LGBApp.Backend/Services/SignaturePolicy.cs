using Microsoft.AspNetCore.Mvc;

namespace LGBApp.Backend.Services;

public static class SignaturePolicy
{
    public static ActionResult? ValidateRequired(string? signatureDataUrl, string? signatureFileName)
    {
        if (ClientApprovalService.IsValidSignature(signatureDataUrl, signatureFileName))
            return null;

        return new BadRequestObjectResult(new
        {
            message = "A signature is required — draw one or attach an image/PDF.",
        });
    }
}
