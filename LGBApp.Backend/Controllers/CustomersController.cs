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
    public async Task<ActionResult<IEnumerable<CustomerResponse>>> GetCustomers(
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? pageSize)
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

        var (p, size) = Pagination.Normalize(page, pageSize);
        var customers = await query
            .OrderBy(c => c.Company)
            .Skip((p - 1) * size)
            .Take(size)
            .ToListAsync();
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
        return await TransactionHelper.ExecuteInTransactionAsync(_context, async () =>
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
            await CustomerSignatoryProvisioner.SyncSignatoriesAsync(
                _context, customer, AuthHelper.CurrentUserId(User));
            await JobRequestSyncService.SyncCustomerWorkAsync(_context, customer);
            await _context.SaveChangesAsync();

            ActionResult<CustomerResponse> created = CreatedAtAction(
                nameof(GetCustomer),
                new { id = customer.CustomerId },
                CustomerMapper.ToResponse(customer));
            return (true, created);
        });
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

        return await TransactionHelper.ExecuteInTransactionAsync(_context, async () =>
        {
            CustomerMapper.ApplyUpdate(customer, request);
            await BillingPartyService.ApplyPartySelectionsAsync(
                _context, customer, request.InvoiceByPartyIds, request.ChargeToPartyIds);

            // §7.5: upsert holders by id — do not churn IDs on every save
            var existingById = customer.AccountHolders.ToDictionary(h => h.AccountHolderId);
            var keepIds = new HashSet<int>();
            foreach (var h in request.AccountHolders)
            {
                var needsMoi = h.Moi || request.Moi.Contains(h.Name, StringComparer.OrdinalIgnoreCase);
                var needsMoiApproval = h.MoiApproval || request.MoiApproval.Contains(h.Name, StringComparer.OrdinalIgnoreCase);
                var needsMoa = h.Moa || request.Moa.Contains(h.Name, StringComparer.OrdinalIgnoreCase);

                if (h.Id > 0 && existingById.TryGetValue(h.Id, out var prior))
                {
                    prior.Name = h.Name;
                    prior.Email = h.Email;
                    prior.Phone = h.Phone;
                    prior.NeedsMoi = needsMoi;
                    prior.NeedsMoiApproval = needsMoiApproval;
                    prior.NeedsMoa = needsMoa;
                    prior.ClientAdded = prior.ClientAdded || h.ClientAdded;
                    if (h.AddedByUserId.HasValue)
                        prior.AddedByUserId = h.AddedByUserId;
                    keepIds.Add(prior.AccountHolderId);
                }
                else
                {
                    customer.AccountHolders.Add(new Models.AccountHolder
                    {
                        CustomerId = id,
                        Name = h.Name,
                        Email = h.Email,
                        Phone = h.Phone,
                        NeedsMoi = needsMoi,
                        NeedsMoiApproval = needsMoiApproval,
                        NeedsMoa = needsMoa,
                        ClientAdded = h.ClientAdded,
                        AddedByUserId = h.AddedByUserId,
                    });
                }
            }

            foreach (var orphan in customer.AccountHolders
                         .Where(h => h.AccountHolderId > 0 && !keepIds.Contains(h.AccountHolderId))
                         .ToList())
                _context.AccountHolders.Remove(orphan);

            CustomerSignatoryProvisioner.SyncCustomerSignerLists(customer);
            await _context.SaveChangesAsync();

            await _context.Entry(customer).Collection(c => c.Packages).LoadAsync();
            await CustomerSignatoryProvisioner.SyncSignatoriesAsync(
                _context, customer, AuthHelper.CurrentUserId(User));
            await JobRequestSyncService.SyncCustomerWorkAsync(_context, customer);
            await _context.SaveChangesAsync();
            var jobs = await _context.JobRequests.Where(j => j.CustomerId == customer.CustomerId).ToListAsync();
            foreach (var job in jobs)
                await JobRequestUnitService.SyncUnitsForJobAsync(_context, job);

            ActionResult<CustomerResponse> ok = CustomerMapper.ToResponse(customer);
            return (true, ok);
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCustomer(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
            return NotFound();

        // §6.6: block delete when jobs exist — cascading strands forms with stale names
        var jobCount = await _context.JobRequests.CountAsync(j => j.CustomerId == id);
        if (jobCount > 0)
        {
            return Conflict(new
            {
                message = $"Cannot delete customer with {jobCount} job request(s). Cancel or reassign jobs first.",
                jobCount,
            });
        }

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
