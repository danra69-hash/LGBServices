using System.Security.Claims;
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
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetUsers()
    {
        if (!AuthHelper.CanManageUsers(User))
            return Forbid();

        var query = _context.Users.Include(u => u.Customer).AsQueryable();

        if (AuthHelper.IsClientAdmin(User))
        {
            var customerId = AuthHelper.CurrentCustomerId(User);
            if (!customerId.HasValue)
                return Ok(Array.Empty<UserResponse>());

            query = query.Where(u =>
                u.CustomerId == customerId
                && u.Role == UserRoles.Client);
        }

        var users = await query.OrderBy(u => u.Name).ToListAsync();
        return users.Select(UserMapper.ToResponse).ToList();
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> GetCurrentUser()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var user = await _context.Users.Include(u => u.Customer).FirstOrDefaultAsync(u => u.UserId == userId.Value);
        if (user == null)
            return NotFound();

        return UserMapper.ToResponse(user);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserResponse>> GetUser(int id)
    {
        if (!AuthHelper.CanManageUsers(User))
            return Forbid();

        var user = await _context.Users.Include(u => u.Customer).FirstOrDefaultAsync(u => u.UserId == id);
        if (user == null)
            return NotFound();

        if (!AuthHelper.CanManageUser(User, user))
            return Forbid();

        return UserMapper.ToResponse(user);
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> CreateUser(CreateUserRequest request)
    {
        if (!AuthHelper.CanManageUsers(User))
            return Forbid();

        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            return Conflict("Email is already registered.");

        var role = string.IsNullOrWhiteSpace(request.Role) ? UserRoles.User : request.Role;
        if (!UserRoles.IsValid(role))
            return BadRequest($"Invalid role. Use: {string.Join(", ", UserRoles.All)}");

        var allowed = AuthHelper.CreatableRoles(User);
        if (!allowed.Contains(role, StringComparer.OrdinalIgnoreCase))
            return Forbid();

        var customerId = await ResolveCustomerIdForRole(role, request.CustomerId, forceScopedCustomer: AuthHelper.IsClientAdmin(User));
        if (customerId == -1)
            return BadRequest("ClientAdmin and Client roles require a customer.");

        var inviterId = GetCurrentUserId();

        var user = new User
        {
            Email = request.Email,
            PasswordHash = PasswordHasher.Hash(request.Password),
            Name = request.Name,
            Mobile = request.Mobile,
            Role = role,
            JobTitle = UserRoles.IsExternalRole(role) ? string.Empty : (request.JobTitle ?? string.Empty),
            CanRecommendMoi = UserRoles.IsExternalRole(role) ? false : request.CanRecommendMoi,
            CustomerId = customerId,
            InvitedByUserId = inviterId,
            IsVerified = AuthHelper.IsClientAdmin(User),
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow,
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await _context.Entry(user).Reference(u => u.Customer).LoadAsync();
        return CreatedAtAction(nameof(GetUser), new { id = user.UserId }, UserMapper.ToResponse(user));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, UpdateUserRequest request)
    {
        if (!AuthHelper.CanManageUsers(User))
            return Forbid();

        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        if (!AuthHelper.CanManageUser(User, user))
            return Forbid();

        var emailTaken = await _context.Users
            .AnyAsync(u => u.Email == request.Email && u.UserId != id);
        if (emailTaken)
            return Conflict("Email is already in use.");

        if (!UserRoles.IsValid(request.Role))
            return BadRequest($"Invalid role. Use: {string.Join(", ", UserRoles.All)}");

        var allowed = AuthHelper.CreatableRoles(User);
        if (!allowed.Contains(request.Role, StringComparer.OrdinalIgnoreCase))
            return Forbid();

        var customerId = await ResolveCustomerIdForRole(request.Role, request.CustomerId, forceScopedCustomer: AuthHelper.IsClientAdmin(User));
        if (customerId == -1)
            return BadRequest("ClientAdmin and Client roles require a customer.");

        user.Name = request.Name;
        user.Email = request.Email;
        user.Mobile = request.Mobile;
        user.Role = request.Role;
        user.JobTitle = UserRoles.IsExternalRole(request.Role) ? string.Empty : (request.JobTitle ?? string.Empty);
        user.CanRecommendMoi = UserRoles.IsExternalRole(request.Role) ? false : request.CanRecommendMoi;
        user.CustomerId = customerId;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        if (!AuthHelper.CanManageUsers(User))
            return Forbid();

        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        if (!AuthHelper.CanManageUser(User, user))
            return Forbid();

        var currentId = GetCurrentUserId();
        if (currentId == user.UserId)
            return BadRequest("You cannot delete your own account.");

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }

    /// <returns>null ok, -1 invalid</returns>
    private async Task<int?> ResolveCustomerIdForRole(string role, int? customerId, bool forceScopedCustomer)
    {
        if (!UserRoles.IsExternalRole(role))
            return null;

        if (forceScopedCustomer)
        {
            var scoped = AuthHelper.CurrentCustomerId(User);
            if (!scoped.HasValue)
                return -1;
            return scoped;
        }

        if (!customerId.HasValue || !await _context.Customers.AnyAsync(c => c.CustomerId == customerId))
            return -1;

        return customerId;
    }
}
