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
        JobTitle = user.JobTitle,
        CanRecommendMoi = user.CanRecommendMoi,
        CustomerId = user.CustomerId,
        CustomerName = user.Customer?.Company,
        IsVerified = user.IsVerified,
        MustChangePassword = user.MustChangePassword,
        CreatedAt = user.CreatedAt
    };
}
