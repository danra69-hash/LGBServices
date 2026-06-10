namespace LGBApp.Backend.Models;

public class JobRequestUnit
{
    public int JobRequestUnitId { get; set; }
    public int JobRequestId { get; set; }
    public JobRequest JobRequest { get; set; } = null!;
    public int UnitNumber { get; set; }
    public int? AssignedUserId { get; set; }
    public string AssignedUserName { get; set; } = string.Empty;
    public DateTime? ScheduledDate { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime? CompletedAt { get; set; }
    public int? PackageScheduleItemId { get; set; }
    public ICollection<JobRequestUnitAssignee> Assignees { get; set; } = new List<JobRequestUnitAssignee>();
}
