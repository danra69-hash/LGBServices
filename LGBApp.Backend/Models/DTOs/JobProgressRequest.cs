namespace LGBApp.Backend.Models.DTOs;

public class JobProgressRequest
{
    /// <summary>1-based unit index. Required when the job has multiple units.</summary>
    public int? UnitNumber { get; set; }
    public string? ScheduledDate { get; set; }
    public int? UserId { get; set; }
    /// <summary>When true, marks the selected unit complete and updates job counters.</summary>
    public bool MarkUnitComplete { get; set; }
    /// <summary>When true, reverts a completed unit back to in progress / pending.</summary>
    public bool MarkUnitIncomplete { get; set; }
}
