namespace LGBApp.Backend.Models.DTOs;

public class ClientApprovalDto
{
    public int UserId { get; set; }
    public string AccountHolderName { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public string SignedAt { get; set; } = string.Empty;
    public string? SignatureFileName { get; set; }
    public string? SignatureDataUrl { get; set; }
}
