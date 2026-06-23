namespace LGBApp.Backend.Models.DTOs;

public class InvoiceResponse
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int? JobRequestId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "MYR";
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? IssuedAt { get; set; }
}

public class CreateInvoiceRequest
{
    public int CustomerId { get; set; }
    public int? JobRequestId { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? Notes { get; set; }
}
