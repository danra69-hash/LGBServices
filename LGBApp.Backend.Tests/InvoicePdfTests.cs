using LGBApp.Backend.Models;
using LGBApp.Backend.Services;

namespace LGBApp.Backend.Tests;

public class InvoicePdfTests
{
    [Fact]
    public void Build_ReturnsPdfMagicBytes()
    {
        var invoice = new Invoice
        {
            InvoiceNumber = "INV-TEST-0001",
            Amount = 1200.50m,
            Currency = "MYR",
            Status = "Issued",
            Notes = "Annual retainer",
            CreatedAt = DateTime.UtcNow,
            IssuedAt = DateTime.UtcNow,
        };
        var customer = new Customer
        {
            Company = "Test Co Sdn Bhd",
            Name = "Alice",
            Email = "alice@test.local",
        };

        var bytes = InvoicePdfService.Build(invoice, customer);
        Assert.True(bytes.Length > 100);
        // %PDF
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }
}
