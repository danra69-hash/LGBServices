namespace LGBApp.Backend.Models.DTOs;

public class PackageScheduleItemDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int CustomerPackageId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string ItemType { get; set; } = "call";
    public string Title { get; set; } = string.Empty;
    public string ScheduledAt { get; set; } = string.Empty;
    public int? DurationMinutes { get; set; }
    public string Status { get; set; } = "scheduled";
    public string? Notes { get; set; }
    public string? BookingUrl { get; set; }
    public int? SequenceNumber { get; set; }
    public int? JobRequestUnitId { get; set; }
    public int? AssignedUserId { get; set; }
    public string? AssignedUserName { get; set; }
}

public class PackageScheduleItemRequest
{
    public int CustomerId { get; set; }
    public int CustomerPackageId { get; set; }
    public string ItemType { get; set; } = "call";
    public string Title { get; set; } = string.Empty;
    public string ScheduledAt { get; set; } = string.Empty;
    public int? DurationMinutes { get; set; }
    public string Status { get; set; } = "scheduled";
    public string? Notes { get; set; }
    public string? BookingUrl { get; set; }
    public int? SequenceNumber { get; set; }
}
