namespace LGBApp.Backend.Models.DTOs;

public class CompletedServiceResponse
{
    public int Id { get; set; }
    public string Customer { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public int UsedQty { get; set; }
    public int TotalQty { get; set; }
    public string DateRequested { get; set; } = string.Empty;
    public string DateCompleted { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public string JobAssignedTo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
