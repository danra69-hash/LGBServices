namespace LGBApp.Backend.Models;

/// <summary>Form record for non-MOI/MOA service tasks.</summary>
public class ServiceJobForm
{
    public int ServiceJobFormId { get; set; }
    public int JobRequestId { get; set; }
    public JobRequest JobRequest { get; set; } = null!;
    public string Company { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string FormDataJson { get; set; } = "{}";
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
