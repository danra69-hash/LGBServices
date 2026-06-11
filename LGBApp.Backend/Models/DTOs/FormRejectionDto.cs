namespace LGBApp.Backend.Models.DTOs;

public class FormRejectionDto
{
    public string Stage { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string RejectedAt { get; set; } = string.Empty;
}
