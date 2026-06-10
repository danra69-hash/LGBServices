using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Services;

public static class BillingPartyService
{
    public static List<int> ParsePartyIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        return JsonHelper.Deserialize<List<int>>(json)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }

    public static async Task ApplyPartySelectionsAsync(
        AppDbContext context,
        Customer customer,
        IReadOnlyList<int>? invoiceByPartyIds,
        IReadOnlyList<int>? chargeToPartyIds)
    {
        if (invoiceByPartyIds != null)
        {
            customer.InvoiceByPartyIdsJson = JsonHelper.Serialize(invoiceByPartyIds.Distinct().ToList());
            customer.InvoiceBy = await FormatPartyNamesAsync(context, customer.InvoiceByPartyIdsJson);
        }

        if (chargeToPartyIds != null)
        {
            customer.ChargeToPartyIdsJson = JsonHelper.Serialize(chargeToPartyIds.Distinct().ToList());
            customer.ChargeTo = await FormatPartyNamesAsync(context, customer.ChargeToPartyIdsJson);
        }
    }

    public static async Task<string> FormatPartyNamesAsync(AppDbContext context, string idsJson)
    {
        var ids = ParsePartyIds(idsJson);
        if (ids.Count == 0)
            return string.Empty;

        var names = await context.BillingParties
            .Where(b => ids.Contains(b.BillingPartyId))
            .OrderBy(b => b.SortOrder)
            .ThenBy(b => b.Name)
            .Select(b => b.Name)
            .ToListAsync();

        return string.Join(", ", names);
    }

    public static async Task SeedFromLegacyCustomerFieldsAsync(AppDbContext context)
    {
        var customers = await context.Customers.ToListAsync();
        var existingNames = await context.BillingParties.Select(b => b.Name).ToListAsync();
        var nameSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

        foreach (var customer in customers)
        {
            foreach (var raw in new[] { customer.InvoiceBy, customer.ChargeTo })
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (nameSet.Contains(part)) continue;
                    context.BillingParties.Add(new BillingParty
                    {
                        Name = part,
                        Category = "Both",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                    });
                    nameSet.Add(part);
                }
            }
        }

        await context.SaveChangesAsync();

        foreach (var customer in customers)
        {
            if (ParsePartyIds(customer.InvoiceByPartyIdsJson).Count == 0 && !string.IsNullOrWhiteSpace(customer.InvoiceBy))
            {
                var ids = await ResolveIdsForNamesAsync(context, customer.InvoiceBy);
                customer.InvoiceByPartyIdsJson = JsonHelper.Serialize(ids);
            }
            if (ParsePartyIds(customer.ChargeToPartyIdsJson).Count == 0 && !string.IsNullOrWhiteSpace(customer.ChargeTo))
            {
                var ids = await ResolveIdsForNamesAsync(context, customer.ChargeTo);
                customer.ChargeToPartyIdsJson = JsonHelper.Serialize(ids);
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task<List<int>> ResolveIdsForNamesAsync(AppDbContext context, string csv)
    {
        var names = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parties = await context.BillingParties.ToListAsync();
        return names
            .Select(n => parties.FirstOrDefault(p => p.Name.Equals(n, StringComparison.OrdinalIgnoreCase))?.BillingPartyId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
    }
}
