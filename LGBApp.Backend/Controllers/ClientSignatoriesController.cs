using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;
using LGBApp.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "ClientAdmin")]
public class ClientSignatoriesController : ControllerBase
{
    private readonly AppDbContext _context;

    public ClientSignatoriesController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AccountHolderDto>>> GetSignatories()
    {
        var customerId = AuthHelper.CurrentCustomerId(User);
        if (!customerId.HasValue)
            return BadRequest(new { message = "Your account is not linked to a customer." });

        var holders = await _context.AccountHolders
            .Where(h => h.CustomerId == customerId)
            .OrderBy(h => h.Name)
            .ToListAsync();

        return holders.Select(CustomerMapper.ToAccountHolderDto).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<AccountHolderDto>> AddSignatory(AddClientSignatoryRequest request)
    {
        if (!request.Moi && !request.MoiApproval && !request.Moa)
            return BadRequest(new { message = "Select at least one of MOI, MOI Approval, or MOA." });

        var customerId = AuthHelper.CurrentCustomerId(User);
        if (!customerId.HasValue)
            return BadRequest(new { message = "Your account is not linked to a customer." });

        var customer = await _context.Customers
            .Include(c => c.AccountHolders)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (customer == null)
            return NotFound();

        var email = request.Email.Trim();
        var existingHolder = customer.AccountHolders.FirstOrDefault(h =>
            string.Equals(h.Email, email, StringComparison.OrdinalIgnoreCase)
            || string.Equals(h.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase));

        AccountHolder holder;
        if (existingHolder != null)
        {
            holder = existingHolder;
            holder.Name = request.Name.Trim();
            holder.Email = email;
            holder.Phone = request.Phone?.Trim() ?? string.Empty;
            holder.NeedsMoi |= request.Moi;
            holder.NeedsMoiApproval |= request.MoiApproval;
            holder.NeedsMoa |= request.Moa;
            holder.ClientAdded = true;
            holder.AddedByUserId = AuthHelper.CurrentUserId(User);
        }
        else
        {
            holder = new AccountHolder
            {
                CustomerId = customer.CustomerId,
                Name = request.Name.Trim(),
                Email = email,
                Phone = request.Phone?.Trim() ?? string.Empty,
                NeedsMoi = request.Moi,
                NeedsMoiApproval = request.MoiApproval,
                NeedsMoa = request.Moa,
                ClientAdded = true,
                AddedByUserId = AuthHelper.CurrentUserId(User),
            };
            customer.AccountHolders.Add(holder);
        }

        CustomerSignatoryProvisioner.SyncCustomerSignerLists(customer);
        await _context.SaveChangesAsync();

        await CustomerSignatoryProvisioner.EnsureSignatoryUserAsync(
            _context, customer, holder, AuthHelper.CurrentUserId(User), clientAdded: true);
        await JobRequestSyncService.SyncCustomerWorkAsync(_context, customer);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetSignatories), CustomerMapper.ToAccountHolderDto(holder));
    }
}
