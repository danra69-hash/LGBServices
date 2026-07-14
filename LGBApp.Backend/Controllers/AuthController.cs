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
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly JwtTokenService _tokenService;
    private readonly SignatoryAccessService _signatoryAccess;
    private readonly PasswordResetService _passwordReset;

    public AuthController(
        AppDbContext context,
        JwtTokenService tokenService,
        SignatoryAccessService signatoryAccess,
        PasswordResetService passwordReset)
    {
        _context = context;
        _tokenService = tokenService;
        _signatoryAccess = signatoryAccess;
        _passwordReset = passwordReset;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, IWebHostEnvironment env)
    {
        if (!env.IsDevelopment())
            return NotFound();
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            return Conflict("Email is already registered.");

        var user = new User
        {
            Email = request.Email,
            PasswordHash = PasswordHasher.Hash(request.Password),
            Name = request.Name,
            Mobile = request.Mobile,
            Role = "User",
            IsVerified = false,
            MustChangePassword = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new AuthResponse
        {
            Token = _tokenService.GenerateToken(user),
            User = UserMapper.ToResponse(user)
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim();
        var user = await _context.Users
            .Include(u => u.Customer!)
            .ThenInclude(c => c.AccountHolders)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        if (user == null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            return Unauthorized("Invalid email or password.");

        var accessibleIds = await _signatoryAccess.GetAccessibleCustomerIdsAsync(_context, user);
        return Ok(new AuthResponse
        {
            Token = _tokenService.GenerateToken(user, accessibleIds),
            User = await UserMapper.ToResponseAsync(_context, user, user.Customer),
        });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult<MessageResponse>> ForgotPassword(ForgotPasswordRequest request)
    {
        try
        {
            await _passwordReset.RequestOtpAsync(request.Email ?? string.Empty);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("wait a minute", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ex.Message);
        }
        catch (Exception)
        {
            // Still return generic success for mail failures after OTP was stored —
            // client can retry; avoid leaking whether the address exists.
        }

        return Ok(new MessageResponse
        {
            Message = "If that email is registered, a reset code has been sent.",
        });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<ActionResult<MessageResponse>> ResetPassword(ResetPasswordWithOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            return BadRequest("New password must be at least 6 characters.");

        if (request.NewPassword != request.ConfirmPassword)
            return BadRequest("New password and confirmation do not match.");

        try
        {
            await _passwordReset.ResetPasswordAsync(
                request.Email ?? string.Empty,
                request.Code ?? string.Empty,
                request.NewPassword);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        return Ok(new MessageResponse
        {
            Message = "Password updated. You can sign in with your new password.",
        });
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> ChangePassword(ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            return BadRequest("New password must be at least 6 characters.");

        if (request.NewPassword != request.ConfirmPassword)
            return BadRequest("New password and confirmation do not match.");

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await _context.Users
            .Include(u => u.Customer!)
            .ThenInclude(c => c.AccountHolders)
            .FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null)
            return NotFound();

        if (!PasswordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest("Current password is incorrect.");

        if (PasswordHasher.Verify(request.NewPassword, user.PasswordHash))
            return BadRequest("New password must be different from the current password.");

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        user.MustChangePassword = false;
        await _context.SaveChangesAsync();

        var accessibleIds = await _signatoryAccess.GetAccessibleCustomerIdsAsync(_context, user);
        return Ok(new AuthResponse
        {
            Token = _tokenService.GenerateToken(user, accessibleIds),
            User = await UserMapper.ToResponseAsync(_context, user, user.Customer),
        });
    }
}
