namespace LGBApp.Backend.Models;

public class PackageScheduleItem
{
    public int PackageScheduleItemId { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int CustomerPackageId { get; set; }
    public CustomerPackage CustomerPackage { get; set; } = null!;
    public string ItemType { get; set; } = "call";
    public string Title { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public int? DurationMinutes { get; set; }
    public string Status { get; set; } = "scheduled";
    public string? Notes { get; set; }
    public string? BookingUrl { get; set; }
    public int? SequenceNumber { get; set; }
    public int? JobRequestUnitId { get; set; }
    public int? AssignedUserId { get; set; }
}
