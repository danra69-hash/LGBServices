using LGBApp.Backend.Models;
using LGBApp.Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Data;

/// <summary>
/// Canonical package catalog from the Figma Make prototype.
/// Bundled add-ons are included in the package price (breakdown for display only).
/// </summary>
public static class FigmaProductCatalog
{
    public const decimal BundledAddOnUnitPrice = 120m;
    public const decimal CustomerOptionalAddOnUnitPrice = 120m;

    private static readonly string[] AllServices =
    [
        "Annual Return",
        "BO Declaration",
        "AR Filing to MBRS",
        "Resolution on annual audited account filing",
        "Submission of annual audited account MBRS zip file",
        "Prov of register Office",
        "Assisting Auditor on statutory Audit",
        "Secretarial record Checks",
        "Prepare Resolution",
        "Follow up with Reso signatory",
    ];

    public static readonly string[] PackageNames =
    [
        "Dormant",
        "Basic Package",
        "Professional Package",
        "Enterprise Package",
        "Enterprise Plus",
    ];

    public static void SyncCatalog(AppDbContext context)
    {
        var catalog = BuildCatalogProducts();
        var catalogNames = new HashSet<string>(PackageNames, StringComparer.OrdinalIgnoreCase);

        var toRemove = context.Products
            .Where(p => !catalogNames.Contains(p.PackageName))
            .ToList();

        if (toRemove.Count > 0)
            context.Products.RemoveRange(toRemove);

        foreach (var template in catalog)
        {
            var existing = context.Products
                .FirstOrDefault(p => p.PackageName == template.PackageName);

            if (existing == null)
            {
                context.Products.Add(template);
                continue;
            }

            existing.ServicesJson = template.ServicesJson;
            existing.ServiceQuantitiesJson = template.ServiceQuantitiesJson;
            existing.Unit = template.Unit;
            existing.QtyPerYear = template.QtyPerYear;
            existing.PackagePrice = template.PackagePrice;
            existing.AddOnsJson = template.AddOnsJson;
            existing.AddOnQuantitiesJson = template.AddOnQuantitiesJson;
            existing.AddOnsQty = template.AddOnsQty;
            existing.AddOnPrice = template.AddOnPrice;
        }

        context.SaveChanges();
    }

    private static List<Product> BuildCatalogProducts()
    {
        return
        [
            BuildPackage(
                "Dormant",
                2360m,
                AllServicesQty(1, 1),
                addOns: null,
                addOnPrice: 0),
            BuildPackage(
                "Basic Package",
                3200m,
                AllServicesQty(1, 1, prepareQty: 5, followUpQty: 5),
                addOns: null,
                addOnPrice: 0),
            BuildPackage(
                "Professional Package",
                4250m,
                AllServicesQty(1, 1, prepareQty: 10, followUpQty: 10),
                addOns: new Dictionary<string, int> { ["Local Support Service"] = 4 },
                addOnPrice: 400m),
            BuildPackage(
                "Enterprise Package",
                5930m,
                AllServicesQty(1, 1, prepareQty: 18, followUpQty: 18),
                addOns: new Dictionary<string, int>
                {
                    ["Overseas Support Service"] = 3,
                    ["Attend Board Meeting"] = 3,
                },
                addOnPrice: 720m),
            BuildPackage(
                "Enterprise Plus",
                6980m,
                AllServicesQty(1, 1, prepareQty: 23, followUpQty: 23),
                addOns: new Dictionary<string, int>
                {
                    ["Overseas Support Service"] = 6,
                    ["Attend Board Meeting"] = 4,
                    ["Prepare board meeting Minutes"] = 2,
                },
                addOnPrice: 1440m),
        ];
    }

    private static Dictionary<string, int> AllServicesQty(
        int baseQty,
        int qtyPerYear,
        int prepareQty = 1,
        int followUpQty = 1)
    {
        var quantities = new Dictionary<string, int>();
        foreach (var service in AllServices)
        {
            quantities[service] = service switch
            {
                "Prepare Resolution" => prepareQty,
                "Follow up with Reso signatory" => followUpQty,
                _ => baseQty,
            };
        }

        return quantities;
    }

    private static Product BuildPackage(
        string name,
        decimal packagePrice,
        Dictionary<string, int> serviceQuantities,
        Dictionary<string, int>? addOns,
        decimal addOnPrice)
    {
        var addOnQuantities = addOns ?? new Dictionary<string, int>();
        var addOnNames = addOnQuantities.Keys.ToList();
        var addOnsQty = addOnQuantities.Values.Sum();

        return new Product
        {
            PackageName = name,
            ServicesJson = JsonHelper.Serialize(AllServices.ToList()),
            ServiceQuantitiesJson = JsonHelper.Serialize(serviceQuantities),
            Unit = "EACH",
            QtyPerYear = 12,
            PackagePrice = packagePrice,
            AddOnsJson = JsonHelper.Serialize(addOnNames),
            AddOnQuantitiesJson = JsonHelper.Serialize(addOnQuantities),
            AddOnsQty = addOnsQty,
            AddOnPrice = addOnPrice,
        };
    }
}
