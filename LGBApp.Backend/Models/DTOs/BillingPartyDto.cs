namespace LGBApp.Backend.Models.DTOs;

public class BillingPartyDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Both";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class BillingPartyRequest
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Both";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
