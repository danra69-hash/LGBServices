namespace LGBApp.Backend.Models.DTOs;

public class DashboardStatsResponse
{
    public int ActiveCustomers { get; set; }
    public string ActiveCustomersChange { get; set; } = "+0%";
    public decimal TotalRevenue { get; set; }
    public string TotalRevenueChange { get; set; } = "+0%";
    public int OutstandingServices { get; set; }
    public string OutstandingServicesChange { get; set; } = "+0%";
    public int TotalServicesCompleted { get; set; }
    public string TotalServicesCompletedChange { get; set; } = "+0%";
    public int AdHocServicesCount { get; set; }
    public decimal AdHocRevenue { get; set; }
    public string AdHocRevenueChange { get; set; } = "+0%";
}
