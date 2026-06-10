namespace LGBApp.Backend.Models.DTOs;

public class ClientPortalSummaryDto
{
    public string CompanyName { get; set; } = string.Empty;
    public int ActivePackages { get; set; }
    public decimal ActivePackageValue { get; set; }
    public int OpenJobs { get; set; }
    public int CompletedJobs { get; set; }
    public int TeamMembers { get; set; }
}
