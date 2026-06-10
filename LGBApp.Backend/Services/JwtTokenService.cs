using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LGBApp.Backend.Models;
using Microsoft.IdentityModel.Tokens;

namespace LGBApp.Backend.Services;

public class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role),
        };
        if (user.CustomerId.HasValue)
            claims.Add(new Claim("customer_id", user.CustomerId.Value.ToString()));
        if (user.MustChangePassword)
            claims.Add(new Claim("must_change_password", "true"));
        if (user.CanApproveMoiIntake)
            claims.Add(new Claim("can_approve_moi_intake", "true"));

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims.ToArray(),
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
