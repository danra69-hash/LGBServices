namespace LGBApp.Backend.Models;

/// <summary>Admin-maintained name for Invoice By / Charge To multi-select on customers.</summary>
public class BillingParty
{
    public int BillingPartyId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>InvoiceBy, ChargeTo, or Both</summary>
    public string Category { get; set; } = "Both";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
