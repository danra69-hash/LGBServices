namespace LGBApp.Backend.Models;

public class CompletedService
{
    public int Id { get; set; }
    public int? JobRequestId { get; set; }
    public string Customer { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public int UsedQty { get; set; }
    public int TotalQty { get; set; }
    public DateTime DateRequested { get; set; }
    public DateTime DateCompleted { get; set; }
    public string AccountHolder { get; set; } = string.Empty;
    public string JobAssignedTo { get; set; } = string.Empty;
    public string Status { get; set; } = "Completed";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
