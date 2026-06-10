using LGBApp.Backend.Data;
using LGBApp.Backend.Models.DTOs;
using LGBApp.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _context;

    public CustomersController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CustomerResponse>>> GetCustomers([FromQuery] string? search)
    {
        var query = _context.Customers
            .Include(c => c.AccountHolders)
            .Include(c => c.Packages)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                c.Company.ToLower().Contains(term) ||
                c.Name.ToLower().Contains(term) ||
                c.Email.ToLower().Contains(term) ||
                c.Package.ToLower().Contains(term));
        }

        var customers = await query.OrderBy(c => c.Company).ToListAsync();
        return customers.Select(CustomerMapper.ToResponse).ToList();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CustomerResponse>> GetCustomer(int id)
    {
        var customer = await _context.Customers
            .Include(c => c.AccountHolders)
            .Include(c => c.Packages)
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer == null)
            return NotFound();

        return CustomerMapper.ToResponse(customer);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerResponse>> CreateCustomer(CreateCustomerRequest request)
    {
        var customer = CustomerMapper.FromCreateRequest(request);
        await BillingPartyService.ApplyPartySelectionsAsync(
            _context, customer, request.InvoiceByPartyIds, request.ChargeToPartyIds);
        if (string.IsNullOrWhiteSpace(customer.InvoiceBy) && !string.IsNullOrWhiteSpace(request.InvoiceBy))
            customer.InvoiceBy = request.InvoiceBy;
        if (string.IsNullOrWhiteSpace(customer.ChargeTo) && !string.IsNullOrWhiteSpace(request.ChargeTo))
            customer.ChargeTo = request.ChargeTo;
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        await _context.Entry(customer).Collection(c => c.AccountHolders).LoadAsync();
        await _context.Entry(customer).Collection(c => c.Packages).LoadAsync();
        await JobRequestSyncService.SyncCustomerWorkAsync(_context, customer);
        await _context.SaveChangesAsync();
        var jobs = await _context.JobRequests.Where(j => j.CustomerId == customer.CustomerId).ToListAsync();
        foreach (var job in jobs)
            await JobRequestUnitService.SyncUnitsForJobAsync(_context, job);
        await CustomerClientAdminProvisioner.EnsureClientAdminAsync(_context, customer);
        return CreatedAtAction(nameof(GetCustomer), new { id = customer.CustomerId }, CustomerMapper.ToResponse(customer));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CustomerResponse>> UpdateCustomer(int id, CustomerResponse request)
    {
        var customer = await _context.Customers
            .Include(c => c.AccountHolders)
            .Include(c => c.Packages)
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer == null)
            return NotFound();

        CustomerMapper.ApplyUpdate(customer, request);
        await BillingPartyService.ApplyPartySelectionsAsync(
            _context, customer, request.InvoiceByPartyIds, request.ChargeToPartyIds);

        _context.AccountHolders.RemoveRange(customer.AccountHolders);
        customer.AccountHolders = request.AccountHolders.Select(h => new Models.AccountHolder
        {
            CustomerId = id,
            Name = h.Name,
            Email = h.Email,
            Phone = h.Phone
        }).ToList();

        await _context.SaveChangesAsync();

        await _context.Entry(customer).Collection(c => c.Packages).LoadAsync();
        await JobRequestSyncService.SyncCustomerWorkAsync(_context, customer);
        await _context.SaveChangesAsync();
        var jobs = await _context.JobRequests.Where(j => j.CustomerId == customer.CustomerId).ToListAsync();
        foreach (var job in jobs)
            await JobRequestUnitService.SyncUnitsForJobAsync(_context, job);
        return CustomerMapper.ToResponse(customer);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCustomer(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
            return NotFound();

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
