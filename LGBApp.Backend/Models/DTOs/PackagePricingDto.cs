namespace LGBApp.Backend.Models.DTOs;

public class AddOnLineDto
{
    public string Name { get; set; } = string.Empty;
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
}

public class PackagePricingDto
{
    public string Validity { get; set; } = "1 Year";
    public decimal BasePackagePrice { get; set; }
    public List<AddOnLineDto> AddOnLines { get; set; } = new();
}
