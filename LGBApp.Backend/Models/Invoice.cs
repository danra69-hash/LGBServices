namespace LGBApp.Backend.Models;

public class Invoice
{
    public int InvoiceId { get; set; }
    public int CustomerId { get; set; }
    public int? JobRequestId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "MYR";
    public string Status { get; set; } = "Draft";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? IssuedAt { get; set; }
}
