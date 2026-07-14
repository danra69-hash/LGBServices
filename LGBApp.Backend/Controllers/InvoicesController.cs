using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;
using LGBApp.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class InvoicesController : ControllerBase
{
    private readonly AppDbContext _context;

    public InvoicesController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<InvoiceResponse>>> List([FromQuery] int? customerId)
    {
        if (!AuthHelper.IsInternalStaff(User))
            return Forbid();

        var query = _context.Invoices.AsNoTracking();
        if (customerId.HasValue)
            query = query.Where(i => i.CustomerId == customerId.Value);

        var invoices = await query
            .OrderByDescending(i => i.CreatedAt)
            .Take(200)
            .Join(
                _context.Customers.AsNoTracking(),
                i => i.CustomerId,
                c => c.CustomerId,
                (i, c) => new InvoiceResponse
                {
                    Id = i.InvoiceId,
                    CustomerId = i.CustomerId,
                    CustomerName = c.Company,
                    JobRequestId = i.JobRequestId,
                    InvoiceNumber = i.InvoiceNumber,
                    Amount = i.Amount,
                    Currency = i.Currency,
                    Status = i.Status,
                    Notes = i.Notes,
                    CreatedAt = i.CreatedAt,
                    IssuedAt = i.IssuedAt,
                })
            .ToListAsync();

        return Ok(invoices);
    }

    [HttpPost]
    public async Task<ActionResult<InvoiceResponse>> Create(CreateInvoiceRequest request)
    {
        if (!AuthHelper.IsAdmin(User))
            return Forbid();

        var customer = await _context.Customers.FindAsync(request.CustomerId);
        if (customer == null) return BadRequest(new { message = "Customer not found." });

        if (request.Amount <= 0)
            return BadRequest(new { message = "Amount must be greater than zero." });

        if (request.JobRequestId.HasValue)
        {
            var job = await _context.JobRequests.FindAsync(request.JobRequestId.Value);
            if (job == null) return BadRequest(new { message = "Job not found." });
        }

        Invoice? invoice = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var invoiceNumber = await NextInvoiceNumberAsync();
            invoice = new Invoice
            {
                CustomerId = request.CustomerId,
                JobRequestId = request.JobRequestId,
                InvoiceNumber = invoiceNumber,
                Amount = request.Amount,
                Currency = string.IsNullOrWhiteSpace(request.Currency) ? "MYR" : request.Currency.Trim(),
                Status = "Draft",
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow,
            };

            _context.Invoices.Add(invoice);
            try
            {
                await _context.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException) when (attempt < 2)
            {
                _context.Entry(invoice).State = EntityState.Detached;
                invoice = null;
            }
        }

        if (invoice == null)
            return Conflict(new { message = "Could not allocate a unique invoice number. Retry." });

        return Ok(new InvoiceResponse
        {
            Id = invoice.InvoiceId,
            CustomerId = invoice.CustomerId,
            CustomerName = customer.Company,
            JobRequestId = invoice.JobRequestId,
            InvoiceNumber = invoice.InvoiceNumber,
            Amount = invoice.Amount,
            Currency = invoice.Currency,
            Status = invoice.Status,
            Notes = invoice.Notes,
            CreatedAt = invoice.CreatedAt,
            IssuedAt = invoice.IssuedAt,
        });
    }

    /// <summary>C5: next INV-yyyyMMdd-#### for today based on max existing suffix.</summary>
    private async Task<string> NextInvoiceNumberAsync()
    {
        var prefix = $"INV-{DateTime.UtcNow:yyyyMMdd}-";
        var existing = await _context.Invoices.AsNoTracking()
            .Where(i => i.InvoiceNumber.StartsWith(prefix))
            .Select(i => i.InvoiceNumber)
            .ToListAsync();

        var maxSuffix = 0;
        foreach (var number in existing)
        {
            var suffixPart = number.Length > prefix.Length ? number[prefix.Length..] : "";
            if (int.TryParse(suffixPart, out var n) && n > maxSuffix)
                maxSuffix = n;
        }

        return $"{prefix}{maxSuffix + 1:D4}";
    }

    [HttpGet("{id}/pdf")]
    public async Task<IActionResult> DownloadPdf(int id)
    {
        if (!AuthHelper.IsInternalStaff(User))
            return Forbid();

        var invoice = await _context.Invoices
            .AsNoTracking()
            .Join(
                _context.Customers.AsNoTracking(),
                i => i.CustomerId,
                c => c.CustomerId,
                (i, c) => new { Invoice = i, Customer = c })
            .FirstOrDefaultAsync(x => x.Invoice.InvoiceId == id);

        if (invoice == null) return NotFound();

        var lines = new[]
        {
            $"Invoice: {invoice.Invoice.InvoiceNumber}",
            $"Customer: {invoice.Customer.Company}",
            $"Amount: {invoice.Invoice.Currency} {invoice.Invoice.Amount:F2}",
            $"Status: {invoice.Invoice.Status}",
            $"Created: {invoice.Invoice.CreatedAt:u}",
            invoice.Invoice.Notes ?? string.Empty,
        };

        var bytes = System.Text.Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines));
        return File(bytes, "text/plain", $"{invoice.Invoice.InvoiceNumber}.txt");
    }
}
