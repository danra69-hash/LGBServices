using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;

namespace LGBApp.Backend.Services;

public static class UserMapper
{
    public static UserResponse ToResponse(User user) => new()
    {
        UserId = user.UserId,
        Email = user.Email,
        Name = user.Name,
        Mobile = user.Mobile,
        Role = user.Role,
        IsVerified = user.IsVerified,
        CreatedAt = user.CreatedAt
    };
}
