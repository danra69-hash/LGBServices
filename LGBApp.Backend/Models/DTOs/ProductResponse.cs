namespace LGBApp.Backend.Models.DTOs;

public class ProductResponse
{
    public int Id { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public List<string> Services { get; set; } = new();
    public Dictionary<string, int> ServiceQuantities { get; set; } = new();
    public string Unit { get; set; } = string.Empty;
    public int QtyPerYear { get; set; }
    public decimal PackagePrice { get; set; }
    public List<string> AddOns { get; set; } = new();
    public Dictionary<string, int> AddOnQuantities { get; set; } = new();
    public int AddOnsQty { get; set; }
    public decimal AddOnPrice { get; set; }
}
