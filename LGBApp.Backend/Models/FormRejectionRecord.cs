namespace LGBApp.Backend.Models;

public class FormRejectionRecord
{
    public string Stage { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime RejectedAt { get; set; }
}
