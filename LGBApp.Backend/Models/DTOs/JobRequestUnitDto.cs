namespace LGBApp.Backend.Models.DTOs;

public class JobRequestUnitDto
{
    public int Id { get; set; }
    public int UnitNumber { get; set; }
    public int? AssignedUserId { get; set; }
    public string AssignedUserName { get; set; } = string.Empty;
    public List<UnitAssigneeDto> Assignees { get; set; } = [];
    public string? ScheduledDate { get; set; }
    public string Status { get; set; } = "Pending";
}

public class WorkTrackerItemDto
{
    public int UnitId { get; set; }
    public int JobId { get; set; }
    public int UnitNumber { get; set; }
    public int? CustomerPackageId { get; set; }
    public string Customer { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public string? ScheduledDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? AssignedUserId { get; set; }
    public string AssignedUserName { get; set; } = string.Empty;
}
