namespace LGBApp.Backend.Models.DTOs;

public class AssignJobRequest
{
    public int UserId { get; set; }
    /// <summary>1-based unit index. When omitted, assigns the first unassigned pending unit.</summary>
    public int? UnitNumber { get; set; }
    public string? AcceptedDate { get; set; }
    public string? Comments { get; set; }
    /// <summary>When true, removes the user from the unit instead of adding them.</summary>
    public bool Remove { get; set; }
}
