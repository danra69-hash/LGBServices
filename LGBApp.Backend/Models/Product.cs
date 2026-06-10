namespace LGBApp.Backend.Models;

public class Product
{
    public int ProductId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public string ServicesJson { get; set; } = "[]";
    public string ServiceQuantitiesJson { get; set; } = "{}";
    public string Unit { get; set; } = "EACH";
    public int QtyPerYear { get; set; }
    public decimal PackagePrice { get; set; }
    public string AddOnsJson { get; set; } = "[]";
    public string AddOnQuantitiesJson { get; set; } = "{}";
    public int AddOnsQty { get; set; }
    public decimal AddOnPrice { get; set; }
}
