namespace LGBApp.Backend.Models;

public class JobRequestUnitAssignee
{
    public int JobRequestUnitAssigneeId { get; set; }
    public int JobRequestUnitId { get; set; }
    public JobRequestUnit Unit { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
