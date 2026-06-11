namespace LGBApp.Backend.Models;

/// <summary>
/// Grants a ClientSignatory user access to jobs and forms for an additional customer entity.
/// </summary>
public class SignatoryCustomerAccess
{
    public int SignatoryCustomerAccessId { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
