using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;

namespace LGBApp.Backend.Services;

public static class ProductMapper
{
    public static ProductResponse ToResponse(Product product)
    {
        var addOnQuantities = JsonHelper.Deserialize<Dictionary<string, int>>(product.AddOnQuantitiesJson);
        var addOnsQty = product.AddOnsQty > 0 ? product.AddOnsQty : addOnQuantities.Values.Sum();

        return new ProductResponse
        {
            Id = product.ProductId,
            PackageName = product.PackageName,
            Services = JsonHelper.Deserialize<List<string>>(product.ServicesJson),
            ServiceQuantities = JsonHelper.Deserialize<Dictionary<string, int>>(product.ServiceQuantitiesJson),
            Unit = product.Unit,
            QtyPerYear = product.QtyPerYear,
            PackagePrice = product.PackagePrice,
            AddOns = JsonHelper.Deserialize<List<string>>(product.AddOnsJson),
            AddOnQuantities = addOnQuantities,
            AddOnsQty = addOnsQty,
            AddOnPrice = product.AddOnPrice
        };
    }

    public static void ApplyRequest(Product product, ProductRequest request)
    {
        product.PackageName = request.PackageName?.Trim() ?? string.Empty;
        product.ServicesJson = JsonHelper.Serialize(request.Services ?? []);
        product.ServiceQuantitiesJson = JsonHelper.Serialize(request.ServiceQuantities ?? []);
        product.Unit = string.IsNullOrWhiteSpace(request.Unit) ? "EACH" : request.Unit;
        product.QtyPerYear = request.QtyPerYear;
        product.PackagePrice = request.PackagePrice;
        product.AddOnsJson = JsonHelper.Serialize(request.AddOns ?? []);
        product.AddOnQuantitiesJson = JsonHelper.Serialize(request.AddOnQuantities ?? []);
        var quantitiesSum = request.AddOnQuantities?.Values.Sum() ?? 0;
        product.AddOnsQty = quantitiesSum > 0 ? quantitiesSum : request.AddOnsQty;
        product.AddOnPrice = request.AddOnPrice;
    }
}
