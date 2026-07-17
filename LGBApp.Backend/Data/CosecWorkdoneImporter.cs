using System.Text.Json;
using LGBApp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Data;

/// <summary>
/// Maps CubeV Cosec Workdone Tracking rows onto existing CompletedServices (no new UI).
/// Idempotent by Customer+Service+DateCompleted fingerprint.
/// </summary>
public static class CosecWorkdoneImporter
{
    private sealed class WorkdoneRow
    {
        public string? RequestDate { get; set; }
        public string? CompletionDate { get; set; }
        public string? Company { get; set; }
        public string? WorkItem { get; set; }
        public string? Description { get; set; }
        public string? FeeCharges { get; set; }
        public string? FeeChargeTo { get; set; }
        public string? InvoiceIssuer { get; set; }
    }

    public static void Seed(AppDbContext context)
    {
        var path = ResolveSeedPath("cosec-workdone-history.json");
        if (path == null || !File.Exists(path))
        {
            Console.WriteLine("[Startup] cosec-workdone-history.json not found — skip workdone import.");
            return;
        }

        var rows = JsonSerializer.Deserialize<List<WorkdoneRow>>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        var customers = context.Customers.AsNoTracking().ToList();
        var existing = context.CompletedServices
            .AsNoTracking()
            .Select(c => new { c.Customer, c.Service, c.DateCompleted, c.AccountHolder })
            .ToList();

        var added = 0;
        var skipped = 0;

        foreach (var row in rows)
        {
            var company = (row.Company ?? "").Trim();
            var workItem = (row.WorkItem ?? "").Trim();
            if (string.IsNullOrWhiteSpace(company)
                || string.IsNullOrWhiteSpace(workItem)
                || company.Contains("(please specify)", StringComparison.OrdinalIgnoreCase)
                || workItem.Contains("**", StringComparison.Ordinal)
                || string.Equals(row.InvoiceIssuer, "#N/A", StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }

            var customer = customers.FirstOrDefault(c =>
                c.Company.Equals(company, StringComparison.OrdinalIgnoreCase));
            if (customer == null)
            {
                skipped++;
                continue;
            }

            var service = workItem;
            if (!string.IsNullOrWhiteSpace(row.Description))
                service = $"{workItem} — {row.Description.Trim()}";

            var requested = ParseDate(row.RequestDate) ?? DateTime.UtcNow.Date;
            var completed = ParseDate(row.CompletionDate) ?? requested;
            var note = BuildNote(row);

            if (existing.Any(e =>
                    e.Customer.Equals(customer.Company, StringComparison.OrdinalIgnoreCase)
                    && e.Service.Equals(service, StringComparison.OrdinalIgnoreCase)
                    && e.DateCompleted.Date == completed.Date))
            {
                skipped++;
                continue;
            }

            context.CompletedServices.Add(new CompletedService
            {
                Customer = customer.Company,
                Service = service,
                UsedQty = 1,
                TotalQty = 1,
                DateRequested = requested,
                DateCompleted = completed,
                AccountHolder = note,
                JobAssignedTo = "Cosec (CubeV workdone import)",
                Status = "Completed",
                CreatedAt = DateTime.UtcNow,
            });
            added++;
        }

        if (added > 0)
            context.SaveChanges();

        Console.WriteLine($"[Startup] Cosec workdone import: {added} completed rows ({skipped} skipped).");
    }

    private static string BuildNote(WorkdoneRow row)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(row.FeeCharges))
            parts.Add($"Fee: {row.FeeCharges.Trim()}");
        if (!string.IsNullOrWhiteSpace(row.FeeChargeTo)
            && !string.Equals(row.FeeChargeTo, "No bill", StringComparison.OrdinalIgnoreCase))
            parts.Add($"Charge to: {row.FeeChargeTo.Trim()}");
        if (!string.IsNullOrWhiteSpace(row.InvoiceIssuer)
            && !string.Equals(row.InvoiceIssuer, "No", StringComparison.OrdinalIgnoreCase))
            parts.Add($"Invoice: {row.InvoiceIssuer.Trim()}");
        return parts.Count == 0 ? "CubeV workdone" : string.Join("; ", parts);
    }

    private static DateTime? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return DateTime.TryParse(raw, out var dt) ? DateTime.SpecifyKind(dt.Date, DateTimeKind.Utc) : null;
    }

    private static string? ResolveSeedPath(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Data", "Seed", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "Data", "Seed", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "LGBApp.Backend", "Data", "Seed", fileName),
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
