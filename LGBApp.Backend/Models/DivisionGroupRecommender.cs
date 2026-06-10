namespace LGBApp.Backend.Models;

public class DivisionGroupRecommender
{
    public int DivisionGroupRecommenderId { get; set; }
    public int DivisionGroupId { get; set; }
    public DivisionGroup DivisionGroup { get; set; } = null!;
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}
