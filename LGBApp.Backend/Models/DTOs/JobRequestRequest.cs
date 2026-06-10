namespace LGBApp.Backend.Models.DTOs;

public class JobRequestRequest
{
    public int? CustomerId { get; set; }
    public int? CustomerPackageId { get; set; }
    public string Customer { get; set; } = string.Empty;
    public string TaskType { get; set; } = "Service";
    public string Service { get; set; } = string.Empty;
    public int UsedQty { get; set; }
    public int TotalQty { get; set; }
    public string DateRequested { get; set; } = string.Empty;
    public string? ScheduledDate { get; set; }
    public string? DateCompleted { get; set; }
    public string AccountHolder { get; set; } = string.Empty;
    public int? AssignedUserId { get; set; }
    public string JobAssignedTo { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? AssignmentComments { get; set; }
}
