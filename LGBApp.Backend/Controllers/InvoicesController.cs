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
        if (customer == null) return BadRequest("Customer not found.");

        if (request.JobRequestId.HasValue)
        {
            var job = await _context.JobRequests.FindAsync(request.JobRequestId.Value);
            if (job == null) return BadRequest("Job not found.");
        }

        var count = await _context.Invoices.CountAsync();
        var invoice = new Invoice
        {
            CustomerId = request.CustomerId,
            JobRequestId = request.JobRequestId,
            InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{count + 1:D4}",
            Amount = request.Amount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "MYR" : request.Currency.Trim(),
            Status = "Draft",
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
        };

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

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
